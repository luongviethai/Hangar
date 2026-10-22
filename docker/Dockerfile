# Build context là thư mục gốc project (Pharse2/Hangar):
#   docker build -f docker/Dockerfile .
# Khi gắn vào docker-compose.yml: context: . , dockerfile: docker/Dockerfile

# Stage 1: build + publish bằng SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy riêng các .csproj trước để tận dụng layer cache của Docker:
# chỉ khi file .csproj đổi (thêm/xoá package) thì bước restore mới chạy lại.
COPY src/Hangar.Api/Hangar.Api.csproj src/Hangar.Api/
COPY src/Hangar.Application/Hangar.Application.csproj src/Hangar.Application/
COPY src/Hangar.Domain/Hangar.Domain.csproj src/Hangar.Domain/
COPY src/Hangar.Infrastructure/Hangar.Infrastructure.csproj src/Hangar.Infrastructure/
RUN dotnet restore src/Hangar.Api/Hangar.Api.csproj

COPY src/ src/
RUN dotnet publish src/Hangar.Api/Hangar.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: chạy bằng runtime image (nhỏ hơn SDK image rất nhiều, không có công cụ build)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Hangar.Api.dll"]
