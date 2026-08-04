# GetDiagnostics

Tells the charger to upload a diagnostics archive to a location **you** host. The charger is the
client here: it needs network access to that URL, and credentials must be embedded in it.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `location` | URI | yes | Target **directory**, not a file. Typically `ftp://user:pass@host/path/`. |
| `retries` | integer | no | Upload attempts. Falls back to the charger's configured default. |
| `retryInterval` | integer | no | Seconds between attempts. |
| `startTime` | dateTime | no | Oldest log entry to include. |
| `stopTime` | dateTime | no | Newest log entry to include. |

## Response

| Field | Type | Notes |
| --- | --- | --- |
| `fileName` | string (255) | Name the charger will upload. Absent when it has nothing to send. |

## Notes

- Progress arrives as separate `DiagnosticsStatusNotification` messages
  (`Idle`, `Uploaded`, `UploadFailed`, `Uploading`) — watch the live log.
- Which schemes work is model-specific: FTP is the common one, HTTP/HTTPS PUT less so.
- Credentials in the URL are sent in clear text over the OCPP link and land in the frame log.
  Use a throwaway account.
- Non-disruptive to charging, but the upload consumes the charger's uplink.
