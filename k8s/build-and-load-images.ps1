$ErrorActionPreference = "Stop"

$clusterName = "crashlab"

$services = @(
    @{ Name = "walletservice";    Path = "./CrashLab.Game/CrashLab.WalletService" },
    @{ Name = "gateway";          Path = "./CrashLab.Game/CrashLab.GateWay" },
    @{ Name = "gameengine";       Path = "./CrashLab.Game/CrashLab.GameEngine" },
    @{ Name = "realtimegateway";  Path = "./CrashLab.Game/CrashLab.RealTimeGateWay" },
    @{ Name = "settlementservice"; Path = "./CrashLab.Game/CrashLab.SettlementService" },
    @{ Name = "identityserver";   Path = "./CrashLab.Game/CrashLab.IdentityServer" }
)

foreach ($service in $services) {
    $image = "$($service.Name):local"
    Write-Host "=== Building $image ===" -ForegroundColor Cyan
    docker build -t $image $service.Path
    if ($LASTEXITCODE -ne 0) { throw "docker build failed for $($service.Name)" }

    Write-Host "=== Loading $image into kind cluster '$clusterName' ===" -ForegroundColor Cyan
    kind load docker-image $image --name $clusterName
    if ($LASTEXITCODE -ne 0) { throw "kind load failed for $($service.Name)" }
}

Write-Host "=== Building frontend:local ===" -ForegroundColor Cyan
docker build -t frontend:local `
    --build-arg VITE_GATEWAY_URL="http://api.crashlab.local:8090" `
    --build-arg VITE_IDENTITY_URL="http://identity.crashlab.local:8090" `
    --build-arg VITE_APP_URL="http://app.crashlab.local:8090" `
    ./frontend
if ($LASTEXITCODE -ne 0) { throw "docker build failed for frontend" }

Write-Host "=== Loading frontend:local into kind cluster '$clusterName' ===" -ForegroundColor Cyan
kind load docker-image frontend:local --name $clusterName
if ($LASTEXITCODE -ne 0) { throw "kind load failed for frontend" }

Write-Host "=== Done ===" -ForegroundColor Green
