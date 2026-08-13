import { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { userManager } from "../auth/userManager";

function CallbackPage() {
    const navigate = useNavigate();
    const hasRun = useRef(false);
    useEffect (() => {
        if (hasRun.current) return;
        hasRun.current = true;

        userManager.signinRedirectCallback().then((user) => {
            console.log('logged in ', user);
            navigate('/game/table-1');
        })
    }, [navigate])
    return(
        <div>
            <h1>Login handling...</h1>
        </div>
    )
}

export default CallbackPage