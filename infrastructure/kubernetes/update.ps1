# Deploy the backend to the selected Kubernetes context. For a remote cluster,
# pass a registry path, for example: -Context my-cluster -Registry mydockerid/luvora.
# The remote cluster must already have the postgres-secret, luvora-db-secret,
# luvora-app-secret, and rabbitmq-secret configured. Persistent storage must be
# available for PVCs (apply the appropriate StorageClass before this script).
param(
    [string]$Context = "docker-desktop",
    [string]$Registry = "",
    [string]$Tag = (Get-Date -Format "yyyyMMdd-HHmmss"),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$k8sRoot = $PSScriptRoot

foreach ($tool in @("docker", "kubectl")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is not installed or not on PATH."
    }
}

$currentContext = (& kubectl config current-context).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not read the current kubectl context." }
if ($currentContext -ne $Context) {
    throw "Current Kubernetes context is '$currentContext', expected '$Context'. Run kubectl config use-context $Context and retry."
}

if (-not [string]::IsNullOrWhiteSpace($Registry) -and $Registry.EndsWith("/")) {
    $Registry = $Registry.TrimEnd("/")
}

$services = @(
    @{ Project = "AuthService"; Image = "luvora-auth-service"; Deployment = "auth-service" },
    @{ Project = "ProductService"; Image = "luvora-product-service"; Deployment = "product-service" },
    @{ Project = "InventoryService"; Image = "luvora-inventory-service"; Deployment = "inventory-service" },
    @{ Project = "CartService"; Image = "luvora-cart-service"; Deployment = "cart-service" },
    @{ Project = "OrderService"; Image = "luvora-order-service"; Deployment = "order-service" },
    @{ Project = "PaymentService"; Image = "luvora-payment-service"; Deployment = "payment-service" },
    @{ Project = "DeliveryService"; Image = "luvora-delivery-service"; Deployment = "delivery-service" },
    @{ Project = "NotificationService"; Image = "luvora-notification-service"; Deployment = "notification-service" },
    @{ Project = "ReviewService"; Image = "luvora-review-service"; Deployment = "review-service" },
    @{ Project = "PromotionService"; Image = "luvora-promotion-service"; Deployment = "promotion-service" },
    @{ Project = "SupportService"; Image = "luvora-support-service"; Deployment = "support-service" },
    @{ Project = "ReportingService"; Image = "luvora-reporting-service"; Deployment = "reporting-service" },
    @{ Project = "ConfigurationService"; Image = "luvora-configuration-service"; Deployment = "configuration-service" },
    @{ Project = "AuditService"; Image = "luvora-audit-service"; Deployment = "audit-service" },
    @{ Project = "ApiGateWay"; Image = "luvora-api-gateway"; Deployment = "api-gateway" }
)

if (-not $SkipBuild) {
    foreach ($service in $services) {
        $dockerfile = Join-Path $repoRoot "services\$($service.Project)\Dockerfile"
        Write-Host "Building $($service.Image):$Tag"
        & docker build -t "$($service.Image):$Tag" -f $dockerfile (Join-Path $repoRoot "services")
        if ($LASTEXITCODE -ne 0) { throw "Docker build failed: $($service.Project)" }

        if (-not [string]::IsNullOrWhiteSpace($Registry)) {
            $remoteImage = "$Registry/$($service.Image):$Tag"
            & docker tag "$($service.Image):$Tag" $remoteImage
            if ($LASTEXITCODE -ne 0) { throw "Docker tag failed: $remoteImage" }
            & docker push $remoteImage
            if ($LASTEXITCODE -ne 0) { throw "Docker push failed: $remoteImage" }
        }
    }
}

Write-Host "Applying Kubernetes manifests to context '$Context'"
& kubectl apply -f (Join-Path $k8sRoot "base\namespace.yaml")
if ($LASTEXITCODE -ne 0) { throw "Namespace apply failed." }

# Apply workload/dependency definitions only. Do not reapply sample Secret templates.
foreach ($manifest in @(
    "database\postgres-pvc.yaml",
    "database\postgres-deployment.yaml",
    "database\postgres-service.yaml",
    "messaging\rabbitmq-deployment.yaml",
    "messaging\rabbitmq-service.yaml",
    "backend\product-images-pvc.yaml",
    "backend\services.yaml",
    "gateway\deployment.yaml",
    "gateway\service.yaml",
    "gateway\hpa.yaml",
    "network\default-deny.yaml",
    "network\gateway-to-services.yaml",
    "database\postgres-network-policy.yaml",
    "messaging\rabbitmq-network-policy.yaml"
)) {
    & kubectl apply -f (Join-Path $k8sRoot $manifest)
    if ($LASTEXITCODE -ne 0) { throw "Manifest apply failed: $manifest" }
}

# Ensure the manifests and freshly built local images use the same tag.
foreach ($service in $services) {
    $image = if ([string]::IsNullOrWhiteSpace($Registry)) {
        "$($service.Image):$Tag"
    } else {
        "$Registry/$($service.Image):$Tag"
    }
    & kubectl set image "deployment/$($service.Deployment)" "$($service.Deployment)=$image" -n luvora
    if ($LASTEXITCODE -ne 0) { throw "Could not set image for $($service.Deployment)." }
}

# Remove the old single frontend workload and Service if present; preserve databases and PVCs.
& kubectl delete deployment frontend service frontend -n luvora --ignore-not-found
if ($LASTEXITCODE -ne 0) { throw "Could not remove legacy frontend resources." }

foreach ($service in $services) {
    Write-Host "Waiting for $($service.Deployment)"
    & kubectl rollout status "deployment/$($service.Deployment)" -n luvora --timeout=180s
    if ($LASTEXITCODE -ne 0) { throw "Rollout did not complete for $($service.Deployment)." }
}

Write-Host "Deployment updated. Current pods:"
& kubectl get pods -n luvora -o wide
