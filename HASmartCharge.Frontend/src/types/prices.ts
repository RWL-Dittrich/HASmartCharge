export interface HourlyPrice {
  hourStartUtc: string
  pricePerKwh: number
  fetchedAt: string
}

export interface PriceFetchResult {
  success: boolean
  pricesUpserted: number
  tomorrowAvailable: boolean
  error: string | null
}
