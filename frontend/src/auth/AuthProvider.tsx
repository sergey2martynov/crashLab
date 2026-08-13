import { useEffect, useState, type ReactNode } from 'react'
import type { User } from 'oidc-client-ts'
import { userManager } from './userManager'
import { AuthContext } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<User | null>(null)

    useEffect(() => {
        userManager.getUser().then(setUser)
        userManager.events.addUserLoaded(setUser)
    }, [])

    return <AuthContext.Provider value={user}>{children}</AuthContext.Provider>
}
