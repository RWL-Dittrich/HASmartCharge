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
  startSocPercent: number | null
  endSocPercent: number | null
  /** Grid → battery ratio for this session alone; null without SoC readings at both ends. */
  efficiency: number | null
  /** False when the session was too small/noisy to feed the settings-page estimate. */
  efficiencyCounted: boolean
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
