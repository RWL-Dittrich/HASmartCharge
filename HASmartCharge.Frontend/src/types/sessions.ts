export interface ChargeSessionSummary {
  transactionId: number
  chargePointId: string
  connectorId: number
  startedAt: string
  completedAt: string | null
  totalKwh: number
  totalCost: number
  avgPricePerKwh: number | null
  planId: number | null
}

export interface HourlyBreakdown {
  hourStartUtc: string
  energyKwh: number
  pricePerKwh: number
  cost: number
}

export interface ChargeSessionDetail extends ChargeSessionSummary {
  hourlyBreakdown: HourlyBreakdown[]
}
