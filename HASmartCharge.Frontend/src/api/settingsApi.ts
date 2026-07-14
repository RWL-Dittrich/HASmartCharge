import { apiFetch } from '@/api/client'
import type { CarSettings, ChargerSettings, MqttSettings, PriceProviderSettings } from '@/types/settings'

export function getPriceSettings(): Promise<PriceProviderSettings> {
  return apiFetch<PriceProviderSettings>('/api/settings/price')
}

export function updatePriceSettings(settings: PriceProviderSettings): Promise<PriceProviderSettings> {
  return apiFetch<PriceProviderSettings>('/api/settings/price', {
    method: 'PUT',
    body: JSON.stringify(settings),
  })
}

export function getCarSettings(): Promise<CarSettings> {
  return apiFetch<CarSettings>('/api/settings/car')
}

export function updateCarSettings(settings: CarSettings): Promise<CarSettings> {
  return apiFetch<CarSettings>('/api/settings/car', {
    method: 'PUT',
    body: JSON.stringify(settings),
  })
}

export function getChargerSettings(): Promise<ChargerSettings> {
  return apiFetch<ChargerSettings>('/api/settings/charger')
}

export function updateChargerSettings(settings: ChargerSettings): Promise<ChargerSettings> {
  return apiFetch<ChargerSettings>('/api/settings/charger', {
    method: 'PUT',
    body: JSON.stringify(settings),
  })
}

export function getMqttSettings(): Promise<MqttSettings> {
  return apiFetch<MqttSettings>('/api/settings/mqtt')
}

export function updateMqttSettings(settings: MqttSettings): Promise<MqttSettings> {
  return apiFetch<MqttSettings>('/api/settings/mqtt', {
    method: 'PUT',
    body: JSON.stringify(settings),
  })
}
