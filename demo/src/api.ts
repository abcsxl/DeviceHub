const BASE = ''

let currentLang = 'en-US'

export function setLanguage(lang: string) {
  currentLang = lang
}

function headers(): Record<string, string> {
  return { 'Accept-Language': currentLang }
}

export async function getJson<T = unknown>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`, { headers: headers() })
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json()
}

export async function putJson<T = unknown>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'PUT',
    headers: { ...headers(), 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json()
}

export async function postJson<T = unknown>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: { ...headers(), 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json()
}

export async function delJson<T = unknown>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'DELETE',
    headers: headers(),
  })
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json()
}
