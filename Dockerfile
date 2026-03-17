# ── Etapa 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copiar archivos de solución y configuración
COPY Ferreteria.sln .
COPY NuGet.config .

# Copiar solo los .csproj primero (cache de restore)
COPY ABST/ABST.csproj           ABST/
COPY BE/BE.csproj               BE/
COPY BLL/BLL.csproj             BLL/
COPY DAL/DAL.csproj             DAL/
COPY L/L.csproj                 L/
COPY FerreteriaDB/FerreteriaDB.csproj FerreteriaDB/

# Restaurar dependencias
RUN dotnet restore

# Copiar todo el código fuente
COPY . .

# Publicar en modo Release
RUN dotnet publish FerreteriaDB/FerreteriaDB.csproj -c Release -o /app/publish --no-restore

# ── Etapa 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render inyecta la variable PORT; ASP.NET Core la usa con ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "FerreteriaDB.dll"]
