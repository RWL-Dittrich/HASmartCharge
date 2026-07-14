import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getCarSettings,
  getChargerSettings,
  getMqttSettings,
  getPriceSettings,
  updateCarSettings,
  updateChargerSettings,
  updateMqttSettings,
  updatePriceSettings,
} from '@/api/settingsApi'
import type { CarSettings, ChargerSettings, MqttSettings, PriceProviderSettings } from '@/types/settings'
import { mqttKeys } from '@/hooks/useMqtt'

export const settingsKeys = {
  price: ['settings', 'price'] as const,
  car: ['settings', 'car'] as const,
  charger: ['settings', 'charger'] as const,
  mqtt: ['settings', 'mqtt'] as const,
}

export function usePriceSettings() {
  return useQuery({ queryKey: settingsKeys.price, queryFn: getPriceSettings })
}

export function useUpdatePriceSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (settings: PriceProviderSettings) => updatePriceSettings(settings),
    onSuccess: (data) => queryClient.setQueryData(settingsKeys.price, data),
  })
}

export function useCarSettings() {
  return useQuery({ queryKey: settingsKeys.car, queryFn: getCarSettings })
}

export function useUpdateCarSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (settings: CarSettings) => updateCarSettings(settings),
    onSuccess: (data) => queryClient.setQueryData(settingsKeys.car, data),
  })
}

export function useChargerSettings() {
  return useQuery({ queryKey: settingsKeys.charger, queryFn: getChargerSettings })
}

export function useUpdateChargerSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (settings: ChargerSettings) => updateChargerSettings(settings),
    onSuccess: (data) => queryClient.setQueryData(settingsKeys.charger, data),
  })
}

export function useMqttSettings() {
  return useQuery({ queryKey: settingsKeys.mqtt, queryFn: getMqttSettings })
}

export function useUpdateMqttSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (settings: MqttSettings) => updateMqttSettings(settings),
    onSuccess: (data) => {
      queryClient.setQueryData(settingsKeys.mqtt, data)
      // Saving may connect/disconnect the publisher — refresh the live status card.
      queryClient.invalidateQueries({ queryKey: mqttKeys.status })
    },
  })
}
