const BASE = new URLSearchParams(location.search).get('hub') || 'http://localhost:5000'

function unwrap<T>(body: unknown): T {
  if (body && typeof body === 'object' && 'success' in (body as Record<string, unknown>)) {
    const apiRes = body as Record<string, unknown>
    if (!apiRes.success) throw new Error((apiRes.message as string) || (apiRes.error as string) || 'API error')
    return apiRes.data as T
  }
  return body as T
}

export async function getJson<T = unknown>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`)
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json().then(body => unwrap<T>(body))
}

export async function putJson<T = unknown>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json().then(body => unwrap<T>(body))
}

export async function postJson<T = unknown>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json().then(body => unwrap<T>(body))
}

export async function delJson<T = unknown>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'DELETE',
  })
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.json().then(body => unwrap<T>(body))
}
