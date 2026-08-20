import { useEffect, useRef, useState } from "react";
import { useParams } from 'react-router-dom'
import { HubConnectionBuilder, type HubConnection} from "@microsoft/signalr";
import {useAuth} from "../auth/useAuth.ts";
import { placeBet, cashOut, type Bet } from "../api/betsApi.ts";
import { getBalance } from '../api/walletApi.ts'
import { LineChart, Line, XAxis, YAxis, ResponsiveContainer, ReferenceDot } from 'recharts'

function GamePage() {
    const { tableId } = useParams()
    const user = useAuth()
    const [resolvedTableId, setResolvedTableId] = useState<string | null>(null)
    const [tickData, setData] = useState<{ roundId: string; tableId: string; multiplier: number; state: string } | null>(null)
    const [betAmount, setAmount] = useState<number>(50)
    const [activeBet, setActiveBet] = useState<Bet | null>(null)
    const [balance, setBalance] = useState<number | null>(null)
    const [history, setHistory] = useState<{ time: number; multiplier: number }[]>([])
    const lastPoint = history[history.length - 1]
    const connectionRef = useRef<HubConnection | null>(null)

    async function handleBetClick() {
        if(!user || !tickData || !resolvedTableId) return;
        try {
            const bet = await placeBet(resolvedTableId, tickData.roundId, betAmount, user.access_token)
            setActiveBet(bet)
            getBalance(user.access_token).then(w => setBalance(w.balance))
        } catch (err) {
            if (err instanceof Error && err.message.includes('Table is closed') && connectionRef.current) {
                const instanceId = await connectionRef.current.invoke<string>('JoinTable', tableId!)
                setResolvedTableId(instanceId)
                setData(null)
                setHistory([])
            } else {
                console.error(err)
            }
        }
    }

    async function handleCashoutClick() {

        if (!user || !activeBet) return
        const amount = await cashOut(activeBet.id, user.access_token)
        console.log('cashed out', amount)
        setActiveBet(null)
        getBalance(user.access_token).then(w => setBalance(w.balance))
    }

    useEffect(() => {
        if (!user) return
        getBalance(user.access_token).then(w => setBalance(w.balance))
    }, [user])

    useEffect(() => {
        if(!user) return

        const connection = new HubConnectionBuilder()
            .withUrl(`${import.meta.env.VITE_GATEWAY_URL}/gamehub?access_token=${user.access_token}`, {
                withCredentials: true
            })
            .build()

        connectionRef.current = connection
        connection.on('ReceiveTick', (data) => {
            setData(data)
            setHistory(prev => data.state === 'WaitingForBets' ? [] : [...prev, { time: data.serverTime, multiplier: data.multiplier }])
        })

        connection.on('BetSettled', (data) => {
            console.log('BetSettled', data)
            setActiveBet(prev => prev?.id === data.betId ? null : prev)
        })

        connection.start()
            .then(() => connection.invoke<string>('JoinTable', tableId!))
            .then(instanceId => setResolvedTableId(instanceId))
            .catch(err => console.error(err))

        return () => {
            connection.stop()
            connectionRef.current = null
        }
    }, [user, tableId]);
    return (
        <div>
            <h1>Table: {resolvedTableId ?? tableId}</h1>
            <p>Multiplier: {tickData?.multiplier ?? '-'}</p>
            <div>
                <input
                    type="number"
                    value={betAmount}
                    onChange={e => setAmount(Number(e.target.value))}>
                </input>
                <button
                    onClick = {handleBetClick}
                    disabled={tickData?.state !== 'WaitingForBets'}>
                    Bet
                </button>
                <button
                    onClick = {handleCashoutClick}
                    disabled={!activeBet || tickData?.state !== 'Running'}>
                    CashOut
                </button>
            </div>
            <div>
                <p>Balance: {balance ?? '-'}</p>
                <p>Active bet: {activeBet ? `${activeBet.id} (${activeBet.amount})` : 'No'}</p>
            </div>
            <ResponsiveContainer width="100%" height={300}>
                <LineChart data={history} margin={{ right: 50 }}>
                    <XAxis
                        dataKey="time"
                        tickFormatter={(value) => {
                            const start = new Date(history[0]?.time ?? value).getTime()
                            return `${((new Date(value).getTime() - start) / 1000).toFixed(1)}s`
                        }}
                    />
                    <YAxis domain={['auto', 'auto']} />
                    <Line type="monotone" dataKey="multiplier" dot={false} isAnimationActive={false} />
                    {lastPoint && (
                        <ReferenceDot
                            x={lastPoint.time}
                            y={lastPoint.multiplier}
                            r={4}
                            label={{ value: `${lastPoint.multiplier.toFixed(2)}x`, position: 'right' }}
                        />
                    )}
                </LineChart>
            </ResponsiveContainer>
        </div>

)
}

export default GamePage