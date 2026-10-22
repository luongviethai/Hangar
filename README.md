# Hangar — Nền tảng phân phối bản build nội bộ

Project 2, Giai đoạn 2 (Tuần 4-10) của lộ trình tự học backend .NET. Xem bối cảnh nghiệp vụ ở [docs/2-tai-lieu-nghiep-vu.md](docs/2-tai-lieu-nghiep-vu.md), kế hoạch chi tiết từng ngày ở [docs/1-ke-hoach-chi-tiet.md](docs/1-ke-hoach-chi-tiet.md).

## Tech stack
- .NET 10, ASP.NET Core Web API
- PostgreSQL 16 (Alpine), chạy qua Docker Compose (cổng host `5436` — Project 1/CRM đã chiếm `5435`)
- EF Core (Npgsql), ASP.NET Core Identity + JWT (tái sử dụng/port từ Project 1)
- FluentValidation
- Clean Architecture: Domain / Application / Infrastructure / Api (giống cấu trúc Project 1)

## Kiến trúc

```
src/
  Hangar.Domain          # Entity thuần, không phụ thuộc layer nào khác
  Hangar.Application      # Interface, DTO, Service (use case), Validator — phụ thuộc Domain
  Hangar.Infrastructure   # EF Core, AppDbContext, Identity, Fluent config, migrations
  Hangar.Api              # Controllers, Program.cs (composition root)
```

## Chạy project (đang ở giai đoạn dựng nền)

```bash
docker compose up -d postgres
cd src/Hangar.Api
dotnet run
```

## Trạng thái

Mới dựng solution skeleton (Ngày 0) — chưa có entity/migration/tính năng nào. Theo dõi tiến độ ở `docs/1-ke-hoach-chi-tiet.md` và bảng tổng ở `dotnet-roadmap.md` (thư mục gốc repo học tập).
