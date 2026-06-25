# syntax=docker/dockerfile:1
# Finnid HIS API — container image for Azure Container Apps.
# Serves the Web API + wireframe (wwwroot) and carries the provisioning templates
# + control-plane migrations needed at runtime.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore as a cached layer: copy only the csprojs HIS.Api needs, then restore.
COPY src/HIS.Api/HIS.Api.csproj                       src/HIS.Api/
COPY src/HIS.Application/HIS.Application.csproj        src/HIS.Application/
COPY src/HIS.Infrastructure/HIS.Infrastructure.csproj src/HIS.Infrastructure/
COPY src/HIS.Domain/HIS.Domain.csproj                 src/HIS.Domain/
COPY src/HIS.Shared/HIS.Shared.csproj                 src/HIS.Shared/
RUN dotnet restore src/HIS.Api/HIS.Api.csproj

# Build + publish the API.
COPY . .
RUN dotnet publish src/HIS.Api/HIS.Api.csproj -c Release -o /app/publish -p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Runtime data that is NOT part of the publish output:
#  - db/tenant-template/{master,fy}: applied by the provisioning engine on onboarding
#  - db/platform/*.sql: control-plane migrations (kept for reference / in-image runs)
COPY --from=build /src/db ./db

# Absolute template root so the engine doesn't use the repo-relative dev fallback.
# Listen on 8080 (Container Apps target port); HTTPS terminates at ingress (port 443).
ENV Provisioning__TemplateRoot=/app/db/tenant-template \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_HTTPS_PORT=443 \
    DOTNET_gcServer=1

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HIS.Api.dll"]
