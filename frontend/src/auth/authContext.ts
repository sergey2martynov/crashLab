import { createContext } from 'react'
import type { User } from 'oidc-client-ts'

export const AuthContext = createContext<User | null>(null)
