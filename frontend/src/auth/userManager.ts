import { UserManager } from 'oidc-client-ts'

export const userManager = new UserManager({
    authority: 'https://localhost:7289',
    client_id: 'crashlab-spa',
    redirect_uri: 'http://localhost:5173/callback',
    post_logout_redirect_uri: 'http://localhost:5173/',
    response_type: 'code',
    scope: 'openid profile',
});