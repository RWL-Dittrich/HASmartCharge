import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'
import type { MeasurandValue } from '@/types/charger'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * Returns a human-readable duration string from an ISO timestamp to now.
 * e.g. "2h 14m", "45m", "< 1m"
 */
export function formatDuration(isoStart: string | null | undefined): string {
  if (!isoStart) return '—'
  const ms = Date.now() - new Date(isoStart).getTime()
  if (ms < 0) return '—'
  const totalMinutes = Math.floor(ms / 60_000)
  if (totalMinutes < 1) return '< 1m'
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`
}

/**
 * Formats a MeasurandValue to a clean display string.
 * Returns "—" for null/undefined values.
 */
export function formatMeasurand(
  mv: MeasurandValue | null | undefined,
  decimalPlaces = 1,
): string {
  if (!mv?.value) return '—'
  const num = parseFloat(mv.value)
  if (isNaN(num)) return mv.value
  const formatted = num.toFixed(decimalPlaces)
  return mv.unit ? `${formatted} ${mv.unit}` : formatted
}

