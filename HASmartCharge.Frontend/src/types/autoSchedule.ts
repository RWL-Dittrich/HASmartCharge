export interface WeeklyDeparture {
  dayOfWeek: number // 0 = Sunday, matching System.DayOfWeek
  enabled: boolean
  departureLocal: string // "HH:mm"
  targetSocPercent: number | null // null = use car default
}

export interface ScheduleOverride {
  id: number
  dateLocal: string // "yyyy-MM-dd"
  departureLocal: string // "HH:mm"
  targetSocPercent: number | null // null = use car default
}

export interface AutoScheduleSettings {
  enabled: boolean
  timeZoneId: string
  weekly: WeeklyDeparture[]
  overrides: ScheduleOverride[]
  nextDepartureUtc: string | null
  nextTargetSocPercent: number | null
}

export interface UpdateAutoScheduleRequest {
  enabled: boolean
  timeZoneId: string
  weekly: WeeklyDeparture[]
}

export interface OverrideRequest {
  dateLocal: string
  departureLocal: string
  targetSocPercent: number | null
}
