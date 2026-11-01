# Recreate Luvora backend on AWS EKS

This runbook recreates the backend deployment discussed in the Luvora deployment chat. It uses Amazon EKS in `sa-east-1` (Sao Paulo), ECR images in `eu-north-1` as currently configured in this repository, EKS Auto Mode compute and storage, PostgreSQL, and RabbitMQ. It maps and deploys all 15 backend images. The frontend is intentionally not deployed to Kubernetes; the API Gateway remains internal (`ClusterIP`) unless you follow the optional HTTPS ingress section. There is no frontend source directory in the current `D:\Luvora` checkout, so frontend build/hosting commands cannot be derived from this project version.

This is a reusable guide, so it uses placeholders and discovers the AWS account ID from your active CLI login. Set your own account, region, and ECR repository values when following it.

> **Costs and data:** EKS, compute nodes, EBS volumes, ALBs, and especially NAT gateways incur charges. Do not delete PVCs or PVs to troubleshoot: the configured reclaim policy is `Delete`, so deleting a claim can destroy its volume and data. This setup uses one replica per service and in-cluster PostgreSQL/RabbitMQ; treat it as a learning/demo deployment, not a production HA design. Before serving real users, set service environments to `Production`, move PostgreSQL to a managed database with backups, and move product images to durable object storage.

## 1. Sign in to AWS and prepare your workstation

This deployment uses four separate access steps:

| What you're signing in to | Command/action |
|---|---|
| AWS Console in your browser | Sign in at [AWS Console](https://console.aws.amazon.com/) with your AWS account/IAM Identity Center user. |
| AWS CLI for AWS API calls | `aws login`, then verify using `aws sts get-caller-identity`. |
| Docker for private ECR push | `aws ecr get-login-password ... | docker login ...` (Section 6). |
| `kubectl` for the EKS cluster | `aws eks update-kubeconfig ...`, then verify using `kubectl get nodes` (Section 4). |

Use the same AWS account for Console and CLI. Prefer an IAM Identity Center or IAM user/role with only the permissions you need; don't use root for routine administration. The identity needs permissions to manage EKS, IAM roles, EC2/VPC, ECR, and Auto Mode resources. Never put AWS credentials in this repository, Kubernetes Secrets, or chat.

### 1.1 Sign in to the AWS Console

Open [https://console.aws.amazon.com/](https://console.aws.amazon.com/) and sign in using your normal AWS sign-in method. If your account uses IAM Identity Center, choose the assigned account and role from the access portal. Switch the Console region selector to **South America (Sao Paulo), `sa-east-1`** before creating the cluster. ECR stays in **Europe (Stockholm), `eu-north-1`** for this project's existing repositories.

### 1.2 Install and sign in to the AWS CLI

Install or update [AWS CLI v2](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html). The recommended local developer login is `aws login`, which opens a browser and uses short-lived credentials; it requires AWS CLI **2.32.0 or newer**. Check the version first:

```powershell
aws --version
```

If the version is `2.32.0` or later, sign in:

```powershell
aws login
aws sts get-caller-identity
```

Complete the browser sign-in. The identity output should show the account you intend to use. Stop if it shows a different account; use the correct profile before creating resources. The short-lived CLI credentials refresh automatically during a session of up to 12 hours. If the browser cannot open, `aws login --remote` prints a URL and code for another device.

To store the account ID for later commands in this PowerShell session:

```powershell
$accountId = aws sts get-caller-identity --query Account --output text
Write-Host "Authenticated AWS account: $accountId"
```

If your organization already gave you an IAM Identity Center profile, keep using that instead of `aws login`:

```powershell
aws configure sso --profile luvora
aws sso login --profile luvora
$env:AWS_PROFILE = 'luvora'
aws sts get-caller-identity --profile luvora
```

At the `aws configure sso` prompts, use the Start URL, IAM Identity Center region, AWS account, and role provided by your organization. Don't invent these values. If `aws login` returns a permission error, an administrator may need to grant the `SignInLocalDevelopmentAccess` policy to your IAM user/role. For an existing named profile, pass `--profile luvora` on AWS commands or set `$env:AWS_PROFILE` as above.

### 1.3 Install/check local build tools

Install Docker Desktop and `kubectl`, then run these from `D:\Luvora`:

```powershell
docker version
kubectl version --client
aws sts get-caller-identity
```

If these commands work, you are signed in to AWS CLI and have the local tools needed to build and deploy. AWS CLI authentication alone does not log Docker into ECR or configure Kubernetes access; complete Sections 4 and 6 for those separate logins.

## 2. Create IAM roles in the AWS Console

Open **IAM > Roles > Create role**.

1. Create the EKS Auto Mode cluster role trusted by the EKS service. Attach `AmazonEKSClusterPolicy`, `AmazonEKSComputePolicy`, `AmazonEKSBlockStoragePolicyV2`, `AmazonEKSLoadBalancingPolicy`, and `AmazonEKSNetworkingPolicy`.
2. Create/select the Auto Mode node role trusted by **EC2**. Attach `AmazonEKSWorkerNodeMinimalPolicy` and `AmazonEC2ContainerRegistryPullOnly` so nodes can join the cluster and pull this account's ECR images.
3. Confirm the role names/ARNs before continuing. A worker instance with no IAM role, or a role without the required EKS worker permissions, cannot join the cluster.

If you deliberately use a *standard managed node group* instead of Auto Mode, use the standard EKS worker-node role permissions from the EKS console documentation. Do not mix standard EBS CSI storage (`ebs.csi.aws.com`) and Auto Mode storage (`ebs.csi.eks.amazonaws.com`) for the same PVCs.

## 3. Prepare networking and create the EKS cluster

In **VPC**, create/select a VPC with subnets in at least two Availability Zones. A common layout is public and private subnets in each AZ, an Internet Gateway for public subnets, and NAT egress for private subnets. EKS control-plane networking needs subnets in multiple AZs.

In **EKS → Clusters → Create cluster**:

1. Name: `luvora-cluster`.
2. Select a currently supported Kubernetes version and the cluster IAM role.
3. Select the VPC and subnets across at least two AZs.
4. Enable **EKS Auto Mode** and configure its system and general-purpose compute/node role. Keep application compute in private subnets when using NAT egress.
5. For the Kubernetes API endpoint, enable the access mode you need. If public access is enabled, restrict it to your current public IP where possible; avoid unrestricted access in a long-lived cluster.
6. Create the cluster and wait for **Active**.

Do not choose `t3.micro` expecting it to qualify for Free Tier or fit this workload. The earlier node-group attempt failed because that instance type was not eligible in the account/region; the undersized nodes also hit pod-capacity limits. Ensure Auto Mode can provision a general-purpose node with enough capacity for the 15 services plus databases and system pods.

### Subnet choices for nodes

Prefer private subnets with working NAT egress. If you intentionally place nodes in public subnets, each subnet must route to an Internet Gateway and automatically assign public IPv4 addresses (or instances must get public addresses another way). Selecting a public subnet with auto-assign public IP disabled caused the node-group launch failure seen in this chat. Keep cluster/control-plane subnet selection distinct from worker/node subnet selection.

## 4. Connect `kubectl` to the right cluster

Run this in PowerShell. The region is **`sa-east-1`**, not the ECR image region:

```powershell
aws eks update-kubeconfig --region sa-east-1 --name luvora-cluster
kubectl config current-context
kubectl get nodes -o wide
```

The context should contain `arn:aws:eks:sa-east-1:<ACCOUNT_ID>:cluster/luvora-cluster`. Nodes should eventually show `Ready`. `kubectl` uses the AWS CLI identity/profile to get EKS authentication tokens, so sign in to the correct CLI profile before running it.

If `update-kubeconfig` says `ResourceNotFoundException`, check the cluster name and region:

```powershell
aws eks list-clusters --region sa-east-1
aws eks describe-cluster --name luvora-cluster --region sa-east-1
```

If `kubectl get nodes` returns `Unauthorized` or `Forbidden`, recheck `aws sts get-caller-identity` and the active profile. A cluster administrator must add that IAM role/user under **EKS > luvora-cluster > Access** and grant an appropriate EKS access policy. Do not work around this by sharing someone else's kubeconfig or AWS credentials.

## 5. Check EKS-managed components, especially DNS

List add-ons:

```powershell
aws eks list-addons --cluster-name luvora-cluster --region sa-east-1
kubectl get pods,services -n kube-system
```

EKS Auto Mode has built-in cluster DNS and EBS storage capabilities. In this chat's mixed cluster, however, standard nodes had pods configured to query `172.20.0.10` while no `kube-dns` service/CoreDNS pods existed; Auth could not resolve `postgres`. Verify that service-name lookups work from your workload nodes. If using a mixed cluster like this one and CoreDNS / `kube-dns` is absent, check the add-on list, then install a version compatible with the cluster:

```powershell
aws eks describe-addon-versions --addon-name coredns --kubernetes-version 1.36 --region sa-east-1
aws eks create-addon --cluster-name luvora-cluster --addon-name coredns --region sa-east-1
aws eks wait addon-active --cluster-name luvora-cluster --addon-name coredns --region sa-east-1
kubectl get pods,service -n kube-system | Select-String 'coredns|kube-dns'
```

Use the cluster's actual Kubernetes minor version in `describe-addon-versions`. If the add-on already exists but is unhealthy, inspect it with `aws eks describe-addon` and repair/update that add-on rather than trying to create a duplicate.

This deployment uses EKS Auto Mode storage. **Do not install the traditional `aws-ebs-csi-driver` EKS add-on for these PVCs.** AWS documents Auto Mode block storage as a managed capability and uses the separate provisioner `ebs.csi.eks.amazonaws.com`; the failed traditional add-on was removed because no current claim uses `ebs.csi.aws.com`. Install the traditional driver only if you intentionally use standard EBS storage on standard nodes and have configured its IAM identity correctly.

## 6. Create ECR repositories and push all backend images

The project has 15 image repositories. The current image URIs use ECR in `eu-north-1`; cross-region pulls work but are slower than keeping ECR and EKS in the same region. To follow the current project manifests, create/use these repositories in **eu-north-1**. In the AWS Console, choose **Amazon ECR > Private registry > Repositories > Create repository**, create each name from the list below, set tag mutability to **Immutable**, and leave encryption as **AES-256**. The CLI loop is faster for creating all 15:

```powershell
$repoNames = @(
  'luvora-api-gateway','luvora-audit-service','luvora-auth-service',
  'luvora-cart-service','luvora-configuration-service','luvora-delivery-service',
  'luvora-inventory-service','luvora-notification-service','luvora-order-service',
  'luvora-payment-service','luvora-product-service','luvora-promotion-service',
  'luvora-reporting-service','luvora-review-service','luvora-support-service'
)
foreach ($repoName in $repoNames) {
  aws ecr describe-repositories --repository-names $repoName --region eu-north-1 *> $null
  if ($LASTEXITCODE -ne 0) {
    aws ecr create-repository --repository-name $repoName --image-tag-mutability IMMUTABLE --encryption-configuration encryptionType=AES256 --region eu-north-1
    if ($LASTEXITCODE -ne 0) { throw "Could not create ECR repository $repoName" }
  }
}
```

Log Docker into ECR:

```powershell
$accountId = aws sts get-caller-identity --query Account --output text
$registry = "$accountId.dkr.ecr.eu-north-1.amazonaws.com"
aws ecr get-login-password --region eu-north-1 |
  docker login --username AWS --password-stdin $registry
```

Docker should report `Login Succeeded`. ECR login tokens expire; repeat this command before a later push if Docker reports an authentication error. If the AWS CLI call returns `ExpiredToken`, reauthenticate with `aws login` (or `aws sso login --profile luvora`) first.

The repository includes a deployment script that builds all 15 Dockerfiles from `services`, pushes the images, applies the Kubernetes manifests, updates the image tags, and waits for rollouts. The script defaults to a fresh timestamp tag. First create the Kubernetes secrets and database/storage dependencies in the next sections; then run:

```powershell
$context = kubectl config current-context
$tag = Get-Date -Format 'yyyyMMdd-HHmmss'
$accountId = aws sts get-caller-identity --query Account --output text
$registry = "$accountId.dkr.ecr.eu-north-1.amazonaws.com"
.\infrastructure\kubernetes\update.ps1 `
  -Context $context `
  -Registry $registry `
  -Tag $tag
```

`update.ps1` removes an old `frontend` Deployment/Service if present. It does not deploy a frontend. If using another AWS account or ECR region, update the manifest image URIs and the `-Registry` argument accordingly.

### Exact service-to-image map and manual build/push code

`backend/services.yaml` defines the 14 services below (each has one Deployment and one ClusterIP Service). `gateway/deployment.yaml` and `gateway/service.yaml` define the API Gateway. The `Database` column is the database connection key/database name used by that app; all databases are in the same PostgreSQL pod but kept separate. RabbitMQ users are services with `RabbitMQ` in the final column.

| Service | Docker project / Dockerfile folder | ECR repository | Kubernetes Deployment | Database | RabbitMQ |
|---|---|---|---|---|---|
| API Gateway | `ApiGateWay` | `luvora-api-gateway` | `api-gateway` | — | — |
| Audit | `AuditService` | `luvora-audit-service` | `audit-service` | `audit_db` | — |
| Auth | `AuthService` | `luvora-auth-service` | `auth-service` | `auth_db` | — |
| Cart | `CartService` | `luvora-cart-service` | `cart-service` | `cart_db` | — |
| Configuration | `ConfigurationService` | `luvora-configuration-service` | `configuration-service` | `configuration_db` | — |
| Delivery | `DeliveryService` | `luvora-delivery-service` | `delivery-service` | `delivery_db` | — |
| Inventory | `InventoryService` | `luvora-inventory-service` | `inventory-service` | `inventory_db` | Yes |
| Notification | `NotificationService` | `luvora-notification-service` | `notification-service` | `notification_db` | Yes |
| Order | `OrderService` | `luvora-order-service` | `order-service` | `order_db` | Yes |
| Payment | `PaymentService` | `luvora-payment-service` | `payment-service` | `payment_db` | Yes |
| Product | `ProductService` | `luvora-product-service` | `product-service` | `product_db` | — |
| Promotion | `PromotionService` | `luvora-promotion-service` | `promotion-service` | `promotion_db` | — |
| Reporting | `ReportingService` | `luvora-reporting-service` | `reporting-service` | `reporting_db` | — |
| Review | `ReviewService` | `luvora-review-service` | `review-service` | `review_db` | — |
| Support | `SupportService` | `luvora-support-service` | `support-service` | `support_db` | — |

If deploying manually instead of using `update.ps1`, this block builds and pushes every image from the correct Dockerfile, then sets the image and waits for each Deployment. Run it from the project root after Docker has logged into ECR. The tag is unique so Kubernetes doesn't reuse an old cached `:1.0` image:

```powershell
$accountId = aws sts get-caller-identity --query Account --output text
$registry = "$accountId.dkr.ecr.eu-north-1.amazonaws.com"
$tag = Get-Date -Format 'yyyyMMdd-HHmmss'
$services = @(
  @{ Project='ApiGateWay'; Image='luvora-api-gateway'; Deployment='api-gateway' },
  @{ Project='AuditService'; Image='luvora-audit-service'; Deployment='audit-service' },
  @{ Project='AuthService'; Image='luvora-auth-service'; Deployment='auth-service' },
  @{ Project='CartService'; Image='luvora-cart-service'; Deployment='cart-service' },
  @{ Project='ConfigurationService'; Image='luvora-configuration-service'; Deployment='configuration-service' },
  @{ Project='DeliveryService'; Image='luvora-delivery-service'; Deployment='delivery-service' },
  @{ Project='InventoryService'; Image='luvora-inventory-service'; Deployment='inventory-service' },
  @{ Project='NotificationService'; Image='luvora-notification-service'; Deployment='notification-service' },
  @{ Project='OrderService'; Image='luvora-order-service'; Deployment='order-service' },
  @{ Project='PaymentService'; Image='luvora-payment-service'; Deployment='payment-service' },
  @{ Project='ProductService'; Image='luvora-product-service'; Deployment='product-service' },
  @{ Project='PromotionService'; Image='luvora-promotion-service'; Deployment='promotion-service' },
  @{ Project='ReportingService'; Image='luvora-reporting-service'; Deployment='reporting-service' },
  @{ Project='ReviewService'; Image='luvora-review-service'; Deployment='review-service' },
  @{ Project='SupportService'; Image='luvora-support-service'; Deployment='support-service' }
)

foreach ($service in $services) {
  $localImage = "$($service.Image):$tag"
  $remoteImage = "$registry/$($service.Image):$tag"
  docker build -t $localImage -f "services/$($service.Project)/Dockerfile" services
  if ($LASTEXITCODE -ne 0) { throw "Docker build failed for $($service.Project)" }
  docker tag $localImage $remoteImage
  if ($LASTEXITCODE -ne 0) { throw "Docker tag failed for $($service.Project)" }
  docker push $remoteImage
  if ($LASTEXITCODE -ne 0) { throw "ECR push failed for $($service.Image)" }
}

foreach ($service in $services) {
  $remoteImage = "$registry/$($service.Image):$tag"
  kubectl set image "deployment/$($service.Deployment)" `
    "$($service.Deployment)=$remoteImage" -n luvora
  if ($LASTEXITCODE -ne 0) { throw "Image update failed for $($service.Deployment)" }
  kubectl rollout status "deployment/$($service.Deployment)" -n luvora --timeout=300s
  if ($LASTEXITCODE -ne 0) { throw "Rollout failed for $($service.Deployment)" }
}

Write-Host "Published and deployed tag $tag to all 15 workloads."
```

The manual block assumes the namespace, Secrets, PVCs, database/message broker, Deployment/Service manifests, and network policy manifests are already applied as described below. It only rebuilds, pushes, and updates the 15 app images. For first-time deployment, use the complete `update.ps1` workflow in Section 9 after setting up the dependencies.

## 7. Create namespace, Auto Mode StorageClass, and Kubernetes secrets

Apply the namespace and Auto Mode StorageClass before the PVCs:

```powershell
kubectl apply -f infrastructure/kubernetes/base/namespace.yaml
kubectl apply -f infrastructure/kubernetes/database/auto-ebs-gp3.yaml
kubectl get storageclass
```

The active claims must use `storageClassName: auto-ebs-gp3`, whose provisioner is `ebs.csi.eks.amazonaws.com`. The old `ebs-gp3` class uses `ebs.csi.aws.com` and is not used by this Auto Mode deployment.

Create strong unique credentials locally; never copy Docker Compose development passwords into AWS. The following PowerShell creates random URL-safe keys and the exact secret names/keys referenced by the manifests. It creates separate PostgreSQL, RabbitMQ, JWT, and audit secrets and connection strings for all 14 databases:

Run this once for a fresh cluster. If reusing an existing PostgreSQL PVC, do not generate a different `$pgPassword` and replace the secrets unless you also rotate the PostgreSQL user's password; otherwise the application connection strings and the persisted database password will disagree.

```powershell
function New-RandomSecret([int]$ByteCount = 48) {
  $bytes = New-Object byte[] $ByteCount
  $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  try { $rng.GetBytes($bytes) }
  finally { $rng.Dispose() }
  return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_')
}

$pgPassword = New-RandomSecret
$rabbitPassword = New-RandomSecret
$jwtKey = New-RandomSecret 64
$auditKey = New-RandomSecret

kubectl create secret generic postgres-secret -n luvora `
  --from-literal=POSTGRES_USER=luvora `
  "--from-literal=POSTGRES_PASSWORD=$pgPassword" `
  --from-literal=POSTGRES_DB=postgres `
  --dry-run=client -o yaml | kubectl apply -f -

kubectl create secret generic rabbitmq-secret -n luvora `
  --from-literal=RABBITMQ_DEFAULT_USER=luvora `
  "--from-literal=RABBITMQ_DEFAULT_PASS=$rabbitPassword" `
  --dry-run=client -o yaml | kubectl apply -f -

$databaseNames = [ordered]@{
  AuthDatabase='auth_db'; ProductDatabase='product_db'; InventoryDatabase='inventory_db'
  CartDatabase='cart_db'; OrderDatabase='order_db'; PaymentDatabase='payment_db'
  DeliveryDatabase='delivery_db'; NotificationDatabase='notification_db'
  ReviewDatabase='review_db'; PromotionDatabase='promotion_db'; SupportDatabase='support_db'
  ReportingDatabase='reporting_db'; ConfigurationDatabase='configuration_db'; AuditDatabase='audit_db'
}
$dbArgs = @()
foreach ($entry in $databaseNames.GetEnumerator()) {
  $connection = "Host=postgres;Port=5432;Database=$($entry.Value);Username=luvora;Password=$pgPassword"
  $dbArgs += "--from-literal=ConnectionStrings__$($entry.Key)=$connection"
}
kubectl create secret generic luvora-db-secret -n luvora @dbArgs --dry-run=client -o yaml |
  kubectl apply -f -

kubectl create secret generic luvora-app-secret -n luvora `
  "--from-literal=JWT_KEY=$jwtKey" `
  "--from-literal=AUDIT_INGEST_KEY=$auditKey" `
  --dry-run=client -o yaml | kubectl apply -f -

Remove-Variable pgPassword, rabbitPassword, jwtKey, auditKey, dbArgs, connection
```

Do not run `kubectl get secret -o yaml` or paste secret values into chat/logs. For production, store secrets in AWS Secrets Manager and configure an approved external-secrets integration rather than keeping long-lived credentials only in Kubernetes Secrets.

## 8. Start PostgreSQL and RabbitMQ, then create service databases

Apply the database and messaging resources. The PostgreSQL manifest sets `PGDATA` under a subdirectory; keep this because EBS filesystems may contain a `lost+found` directory, which otherwise makes PostgreSQL `initdb` fail.

```powershell
kubectl apply -f infrastructure/kubernetes/database/postgres-pvc.yaml
kubectl apply -f infrastructure/kubernetes/database/postgres-deployment.yaml
kubectl apply -f infrastructure/kubernetes/database/postgres-service.yaml
kubectl apply -f infrastructure/kubernetes/backend/product-images-pvc.yaml
kubectl apply -f infrastructure/kubernetes/messaging/rabbitmq-deployment.yaml
kubectl apply -f infrastructure/kubernetes/messaging/rabbitmq-service.yaml

kubectl rollout status deployment/postgres -n luvora --timeout=300s
kubectl rollout status deployment/rabbitmq -n luvora --timeout=300s
kubectl get pvc -n luvora
```

Both PVCs should become `Bound`. The Postgres Deployment initializes only its configured default database (`postgres`); create each service database once:

```powershell
$databaseNames = @(
  'auth_db','product_db','inventory_db','cart_db','order_db','payment_db',
  'delivery_db','notification_db','review_db','promotion_db','support_db',
  'reporting_db','configuration_db','audit_db'
)
foreach ($dbName in $databaseNames) {
  $exists = kubectl exec deployment/postgres -n luvora -- `
    psql -U luvora -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$dbName'"
  if (($exists -join '').Trim() -ne '1') {
    kubectl exec deployment/postgres -n luvora -- `
      createdb -U luvora --maintenance-db=postgres $dbName
    if ($LASTEXITCODE -ne 0) { throw "Could not create database $dbName" }
  }
}
```

Do not delete `postgres-pvc` or `product-images-pvc` to fix a startup issue; inspect the pod and claim events instead.

## 9. Build, push, and deploy the backend

With AWS ECR login complete, secrets present, Postgres/RabbitMQ ready, and the 14 databases created, run the `update.ps1` command from Section 6. It builds from each service Dockerfile, pushes tag `1.0`, applies backend/gateway/network manifests, sets the image on each deployment, removes any legacy frontend workload, and waits for all 15 application rollouts.

If images are built/pushed separately, run the script with `-SkipBuild` and pass the exact registry host/tag used for those images:

```powershell
$tag = 'release-20261031-1' # use the exact tag that was pushed to ECR
$accountId = aws sts get-caller-identity --query Account --output text
$registry = "$accountId.dkr.ecr.eu-north-1.amazonaws.com"
.\infrastructure\kubernetes\update.ps1 `
  -Context (kubectl config current-context) `
  -Registry $registry `
  -Tag $tag `
  -SkipBuild
```

## 10. Verify the deployment

```powershell
kubectl get nodes
kubectl get deployments,pods,services,pvc -n luvora
kubectl get pods -n kube-system | Select-String 'coredns|aws-node|kube-proxy'
kubectl get endpointslices -n luvora
```

Expected: all nodes `Ready`; each deployment `1/1`; each app pod `1/1 Running`; both PVCs `Bound`; service EndpointSlices have pod IPs; and in-cluster names such as `postgres` resolve. For mixed Auto Mode and standard-node clusters, confirm CoreDNS pods and `kube-dns` are present. Auto Mode-only clusters can use AWS-managed cluster DNS without a customer-managed CoreDNS add-on. Verify AWS add-ons too:

In this repository, Kubernetes readiness probes are configured for the API Gateway, PostgreSQL, and RabbitMQ. Several service manifests do not define probes, so Kubernetes can show their containers `Ready` simply because the process started. Also inspect application logs and make a real API request before treating the whole application flow as verified.

```powershell
aws eks list-addons --cluster-name luvora-cluster --region sa-east-1
```

The failed traditional `aws-ebs-csi-driver` add-on should not be in the list for this Auto Mode storage setup. Check add-ons and DNS behavior against the cluster mode; mixed standard/Auto Mode clusters like the one in this chat needed the CoreDNS add-on restored.

The API Gateway service is intentionally `ClusterIP`, so it has no public IP or DNS name. To test it from your workstation, open a PowerShell window and keep this command running:

```powershell
kubectl port-forward service/api-gateway 5000:8080 -n luvora
```

Then access the API at `http://localhost:5000`. This is a local tunnel, not public production access. Optional public HTTPS access through an Auto Mode ALB is described below.

### Optional: expose the API Gateway publicly over HTTPS with Auto Mode

Only do this when the API is ready for internet traffic. Do not expose login or customer APIs over plain HTTP. This uses the EKS Auto Mode built-in ALB integration; it does not install the separate AWS Load Balancer Controller.

Prerequisites:

1. EKS Auto Mode load balancing is enabled and the cluster role has `AmazonEKSLoadBalancingPolicy`.
2. Public subnets have a route to an Internet Gateway and are tagged `kubernetes.io/role/elb=1` for ALB discovery.
3. Request/validate an ACM public certificate in `sa-east-1` for the DNS hostname you will use, such as `api.example.com`.
4. Have a Route 53 hosted zone (or other DNS provider) for that hostname.

Create `infrastructure/kubernetes/gateway/public-ingress.yaml`, replace the example hostname and certificate ARN, then apply it:

```powershell
@'
apiVersion: eks.amazonaws.com/v1
kind: IngressClassParams
metadata:
  name: luvora-public
spec:
  scheme: internet-facing
  targetType: ip
  certificateARNs:
    - arn:aws:acm:sa-east-1:<ACCOUNT_ID>:certificate/REPLACE_WITH_CERTIFICATE_ID
---
apiVersion: networking.k8s.io/v1
kind: IngressClass
metadata:
  name: luvora-public
spec:
  controller: eks.amazonaws.com/alb
  parameters:
    apiGroup: eks.amazonaws.com
    kind: IngressClassParams
    name: luvora-public
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: api-gateway-public
  namespace: luvora
  annotations:
    alb.ingress.kubernetes.io/listen-ports: '[{"HTTP":80},{"HTTPS":443}]'
    alb.ingress.kubernetes.io/ssl-redirect: '443'
spec:
  ingressClassName: luvora-public
  rules:
    - host: api.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: api-gateway
                port:
                  number: 8080
'@ | Set-Content -Encoding ascii infrastructure/kubernetes/gateway/public-ingress.yaml

kubectl apply -f infrastructure/kubernetes/gateway/public-ingress.yaml
kubectl get ingress -n luvora --watch
```

Wait for the `ADDRESS` field to receive an ALB DNS name. In Route 53 create an Alias A record for `api.example.com` pointing to that ALB; with another DNS provider create a CNAME. Update the frontend's API base URL to `https://api.example.com`. The ALB is billable; deleting the Kubernetes Ingress asks Auto Mode to remove the associated ALB. See AWS's [Auto Mode ALB Ingress guide](https://docs.aws.amazon.com/eks/latest/userguide/auto-configure-alb.html) for current field support and troubleshooting.

## 11. Troubleshooting guide

| Symptom | Likely cause | Fix/check |
|---|---|---|
| `ResourceNotFoundException` on `update-kubeconfig` | Wrong region or cluster name | Use `sa-east-1` and `luvora-cluster`; run `aws eks list-clusters --region sa-east-1`. |
| Node group says instance not Free Tier eligible | Selected instance type is unavailable for the account's Free Tier | Pick a supported On-Demand instance type; do not rely on Free Tier eligibility. |
| `NodeCreationFailure` / instances fail to join | Wrong node IAM role, subnet routing/IP assignment, or insufficient capacity | Verify node IAM role and trust; use private subnets with NAT, or ensure public subnets assign public IPv4; check node-group health/events. |
| `Too many pods` / pods stay Pending | Too few/small worker nodes for the number of services | Enable/provision adequate Auto Mode general-purpose capacity; don't attempt all workloads on one/two tiny instances. |
| `ImagePullBackOff`, pulling `docker.io/library/luvora-...` | Manifest uses a local Docker tag or ECR repo/tag absent | Build/push all images to ECR, verify the URI/tag under `<ACCOUNT_ID>.dkr.ecr.<ECR_REGION>.amazonaws.com`, and set each deployment to that ECR URI. Confirm node role can pull ECR. |
| `kubectl get pods,pvc -w` says only one type allowed | `kubectl get -w` accepts one resource type in this usage | Run `kubectl get pods -n luvora -w` and `kubectl get pvc -n luvora -w` in separate commands. |
| PVC `Pending`, no StorageClass set | Claim omitted or names an unavailable class | Use `storageClassName: auto-ebs-gp3` and apply `auto-ebs-gp3.yaml`; inspect `kubectl describe pvc <claim> -n luvora`. |
| PVC says `WaitForPodScheduled` | Normal with `WaitForFirstConsumer` binding | Check the consuming pod's scheduling events and available Auto Mode nodes; wait for scheduling before diagnosing the volume. |
| Postgres logs `lost+found` / `initdb` says data directory not empty | EBS root has filesystem metadata | Set `PGDATA=/var/lib/postgresql/data/pgdata` as in `postgres-deployment.yaml`. |
| `endpoints postgres not found` | Postgres Service wasn't applied | `kubectl apply -f infrastructure/kubernetes/database/postgres-service.yaml`; check `kubectl get svc,endpointslices -n luvora`. |
| Apps report database does not exist | Postgres container creates only `POSTGRES_DB` | Create all 14 named databases in Section 8; verify Postgres is Ready first. |
| App logs show DNS lookup errors for `postgres` or services | CoreDNS add-on/service absent or unhealthy | Check `kubectl get pods,svc -n kube-system`; ensure `kube-dns` exists at the cluster DNS IP and restart the affected deployment after CoreDNS is healthy. |
| EBS CSI add-on `CREATE_FAILED`, credential timeout | Traditional driver has no working IAM identity; it is not used by Auto Mode PVCs here | Keep claims on `auto-ebs-gp3` and remove/leave out the traditional add-on. Do not recreate it for the current claims. |
| Pod says missing required Kubernetes Secret/key | Secret name/key doesn't match Deployment | Compare manifest `secretKeyRef` names and keys with Section 7; recreate the correct secret and restart the affected deployment. |
| Pod `CrashLoopBackOff` / `Error` | App startup failure | `kubectl describe pod <pod> -n luvora`, `kubectl logs <pod> -n luvora --previous`, and inspect events; fix the underlying config/dependency before restarting. |

Useful diagnostics:

```powershell
kubectl describe pod <pod-name> -n luvora
kubectl logs deployment/<deployment-name> -n luvora --tail=100
kubectl logs <pod-name> -n luvora --previous
kubectl get events -n luvora --sort-by=.lastTimestamp
kubectl rollout status deployment/<deployment-name> -n luvora --timeout=180s
kubectl describe pvc <claim-name> -n luvora
```

## 12. Rebuild/push a changed service later

After editing a service, rebuild and push its image with a new tag, then update only that Deployment. Example for AuthService:

```powershell
$tag = 'release2'
$accountId = aws sts get-caller-identity --query Account --output text
$registry = "$accountId.dkr.ecr.eu-north-1.amazonaws.com"
docker build -t "luvora-auth-service:$tag" -f services/AuthService/Dockerfile services
docker tag "luvora-auth-service:$tag" "$registry/luvora-auth-service:$tag"
docker push "$registry/luvora-auth-service:$tag"
kubectl set image deployment/auth-service `
  "auth-service=$registry/luvora-auth-service:$tag" -n luvora
kubectl rollout status deployment/auth-service -n luvora --timeout=180s
```

Use a new immutable-style release tag for repeat deployments to avoid stale node caches; then verify the rollout and pod logs.

## AWS documentation references

- [Sign in through the AWS CLI with `aws login`](https://docs.aws.amazon.com/signin/latest/userguide/command-line-sign-in.html)
- [Configure IAM Identity Center for the AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sso.html)
- [AWS CLI local-development sign-in permissions](https://docs.aws.amazon.com/aws-managed-policy/latest/reference/SignInLocalDevelopmentAccess.html)
- [Create an EKS Auto Mode cluster](https://docs.aws.amazon.com/eks/latest/userguide/create-cluster-auto.html)
- [EKS Auto Mode cluster IAM role and required policies](https://docs.aws.amazon.com/eks/latest/userguide/auto-cluster-iam-role.html)
- [EKS Auto Mode node IAM role and required policies](https://docs.aws.amazon.com/eks/latest/userguide/auto-create-node-role.html)
- [EKS Auto Mode StorageClass](https://docs.aws.amazon.com/eks/latest/userguide/create-storage-class.html)
- [EBS CSI and Auto Mode storage considerations](https://docs.aws.amazon.com/eks/latest/userguide/ebs-csi.html)
- [EKS add-ons](https://docs.aws.amazon.com/eks/latest/userguide/eks-add-ons.html)
- [EKS Auto Mode Application Load Balancers](https://docs.aws.amazon.com/eks/latest/userguide/auto-configure-alb.html)
- [Push images to Amazon ECR](https://docs.aws.amazon.com/AmazonECR/latest/userguide/getting-started-cli.html)
