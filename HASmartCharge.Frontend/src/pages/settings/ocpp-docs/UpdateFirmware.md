# UpdateFirmware

Tells the charger to download and install firmware from a location you host.

> **This is the most destructive call on this page.** A wrong or corrupt image can brick the
> hardware, and the charger will reboot mid-update. There is no OCPP way to cancel it once the
> retrieve date passes.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `location` | URI | yes | The firmware **file**. Typically `ftp://user:pass@host/firmware.bin`. |
| `retrieveDate` | dateTime | yes | When to start downloading. A past date means immediately. |
| `retries` | integer | no | Download attempts. |
| `retryInterval` | integer | no | Seconds between attempts. |

## Response

_No fields — an empty object `{}` on success._

## Notes

- Because the response is empty, `Accepted` is implied by the absence of a CALLERROR. Progress
  comes as `FirmwareStatusNotification` messages (`Downloaded`, `DownloadFailed`, `Downloading`,
  `Idle`, `InstallationFailed`, `Installing`, `Installed`) — watch the live log.
- The charger drops the OCPP link while installing and re-sends `BootNotification` afterwards.
- The example payload's `retrieveDate` is generated when you pick the template, so it means
  "now". Set it explicitly if you want a scheduled window.
- Credentials in the URL travel in clear text and are recorded in the frame log.
