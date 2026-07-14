import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getMqttStatus, testMqtt } from '@/api/mqttApi'

export const mqttKeys = {
  status: ['mqtt', 'status'] as const,
}

export function useMqttStatus() {
  return useQuery({ queryKey: mqttKeys.status, queryFn: getMqttStatus, refetchInterval: 10_000 })
}

export function useTestMqtt() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: testMqtt,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: mqttKeys.status }),
  })
}
