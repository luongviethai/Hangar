# ERD — Hangar (Project 2, Giai đoạn 2)

> Placeholder — vẽ chính thức ở Ngày 2 (Tuần 4) khi entity `Organization`/`OrganizationMember`/`App` đã có migration thật. Xem thiết kế entity đầy đủ (bảng field) ở [2-tai-lieu-nghiep-vu.md](2-tai-lieu-nghiep-vu.md) mục 2 — ERD ở đây chỉ là sơ đồ quan hệ, không lặp lại chi tiết field.

```mermaid
erDiagram
    ORGANIZATION ||--o{ ORGANIZATION_MEMBER : "1-n"
    ORGANIZATION ||--o{ APP : "1-n"
    ORGANIZATION ||--o{ API_KEY : "1-n"
    ORGANIZATION ||--o{ WEBHOOK_ENDPOINT : "1-n"
    APP ||--o{ BUILD : "1-n"
    APP ||--o{ APP_TESTER : "1-n"
    BUILD ||--o{ INSTALL_LINK : "1-n"
    WEBHOOK_ENDPOINT ||--o{ WEBHOOK_DELIVERY : "1-n"
```
