import { apiFetch } from './client'

export interface Bet {
    id: string
    roundId: string
    accountId: string
    amount: number
    tableId: string
    status: string
}

export function placeBet(tableId: string, roundId: string, amount: number, accessToken: string) {
    return apiFetch<Bet>('/bets', accessToken, {
        method: 'POST',
        body: JSON.stringify({ tableId, roundId, amount })
    })
}

export function cashOut(betId: string, accessToken: string) {
    return apiFetch<number>(`/bets/${betId}/cashout`, accessToken, {
        method: 'POST'
    })
}