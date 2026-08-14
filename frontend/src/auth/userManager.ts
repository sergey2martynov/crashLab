import { UserManager } from 'oidc-client-ts'

export const userManager = new UserManager({
    authority: import.meta.env.VITE_IDENTITY_URL,
    client_id: 'crashlab-spa',
    redirect_uri: `${import.meta.env.VITE_APP_URL}/callback`,
    post_logout_redirect_uri: `${import.meta.env.VITE_APP_URL}/`,
    response_type: 'code',
    scope: 'openid profile',
});