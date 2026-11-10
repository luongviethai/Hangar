# Hangar — Kho build mobile nội bộ

Project 2, Giai đoạn 2 (Tuần 5-8) của lộ trình tự học backend .NET. Upload APK/IPA, lưu trên MinIO, chia sẻ qua link cài đặt có hạn dùng/giới hạn lượt tải. Hệ thống mở — không có đăng nhập.

Nghiệp vụ: [docs/2-tai-lieu-nghiep-vu.md](docs/2-tai-lieu-nghiep-vu.md) · Kế hoạch từng ngày: [docs/1-ke-hoach-chi-tiet.md](docs/1-ke-hoach-chi-tiet.md)

## Tech stack
- .NET 10, ASP.NET Core Web API
- PostgreSQL 16 (Alpine) — cổng host `5436` (CRM đã chiếm `5435`)
- MinIO (image Chainguard) — API `9002`, Console `9003`
- Clean Architecture: Domain / Application / Infrastructure / Api

## Kiến trúc

```
src/
  Hangar.Domain          # Entity thuần, không phụ thuộc layer nào khác
  Hangar.Application     # Interface, DTO, Service (use case), Validator — phụ thuộc Domain
  Hangar.Infrastructure  # EF Core, MinIO, các implementation — phụ thuộc Application
  Hangar.Api             # Controllers, Program.cs (composition root)
```

## Chạy project

```bash
docker compose up -d
cd src/Hangar.Api
dotnet run
```

## Trạng thái

Làm lại từ đầu ngày 2026-11-09 — solution trống, chưa có entity/migration/tính năng nào. Bản cũ (có auth/Organization) ở tag git `ban-cu-auth-org`. Theo dõi tiến độ ở `docs/1-ke-hoach-chi-tiet.md`.
