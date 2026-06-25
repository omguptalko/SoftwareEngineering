# Finnid HIS — Production Deployment (secrets, Key Vault, TLS)

The app is **config-driven and secret-free in source**. Production supplies every secret at
deploy time through one of two interchangeable mechanisms, both already wired in `Program.cs`:

1. **Environment variables** (works everywhere — containers, App Service, k8s).
2. **Azure Key Vault** (set `KeyVault__Uri`; identity via `DefaultAzureCredential`).

Key Vault, when configured, is added as a configuration source on startup. Env vars always
override. Nothing here is hardcoded — `appsettings.Production.json` holds only non-secret defaults.

## 1. Secrets to supply

| Config key | Env var (`Section__Key`) | Key Vault secret name (`:` → `--`) | Purpose |
|---|---|---|---|
| `ConnectionStrings:Platform` | `ConnectionStrings__Platform` | `ConnectionStrings--Platform` | Control-plane DB (`HIS_Platform`) |
| `Provisioning:BaseConnection` | `Provisioning__BaseConnection` | `Provisioning--BaseConnection` | Server used to CREATE per-tenant DBs (Azure SQL elastic pool admin) |
| `Jwt:SigningKey` | `Jwt__SigningKey` | `Jwt--SigningKey` | JWT HMAC key (≥ 32 bytes) |
| `Jwt:Issuer` / `Jwt:Audience` | `Jwt__Issuer` / `Jwt__Audience` | `Jwt--Issuer` / `Jwt--Audience` | Token realm |
| `Security:DataProtection:Key` | `Security__DataProtection__Key` | `Security--DataProtection--Key` | AES-256-GCM at-rest key — **base64 of 32 random bytes** |
| `Platform:Bootstrap:Password` | `Platform__Bootstrap__Password` | `Platform--Bootstrap--Password` | First superadmin password (rotate on first login) |
| `Tenancy:CommonDomain` | `Tenancy__CommonDomain` | — | e.g. `app.finnid.in` |

Generate the data-protection key:
```bash
python -c "import os,base64; print(base64.b64encode(os.urandom(32)).decode())"
```

Point the app at a vault:
```
KeyVault__Uri=https://finnid-his-kv.vault.azure.net/
```
Grant the app's managed identity **Key Vault Secrets User**. Locally, `az login` satisfies
`DefaultAzureCredential` without a vault — or just use env vars.

> Do **not** set `ConnectionStrings:His` in prod — the legacy single DB was retired in L1.8.5;
> all data access flows through the per-tenant master / per-FY databases.

## 2. TLS in transit

`Program.cs` enables HSTS + HTTPS redirection outside Development, and honors
`X-Forwarded-Proto`/`-For` so it works whether TLS terminates **at the app** or **at a proxy**.

- **TLS terminated at a proxy / ingress / App Service** (recommended): nothing else needed —
  the forwarded headers make redirects correct. Enforce HTTPS-only at the proxy.
- **TLS terminated at Kestrel**: provide a certificate via env (no secrets in source):
  ```
  Kestrel__Endpoints__Https__Url=https://0.0.0.0:443
  Kestrel__Certificates__Default__Path=/certs/tls.pfx
  Kestrel__Certificates__Default__Password=<from Key Vault>
  ```
  or load the cert from Key Vault / the OS store per your platform.

HSTS sends `Strict-Transport-Security` (1 year default). Confirm with
`curl -sI https://<host>/api/health | grep -i strict-transport-security`.

## 3. Database migrations

Apply the control-plane migrations once per environment (idempotent):
```bash
pwsh db/platform/run-platform-migrations.ps1 -Server "<azure-sql>" -Database "HIS_Platform"
```
Per-tenant databases are created + migrated **automatically** by the provisioning engine on
onboarding (`POST /api/platform/tenants/onboard`) and year-shift — no manual step.

## 4. Scale notes (from the L1.9 benchmarks)

- **Provisioning (L1.9.3):** onboarding is unattended and idempotent; a failed provision rolls
  back the DB it created (`Provisioning:RollbackOnFailure`). Run onboarding off the request hot
  path for large batches.
- **Connection routing (L1.9.4):** tenant resolution does one cached-able catalog lookup per
  request; for high fan-out, front `platform.DbCatalog`/`TenantDomain` with a short-TTL cache and
  use Azure SQL elastic pools (Decision D5) so per-tenant DBs share capacity.

## 5. CI/CD — GitHub Actions → Azure Container Apps

`Dockerfile` builds the API image (serves the wireframe + carries the provisioning templates;
listens on **8080**, runs **non-root**, `Provisioning__TemplateRoot=/app/db/tenant-template`).
`.github/workflows/deploy-containerapp.yml` runs: **build + test** (every push/PR) →
**`az acr build`** the image → **optional DB migrations** → **`az containerapp update`** →
**`/api/health` smoke check** (deploy only from `main`).

### One-time Azure setup
```bash
az group create -n finnid-his -l centralindia
az acr create  -n finnidhisacr -g finnid-his --sku Standard
az containerapp env create -n his-env -g finnid-his -l centralindia
# First revision (CI updates the image thereafter):
az containerapp create -n his-api -g finnid-his --environment his-env \
  --image mcr.microsoft.com/k8se/quickstart:latest \
  --target-port 8080 --ingress external --min-replicas 1 \
  --registry-server finnidhisacr.azurecr.io
```
Grant the Container App's managed identity **AcrPull** on the registry and **Key Vault Secrets
User** on the vault, then configure app settings as **Key Vault references** (or secrets):
`ConnectionStrings__Platform`, `Provisioning__BaseConnection`, `Jwt__SigningKey`,
`Security__DataProtection__Key`, `KeyVault__Uri`, `Tenancy__CommonDomain`,
`Platform__Bootstrap__Password`, and `ASPNETCORE_ENVIRONMENT=Production` (see §1).

### OIDC federated credential (no stored client secret)
Create an app registration / service principal with **Contributor** on the resource group and
**AcrPush** on the registry, then add a federated credential for this repo, e.g. subject
`repo:<org>/<repo>:ref:refs/heads/main` and `repo:<org>/<repo>:environment:production`.

### GitHub configuration
| Secrets | Variables |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | `ACR_NAME` (e.g. `finnidhisacr`) |
| `SQL_SERVER`, `SQL_DATABASE`, `SQL_USER`, `SQL_PASSWORD` (only if `RUN_DB_MIGRATIONS=true`) | `AZURE_RESOURCE_GROUP`, `CONTAINERAPP_NAME` |
| | `RUN_DB_MIGRATIONS` (`true` to apply control-plane migrations in the pipeline) |

App config/secrets live **on the Container App** (ideally Key Vault references) — the pipeline
ships code/images, not secrets.
