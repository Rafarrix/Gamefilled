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
RUN dotnet ef migrations bundle \
    --project Gamefilled.csproj \
    --startup-project Gamefilled.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-build \
    --self-contained false \
    --output /app/migrations/efbundle

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
