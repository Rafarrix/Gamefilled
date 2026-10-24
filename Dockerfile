# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Gamefilled.csproj ./
COPY .config/dotnet-tools.json ./.config/dotnet-tools.json
RUN dotnet restore Gamefilled.csproj

COPY . .
RUN dotnet publish Gamefilled.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM build AS migration-bundle
ARG BUILD_CONFIGURATION=Release
ENV ASPNETCORE_ENVIRONMENT=ContainerBuild \
    ConnectionStrings__DefaultConnection="Server=127.0.0.1,1;Database=GamefilledBundleBuild;User Id=sa;Password=BuildOnly_123!;Encrypt=False;Connect Timeout=1"

RUN dotnet tool restore
RUN mkdir -p /app/migrations && \
    dotnet tool run dotnet-ef migrations bundle \
      --project Gamefilled.csproj \
      --startup-project Gamefilled.csproj \
      --configuration ${BUILD_CONFIGURATION} \
      --context AppDbContext \
      --no-build \
      --output /app/migrations/efbundle

# Privileged provisioner belongs to a separate one-shot image, never the web.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS db-provisioner-build
WORKDIR /src
COPY tools/Gamefilled.DbProvisioner/Gamefilled.DbProvisioner.csproj ./tools/Gamefilled.DbProvisioner/
RUN dotnet restore tools/Gamefilled.DbProvisioner/Gamefilled.DbProvisioner.csproj
COPY tools/Gamefilled.DbProvisioner/Program.cs ./tools/Gamefilled.DbProvisioner/
RUN dotnet publish tools/Gamefilled.DbProvisioner/Gamefilled.DbProvisioner.csproj \
    -c Release -o /app/db-provisioner --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS db-provisioner
WORKDIR /app
COPY --from=db-provisioner-build --chown=app:app /app/db-provisioner ./
USER app
ENTRYPOINT ["dotnet", "Gamefilled.DbProvisioner.dll"]

# Setup-only, read-only audit for mapping old Gamefilled data to Identity.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS migration-audit-build
WORKDIR /src
COPY tools/Gamefilled.MigrationAudit/Gamefilled.MigrationAudit.csproj ./tools/Gamefilled.MigrationAudit/
RUN dotnet restore tools/Gamefilled.MigrationAudit/Gamefilled.MigrationAudit.csproj
COPY tools/Gamefilled.MigrationAudit/Program.cs ./tools/Gamefilled.MigrationAudit/
RUN dotnet publish tools/Gamefilled.MigrationAudit/Gamefilled.MigrationAudit.csproj \
    -c Release -o /app/migration-audit --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migration-audit
WORKDIR /app
COPY --from=migration-audit-build --chown=app:app /app/migration-audit ./
USER app
ENTRYPOINT ["dotnet", "Gamefilled.MigrationAudit.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app

COPY --from=migration-bundle /app/migrations/efbundle ./efbundle
COPY appsettings.example.json ./appsettings.json
RUN chmod 0555 /app/efbundle && chown -R app:app /app

USER app
ENTRYPOINT ["./efbundle", "--no-color"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build --chown=app:app /app/publish .
USER app

ENTRYPOINT ["dotnet", "Gamefilled.dll"]
