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
