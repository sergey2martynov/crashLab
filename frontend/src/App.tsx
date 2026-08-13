import {Routes, Route } from 'react-router-dom'
import './App.css'
import LoginPage from "./pages/LoginPage";
import CallbackPage from "./pages/CallbackPage.tsx";
import GamePage from "./pages/GamePage";

function App() {

  return (
    <Routes>
        <Route path="/" element={<LoginPage />} />
        <Route path="callback" element={<CallbackPage />} />
        <Route path="game/:tableId" element={<GamePage />} />
    </Routes>
  )
}

export default App
