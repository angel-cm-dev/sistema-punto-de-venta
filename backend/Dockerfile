# syntax=docker/dockerfile:1

# ---------- Etapa 1: compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Se copian primero solo los .csproj para aprovechar la caché de capas en el restore:
# si solo cambia código fuente, no se vuelven a descargar los paquetes NuGet.
COPY PuntoVenta.sln ./
COPY src/PuntoVenta.Domain/PuntoVenta.Domain.csproj src/PuntoVenta.Domain/
COPY src/PuntoVenta.Application/PuntoVenta.Application.csproj src/PuntoVenta.Application/
COPY src/PuntoVenta.Infrastructure/PuntoVenta.Infrastructure.csproj src/PuntoVenta.Infrastructure/
COPY src/PuntoVenta.Api/PuntoVenta.Api.csproj src/PuntoVenta.Api/
RUN dotnet restore src/PuntoVenta.Api/PuntoVenta.Api.csproj

COPY src/ src/
RUN dotnet publish src/PuntoVenta.Api/PuntoVenta.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Etapa 2: runtime mínimo ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
COPY --from=build --chown=app:app /app/publish .

# La imagen oficial trae el usuario sin privilegios "app" (UID 1654): nunca se ejecuta como root.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PuntoVenta.Api.dll"]
