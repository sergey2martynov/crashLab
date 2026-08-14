const BASE_URL = import.meta.env.VITE_GATEWAY_URL

export async function apiFetch<T>(path: string, accessToken: string, options: RequestInit = {}): Promise<T> {
    const res = await fetch(`${BASE_URL}${path}`, {
        ...options,
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${accessToken}`,
            ...options.headers
        }
    })

    if (!res.ok) {
        throw new Error(`API error ${res.status}: ${await res.text()}`)
    }

    return res.json()
}