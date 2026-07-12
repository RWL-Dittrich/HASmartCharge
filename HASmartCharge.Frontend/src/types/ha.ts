export interface HaStatus {
  connected: boolean
  baseUrl: string | null
  tokenExpiresAt: string | null
}

export interface HaEntity {
  entityId: string
  friendlyName: string | null
  state: string | null
}

export interface HaServiceDomain {
  domain: string
  services: string[]
}
