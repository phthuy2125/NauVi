# ==========================================
# Multi-stage Dockerfile cho ASP.NET Core 8 (NauVi)
# ==========================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

# Copy project file và restore dependencies riêng để tối ưu cache
COPY ["NauVi.csproj", "./"]
RUN dotnet restore "NauVi.csproj"

# Copy toàn bộ mã nguồn và biên dịch Release
COPY . .
RUN dotnet publish "NauVi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app

# Cài đặt curl phục vụ HEALTHCHECK và bảo trì
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy artifact đã build từ Stage 1
COPY --from=build /app/publish .

# Tạo thư mục lưu trữ ảnh tải lên và cấp quyền cho user không phải root (app)
RUN mkdir -p /app/uploads/posts \
    && chown -R app:app /app

# Chuyển sang user không phải root có sẵn trong aspnet runtime 8.0
USER app

# Cấu hình cổng chạy mặc định của .NET 8 container
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# HEALTHCHECK kiểm tra endpoint /health
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "NauVi.dll"]
