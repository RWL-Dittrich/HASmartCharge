import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * Returns a human-readable duration string between two ISO timestamps
 * (or from an ISO start to now if `end` is omitted). e.g. "2h 14m", "45m", "< 1m"
 */
export function formatDuration(
  isoStart: string | null | undefined,
  isoEnd?: string | null,
): string {
  if (!isoStart) return '—'
  const end = isoEnd ? new Date(ensureUtcSuffix(isoEnd)).getTime() : Date.now()
  const ms = end - new Date(ensureUtcSuffix(isoStart)).getTime()
  if (ms < 0) return '—'
  const totalMinutes = Math.floor(ms / 60_000)
  if (totalMinutes < 1) return '< 1m'
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`
}

/** Format a fractional hour count as "Xh Ym" (e.g. 3.34 → "3h 20m"). */
export function formatHoursDuration(hours: number | null | undefined): string {
  if (hours == null || hours < 0) return '—'
  const totalMinutes = Math.round(hours * 60)
  if (totalMinutes < 1) return '< 1m'
  const h = Math.floor(totalMinutes / 60)
  const m = totalMinutes % 60
  return h > 0 ? `${h}h ${m}m` : `${m}m`
}

const CURRENCY_SYMBOLS: Record<string, string> = {
  EUR: '€',
  USD: '$',
  GBP: '£',
}

/** Resolves a currency code (e.g. "EUR") to its display symbol, falling back to the code itself. */
export function currencySymbol(currency: string | null | undefined): string {
  if (!currency) return '€'
  return CURRENCY_SYMBOLS[currency.toUpperCase()] ?? currency
}

/** Formats a monetary amount with 2 decimals, e.g. "€ 1.23". */
export function formatMoney(value: number | null | undefined, currency?: string | null): string {
  if (value === null || value === undefined || isNaN(value)) return '—'
  return `${currencySymbol(currency)} ${value.toFixed(2)}`
}

/** Formats a €/kWh price with 4 decimals, e.g. "€ 0.2431 / kWh". */
export function formatPricePerKwh(value: number | null | undefined, currency?: string | null): string {
  if (value === null || value === undefined || isNaN(value)) return '—'
  return `${currencySymbol(currency)} ${value.toFixed(4)} / kWh`
}

/** Formats kWh with 2 decimals, e.g. "12.34 kWh". */
export function formatKwh(value: number | null | undefined): string {
  if (value === null || value === undefined || isNaN(value)) return '—'
  return `${value.toFixed(2)} kWh`
}

/** Formats kW with 2 decimals, e.g. "7.40 kW". */
export function formatKw(value: number | null | undefined): string {
  if (value === null || value === undefined || isNaN(value)) return '—'
  return `${value.toFixed(2)} kW`
}

/**
 * Some backend endpoints (e.g. /api/prices, /api/plan/preview) serialize DateTimes without
 * a timezone designator because SQLite round-trips them as Kind=Unspecified. `new Date(...)`
 * would then parse them as local time instead of UTC. Force UTC by appending "Z" unless the
 * string already carries an offset.
 */
export function ensureUtcSuffix(iso: string): string {
  return /[Zz]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : `${iso}Z`
}

/** Formats a UTC ISO timestamp as a local date + time, e.g. "12 Jul, 14:30". */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = new Date(ensureUtcSuffix(iso))
  if (isNaN(date.getTime())) return '—'
  return date.toLocaleString(undefined, {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** Formats a UTC ISO timestamp as a local hour label, e.g. "14:00". */
export function formatHourLabel(iso: string): string {
  const date = new Date(ensureUtcSuffix(iso))
  return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
}

/** Converts a `datetime-local` input value (local time, no timezone) to a UTC ISO string. */
export function localInputToUtcIso(localValue: string): string {
  return new Date(localValue).toISOString()
}

/** Converts a UTC ISO string to a value usable in a `datetime-local` input (local time). */
export function utcIsoToLocalInput(iso: string): string {
  const date = new Date(iso)
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16)
}
