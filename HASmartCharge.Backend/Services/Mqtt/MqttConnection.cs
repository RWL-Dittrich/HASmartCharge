using HASmartCharge.Backend.DB.Models;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using MQTTnet.Server; // MqttPendingMessagesOverflowStrategy lives here in v4

namespace HASmartCharge.Backend.Services.Mqtt;

/// <summary>
/// Connection-relevant MQTT settings plus the topics the connection needs at the transport layer
/// (the LWT status topic and the topics to subscribe to). Kept as a plain record so
/// <see cref="MqttConnection"/> stays the only type that knows the DB entity shape doesn't matter.
/// </summary>
public sealed record MqttConnectionOptions(
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool UseTls,
    string ClientId,
    string StatusTopic,
    IReadOnlyList<string> SubscribeTopics)
{
    public static MqttConnectionOptions From(MqttSettings s)
    {
        var topics = new MqttTopics(s.BaseTopic, s.DiscoveryPrefix);
        return new MqttConnectionOptions(
            s.Host, s.Port, s.Username, s.Password, s.UseTls, s.ClientId,
            topics.Status,
            new[] { topics.SwitchSet, topics.HaBirth });
    }
}

/// <summary>
/// The ONLY type that touches MQTTnet. Wraps a <see cref="IManagedMqttClient"/> (auto-reconnect
/// every 5s, auto-resubscribe, bounded drop-oldest outbound queue) and an LWT that flips the app
/// status topic to "offline" on an ungraceful drop. Raises framework-agnostic callbacks so the rest
/// of the app never references an MQTTnet type. A future MQTTnet v5 migration is confined here.
/// </summary>
public sealed class MqttConnection : IAsyncDisposable
{
    private readonly IManagedMqttClient _client;
    private readonly MqttConnectionOptions _options;
    private readonly ILogger _logger;

    /// <summary>Raised for every inbound message: (topic, payload). Never on the caller's thread.</summary>
    public Func<string, string, Task>? OnMessage { get; set; }

    /// <summary>Raised each time the client (re)connects to the broker.</summary>
    public Func<Task>? OnConnected { get; set; }

    /// <summary>Raised when a connect attempt fails, with a short reason (for throttled status/logging).</summary>
    public Func<string, Task>? OnConnectingFailed { get; set; }

    public MqttConnection(MqttConnectionOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        var factory = new MqttFactory();
        _client = factory.CreateManagedMqttClient(factory.CreateMqttClient());

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;
                if (OnMessage is { } handler)
                {
                    await handler(topic, payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling inbound MQTT message.");
            }
        };

        _client.ConnectedAsync += async _ =>
        {
            try
            {
                if (OnConnected is { } handler)
                {
                    await handler();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MQTT connected handler.");
            }
        };

        _client.ConnectingFailedAsync += async e =>
        {
            try
            {
                var reason = e.Exception?.Message ?? e.ConnectResult?.ResultCode.ToString() ?? "connect failed";
                if (OnConnectingFailed is { } handler)
                {
                    await handler(reason);
                }
            }
            catch
            {
                // status/logging callback must never break the client
            }
        };
    }

    public bool IsConnected => _client.IsConnected;

    public async Task StartAsync()
    {
        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(BuildClientOptions(_options, includeWill: true, _options.ClientId))
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithMaxPendingMessages(500)
            .WithPendingMessagesOverflowStrategy(MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
            .Build();

        await _client.StartAsync(managedOptions);

        if (_options.SubscribeTopics.Count > 0)
        {
            var filters = _options.SubscribeTopics
                .Select(t => new MqttTopicFilterBuilder()
                    .WithTopic(t)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build())
                .ToList();

            // Managed client persists subscriptions and auto-resubscribes on every reconnect.
            await _client.SubscribeAsync(filters);
        }
    }

    /// <summary>
    /// Enqueues a retained (QoS 1) publish. The managed client owns delivery + retry, so this returns
    /// as soon as the message is queued, not when it reaches the broker (see <see cref="FlushAsync"/>
    /// for teardown). Note: an unknown sensor value must be the literal "None" (HA's PAYLOAD_NONE),
    /// NOT an empty payload — HA ignores empty payloads and keeps the previous value.
    /// </summary>
    public Task PublishAsync(string topic, string? payload, bool retain)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload ?? string.Empty)
            .WithRetainFlag(retain)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        return _client.EnqueueAsync(message);
    }

    /// <summary>
    /// Waits (up to <paramref name="timeout"/>) for the managed client's outbound queue to drain, so
    /// an enqueued message — notably the graceful "offline" — actually reaches the broker before the
    /// client is stopped. <c>StopAsync</c> clears the queue without flushing, and a clean disconnect
    /// suppresses the LWT, so without this the offline announcement would be dropped.
    /// </summary>
    public async Task FlushAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (_client.PendingApplicationMessagesCount > 0 && _client.IsConnected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _client.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping managed MQTT client during dispose.");
        }

        _client.Dispose();
    }

    /// <summary>
    /// One-shot connect/disconnect used by <c>POST /api/mqtt/test</c>. Uses a distinct client id
    /// (suffix "-test") so it never fights the live publisher's session for the same id on the broker.
    /// </summary>
    public static async Task<(bool ok, string? error)> TestConnectionAsync(MqttConnectionOptions options, CancellationToken ct)
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await client.ConnectAsync(
                BuildClientOptions(options, includeWill: false, options.ClientId + "-test"),
                timeoutCts.Token);

            if (result.ResultCode != MqttClientConnectResultCode.Success)
            {
                return (false, $"Broker refused connection: {result.ResultCode}");
            }

            await client.DisconnectAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static MqttClientOptions BuildClientOptions(MqttConnectionOptions options, bool includeWill, string clientId)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(options.Host, options.Port)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(options.Username))
        {
            builder = builder.WithCredentials(options.Username, options.Password ?? string.Empty);
        }

        if (options.UseTls)
        {
            builder = builder.WithTlsOptions(o => o.UseTls(true));
        }

        if (includeWill)
        {
            builder = builder
                .WithWillTopic(options.StatusTopic)
                .WithWillPayload("offline")
                .WithWillRetain(true)
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);
        }

        return builder.Build();
    }
}
