export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

// Resolve API paths against the document base URI so requests carry the HA ingress
// prefix when present (<base href>), and stay absolute-from-root when standalone.
function resolveUrl(url: string): string {
  return new URL(url.replace(/^\//, ''), document.baseURI).toString()
}

export async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(resolveUrl(url), {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })

  if (!response.ok) {
    let message = `HTTP ${response.status}`
    try {
      const body = await response.json()
      message = body?.error ?? body?.errorDescription ?? message
    } catch {
      // ignore parse failure
    }
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}
