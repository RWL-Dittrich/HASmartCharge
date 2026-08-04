# DataTransfer

The vendor extension escape hatch: carries anything not covered by standard OCPP. What the
charger accepts is entirely model-specific — consult its vendor documentation.

## Request

| Field | Type | Required | Values / notes |
| --- | --- | --- | --- |
| `vendorId` | string (255) | yes | Reverse-domain vendor identifier, e.g. `com.example`. |
| `messageId` | string (50) | no | Vendor-defined message name. |
| `data` | string | no | Free-form payload. Often JSON encoded as a string. |

## Response

| Field | Type | Values / notes |
| --- | --- | --- |
| `status` | enum | `Accepted`, `Rejected`, `UnknownMessageId`, `UnknownVendorId` |
| `data` | string | Vendor-defined reply. Optional. |

## Notes

- `UnknownVendorId` means the charger does not recognise the vendor string — the usual answer
  when guessing.
- `data` is a **string** in OCPP 1.6, not an object. To send structured data, stringify it.
- Effects are undocumented by definition. Safe to probe with a nonsense `messageId`; unsafe to
  fire vendor commands you have not read up on.
