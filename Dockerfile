# Multi-stage build for QRMenu (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyalarını kopyala ve restore et (Docker cache layer optimizasyonu)
COPY ["QRMenu.sln", "./"]
COPY ["src/QRMenu.Core/QRMenu.Core.csproj", "src/QRMenu.Core/"]
COPY ["src/QRMenu.Data/QRMenu.Data.csproj", "src/QRMenu.Data/"]
COPY ["src/QRMenu.Web/QRMenu.Web.csproj", "src/QRMenu.Web/"]
COPY ["tests/QRMenu.Tests/QRMenu.Tests.csproj", "tests/QRMenu.Tests/"]
COPY ["tests/QRMenu.IntegrationTests/QRMenu.IntegrationTests.csproj", "tests/QRMenu.IntegrationTests/"]

RUN dotnet restore "QRMenu.sln"

# Tüm kaynak kodları kopyala ve Release olarak derle
COPY . .
WORKDIR "/src/src/QRMenu.Web"
RUN dotnet publish "QRMenu.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime aşaması: Güvenli, minimal ASP.NET 8 imajı
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Healthcheck için curl kur ve paket önbelleğini temizle
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=app:app /app/publish .

# Güvenlik & İzinler: Uploads dizinini hazırla ve app kullanıcısına yetki ver
RUN mkdir -p /app/wwwroot/uploads/urunler && chown -R app:app /app/wwwroot/uploads

# Non-root kullanıcı ile çalıştır
USER app

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "QRMenu.Web.dll"]
