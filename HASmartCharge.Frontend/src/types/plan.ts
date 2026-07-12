export type ChargePlanStatus = 'Pending' | 'Active' | 'Completed' | 'Cancelled' | 'MissedDeadline'

export interface ChargePlan {
  id: number
  deadlineUtc: string
  targetSocPercent: number
  startSocPercent: number | null
  status: ChargePlanStatus
  estimatedEnergyKwh: number
  estimatedCost: number
  selectedHours: string[]
  createdAt: string
  completedAt: string | null
}

export interface PlanPreview {
  socPercent: number | null
  done: boolean
  feasible: boolean
  energyNeededKwh: number
  hoursNeeded: number
  chargeDurationHours: number
  selectedHours: string[]
  estimatedCost: number
  warning: string | null
}

export interface CreatePlanRequest {
  deadlineUtc: string
  targetSocPercent?: number
}
