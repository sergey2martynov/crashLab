$ErrorActionPreference = "Stop"

$clusterName = "crashlab"
$namespace = "crashlab"

Write-Host "=== Recreating kind cluster '$clusterName' ===" -ForegroundColor Cyan
$prevEAP = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
kind delete cluster --name $clusterName 2>$null | Out-Null
$ErrorActionPreference = $prevEAP
kind create cluster --config "$PSScriptRoot\kind-config.yaml" --name $clusterName
if ($LASTEXITCODE -ne 0) { throw "kind create cluster failed" }

Write-Host "=== Installing ingress-nginx controller ===" -ForegroundColor Cyan
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml
kubectl wait --namespace ingress-nginx --for=condition=ready pod --selector=app.kubernetes.io/component=controller --timeout=180s
if ($LASTEXITCODE -ne 0) { throw "ingress-nginx controller did not become ready" }

Write-Host "=== Patching CoreDNS (split-horizon DNS for identity.crashlab.local) ===" -ForegroundColor Cyan
kubectl apply -f "$PSScriptRoot\coredns-patch.yaml"
kubectl rollout restart deployment coredns -n kube-system
kubectl rollout status deployment coredns -n kube-system --timeout=60s

Write-Host "=== Creating namespace '$namespace' ===" -ForegroundColor Cyan
kubectl create namespace $namespace --dry-run=client -o yaml | kubectl apply -f -

Write-Host "=== Creating openiddict-secret (fresh random key each rebuild) ===" -ForegroundColor Cyan
$bytes = New-Object byte[] 32
$rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
$rng.GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)
kubectl create secret generic openiddict-secret --from-literal=key=$key -n $namespace --dry-run=client -o yaml | kubectl apply -f -

Write-Host "=== Creating postgres-secrets ===" -ForegroundColor Cyan
kubectl create secret generic postgres-secrets `
    --from-literal=crashlab-password=crashlab `
    --from-literal=gameengine-password=gameengine `
    --from-literal=settlement-password=settlement `
    --from-literal=identity-password=identity `
    -n $namespace --dry-run=client -o yaml | kubectl apply -f -

Write-Host "=== Building and loading images ===" -ForegroundColor Cyan
& "$PSScriptRoot\build-and-load-images.ps1"

Write-Host "=== Applying manifests ===" -ForegroundColor Cyan
$manifestFiles = Get-ChildItem "$PSScriptRoot\*.yaml" -Exclude "kind-config.yaml"
$applyArgs = @()
foreach ($file in $manifestFiles) {
    $applyArgs += "-f"
    $applyArgs += $file.FullName
}
kubectl apply @applyArgs

Write-Host "=== Waiting for Kafka ===" -ForegroundColor Cyan
kubectl rollout status deployment kafka -n $namespace --timeout=120s

Write-Host "=== Creating Kafka topics ===" -ForegroundColor Cyan
$topics = @("round.crashed", "bet.placed", "bet.cashed_out", "bet.settled", "dead-letter")
foreach ($topic in $topics) {
    kubectl exec -n $namespace deploy/kafka -- /opt/kafka/bin/kafka-topics.sh --create --if-not-exists --topic $topic --bootstrap-server localhost:9092 --partitions 3 --replication-factor 1
}

Write-Host "=== Done. Check: kubectl get pods -n $namespace ===" -ForegroundColor Green
