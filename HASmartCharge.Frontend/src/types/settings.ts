export interface PriceProviderSettings {
  id: number
  apiUrl: string
  supplierSlug: string
  currency: string
  refreshMinutes: number
}

/** Charge efficiency measured from real sessions, for checking the configured value. */
export interface EfficiencyEstimate {
  configuredEfficiency: number
  measuredEfficiency: number | null
  /** Sessions that passed the noise thresholds and fed the estimate. */
  sessionCount: number
  /** Completed sessions that had both SoC readings at all. */
  candidateSessionCount: number
  batteryKwh: number
  gridKwh: number
  plausible: boolean
}

export interface CarSettings {
  id: number
  name: string
  batteryCapacityKwh: number
  targetSocPercent: number
  chargeEfficiency: number
  haSocEntityId: string
  haStartDomain: string
  haStartService: string
  haStartDataJson: string | null
  haStopDomain: string
  haStopService: string
  haStopDataJson: string | null
  haPluggedInEntityId: string | null
  haChargingStateEntityId: string | null
  haTargetSocEntityId: string | null
}

export interface MqttSettings {
  id: number
  enabled: boolean
  host: string
  port: number
  username: string | null
  password: string | null
  useTls: boolean
  clientId: string
  baseTopic: string
  discoveryPrefix: string
}

export type ChargePowerControlMode = 'ChargingProfile' | 'Configuration'

export const CHARGE_POWER_UNITS = ['A', 'mA', 'W', 'kW'] as const
export type ChargePowerUnit = (typeof CHARGE_POWER_UNITS)[number]

export interface ChargerSettings {
  id: number
  chargePointId: string
  friendlyName: string
  maxChargeKw: number
  connectorId: number
  chargePowerMinKw: number
  chargePowerMaxKw: number
  /** Last power ceiling applied via OCPP; written by POST /api/charger/power, not the settings PUT. */
  chargePowerSetpointKw: number
  /** How the slider reaches the charger: SetChargingProfile, or ChangeConfiguration on a vendor key. */
  chargePowerControlMode: ChargePowerControlMode
  /** Configuration key written in Configuration mode, e.g. USER_PMAX. */
  chargePowerConfigurationKey: string
  /** Unit that key expects; the kW setpoint is converted to it. */
  chargePowerConfigurationUnit: ChargePowerUnit
  /** Per-phase supply voltage + phase count: used server-side to convert the kW setpoint to amps. */
  supplyVoltage: number
  phaseCount: number
  heartbeatInterval: number
  meterValueSampleInterval: number
  clockAlignedDataInterval: number
  meterValuesSampledData: string
}
