export interface PriceProviderSettings {
  id: number
  apiUrl: string
  supplierSlug: string
  currency: string
  refreshMinutes: number
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
  /** Per-phase supply voltage + phase count: used server-side to convert the kW setpoint to amps. */
  supplyVoltage: number
  phaseCount: number
  heartbeatInterval: number
  meterValueSampleInterval: number
  clockAlignedDataInterval: number
  meterValuesSampledData: string
}
