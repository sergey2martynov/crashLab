import {userManager} from "../auth/userManager"
import { useAuth } from "../auth/useAuth.ts";

function LoginPage() {
    const user = useAuth();
    function handleLoginClick() {
        userManager.signinRedirect()
    }

    if (user) {
        return(
            <div>
                <h1>Hello, {user.profile.name}</h1>
            </div>
        )
    }
    return (
        <div>
            <h1>Login</h1>
            <button onClick={handleLoginClick}>Login</button>
        </div>
    )
}

export default LoginPage