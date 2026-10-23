# Cheslax API — multi-stage image for a 2 vCPU / 4 GB Ubuntu VPS.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json Cheslax.slnx ./
COPY src/Cheslax.Domain/Cheslax.Domain.csproj src/Cheslax.Domain/
COPY src/Cheslax.GameEngine/Cheslax.GameEngine.csproj src/Cheslax.GameEngine/
COPY src/Cheslax.Application/Cheslax.Application.csproj src/Cheslax.Application/
COPY src/Cheslax.Infrastructure/Cheslax.Infrastructure.csproj src/Cheslax.Infrastructure/
COPY src/Cheslax.Api/Cheslax.Api.csproj src/Cheslax.Api/
RUN dotnet restore src/Cheslax.Api/Cheslax.Api.csproj

COPY src/ src/
RUN dotnet publish src/Cheslax.Api/Cheslax.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Cheslax.Api.dll"]
