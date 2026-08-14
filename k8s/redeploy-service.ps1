param(
    [Parameter(Mandatory = $true)]
    [string[]]$Services
)

$ErrorActionPreference = "Stop"
$clusterName = "crashlab"
$namespace = "crashlab"

$pathMap = @{
    "walletservice"     = "./CrashLab.Game/CrashLab.WalletService"
    "gateway"           = "./CrashLab.Game/CrashLab.GateWay"
    "gameengine"        = "./CrashLab.Game/CrashLab.GameEngine"
    "realtimegateway"   = "./CrashLab.Game/CrashLab.RealTimeGateWay"
    "settlementservice" = "./CrashLab.Game/CrashLab.SettlementService"
    "identityserver"    = "./CrashLab.Game/CrashLab.IdentityServer"
}

foreach ($service in $Services) {
    $image = "$service`:local"

    if ($service -eq "frontend") {
        Write-Host "=== Rebuilding frontend ===" -ForegroundColor Cyan
        docker build -t frontend:local `
            --build-arg VITE_GATEWAY_URL="http://api.crashlab.local:8090" `
            --build-arg VITE_IDENTITY_URL="http://identity.crashlab.local:8090" `
            --build-arg VITE_APP_URL="http://app.crashlab.local:8090" `
            ./frontend
        if ($LASTEXITCODE -ne 0) { throw "docker build failed for frontend" }
    }
    elseif ($pathMap.ContainsKey($service)) {
        Write-Host "=== Rebuilding $service ===" -ForegroundColor Cyan
        docker build -t $image $pathMap[$service]
        if ($LASTEXITCODE -ne 0) { throw "docker build failed for $service" }
    }
    else {
        throw "Unknown service '$service'. Known: frontend, $($pathMap.Keys -join ', ')"
    }

    Write-Host "=== Loading $image into kind cluster '$clusterName' ===" -ForegroundColor Cyan
    kind load docker-image $image --name $clusterName
    if ($LASTEXITCODE -ne 0) { throw "kind load failed for $service" }

    Write-Host "=== Restarting deployment/$service ===" -ForegroundColor Cyan
    kubectl rollout restart deployment/$service -n $namespace
    if ($LASTEXITCODE -ne 0) { throw "kubectl rollout restart failed for $service" }
}

Write-Host "=== Done ===" -ForegroundColor Green
