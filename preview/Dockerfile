# Build ONLY the isolated modernized Gamefilled V2. Does not change the
# running post-PAP V2 production image or touch the legacy database.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src
COPY . .
RUN dotnet restore v2-modernization/Gamefilled.V2/Gamefilled.V2.csproj
RUN dotnet publish v2-modernization/Gamefilled.V2/Gamefilled.V2.csproj \
      -c Release -o /publish --no-restore /p:UseAppHost=false

# Migration runner is invoked manually on the disposable preview database.
FROM builder AS migrator
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "tool", "run", "dotnet-ef", "database", "update", \
    "--project", "v2-modernization/Gamefilled.V2/Gamefilled.V2.csproj", \
    "--startup-project", "v2-modernization/Gamefilled.V2/Gamefilled.V2.csproj", \
    "--context", "GamefilledIdentityDbContext", "--no-build", "--configuration", "Release"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
ARG GAMEFILLED_PREVIEW_REVISION=local
WORKDIR /app
COPY --from=builder --chown=app:app /publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Preview \
    Preview__Revision=${GAMEFILLED_PREVIEW_REVISION} \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gamefilled.V2.dll"]
