import { apiFetch } from './client'

export interface Wallet {
    accountId: string
    balance: number
    version: number
}

export function getBalance(accessToken: string) {
    return apiFetch<Wallet>(`/balance`, accessToken)
}