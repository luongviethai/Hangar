# ERD — Hangar (Project 2, Giai đoạn 2)

> Sơ đồ dự kiến — cập nhật theo code thật ở Ngày 23. Chi tiết field xem [2-tai-lieu-nghiep-vu.md](2-tai-lieu-nghiep-vu.md) mục 2, ở đây chỉ vẽ quan hệ.

```mermaid
erDiagram
    APP ||--o{ BUILD : "1-n"
    BUILD ||--o{ INSTALL_LINK : "1-n"
```
