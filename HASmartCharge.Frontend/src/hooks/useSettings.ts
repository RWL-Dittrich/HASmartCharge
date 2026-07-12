import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getCarSettings,
  getChargerSettings,
  getPriceSettings,
  updateCarSettings,
  updateChargerSettings,
  updatePriceSettings,
} from '@/api/settingsApi'
import type { CarSettings, ChargerSettings, PriceProviderSettings } from '@/types/settings'

export const settingsKeys = {
  price: ['settings', 'price'] as const,
  car: ['settings', 'car'] as const,
  charger: ['settings', 'charger'] as const,
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
