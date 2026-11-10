# Tài liệu nghiệp vụ — Hangar (Project 2, Giai đoạn 2)

> Nguồn tham chiếu nghiệp vụ duy nhất khi code, tránh tự suy diễn giữa đường. Cập nhật lại nếu nghiệp vụ thay đổi trong lúc làm.
>
> **Làm lại 2026-11-09:** bỏ Organization, thành viên/vai trò, tester, mời qua email, API key, webhook. Bản cũ xem tag git `ban-cu-auth-org`.

## 1. Bối cảnh

Hangar là kho build mobile (APK/IPA) dùng nội bộ: **upload, lưu trữ, chia sẻ cho người cần cài thử** mà không cần qua App Store/Play Store.

Hangar là **hệ thống mở**: không có tài khoản, không đăng nhập — ai gọi được API thì upload/tải được. Phần học của project nằm ở chỗ khác Project 1 (CRM): xử lý **file lớn** (streaming, object storage), **chia sẻ có kiểm soát** (link có hạn dùng/giới hạn lượt tải, an toàn khi tải đồng thời), **việc chạy nền**, **cache**, **chống spam**, **quan sát hệ thống** và **deploy**.

Vì ai cũng upload được, mọi dữ liệu client gửi lên (tên file, release notes, nội dung file) đều phải coi là không tin cậy.

## 2. Domain entities

### App
Ứng dụng mobile mà các build thuộc về.

| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| Name | string | required, maxlength 200 |
| Platform | enum | `Android`, `iOS` — trả về JSON dạng chuỗi |
| PackageId | string | ví dụ `com.company.app` — required, **unique toàn hệ thống** |
| CreatedAtUtc | datetime | |

### Build
1 file APK/IPA đã upload của 1 App.

| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| AppId | int | FK |
| Version | string? | ví dụ `"1.4.2"` — đọc từ file; null trong lúc chờ job đọc metadata (từ Ngày 11) |
| BuildNumber | ? | tăng dần, dùng sort "bản mới nhất" — kiểu dữ liệu chốt ở Ngày 6; null trong lúc chờ job |
| StorageKey | string | key trong MinIO do server sinh (`apps/{appId}/{Guid}.apk`), không phải URL public, không dùng tên file client gửi |
| OriginalFileName | string | tên file lúc upload — chỉ để hiển thị |
| FileSizeBytes | long | size thật đo được khi ghi file, không tin header client gửi |
| ReleaseNotes | string? | nullable — dữ liệu người dùng, phải encode khi hiển thị HTML |
| ProcessingStatus | enum | `Pending`, `Processing`, `Ready`, `Failed` (từ Ngày 12) |
| FailureReason | string? | lý do khi `Failed` |
| CreatedAtUtc | datetime | |

Unique index `(AppId, BuildNumber)`.

### InstallLink
Link chia sẻ 1 Build cho người cần cài.

| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| BuildId | int | FK |
| Token | string | ngẫu nhiên (32 byte, Base64Url), unique — dùng trong URL `/install/{token}`, không dùng Id số (đoán được build khác) |
| ExpiresAtUtc | datetime? | null = không hết hạn |
| MaxDownloads | int? | null = không giới hạn |
| DownloadCount | int | default 0, tăng mỗi lần **tải thật** (mở trang xem không tính) |
| CreatedAtUtc | datetime | |

## 3. Quy tắc nghiệp vụ

| # | Quy tắc | Vi phạm thì |
|---|---|---|
| 1 | `PackageId` của App là duy nhất | `409` |
| 2 | Upload vào App không tồn tại | `404`, không lưu file nào |
| 3 | File vượt `Upload:MaxSizeBytes` | dừng đọc ngay, dọn file tạm, `413` |
| 4 | `PackageId` đọc từ file phải khớp `App.PackageId`; `.apk` chỉ cho App Android, `.ipa` chỉ cho App iOS | Build bị chặn (Ngày 6) / thành `Failed` (từ Ngày 11) |
| 5 | Không có 2 Build trùng `BuildNumber` trong 1 App | `409` / `Failed` |
| 6 | Chỉ Build `Ready` mới lấy được presigned URL / tạo được InstallLink | `409` |
| 7 | InstallLink hết hạn hoặc hết lượt tải | `410 Gone` |
| 8 | Lượt tải cuối cùng chỉ được đúng 1 request lấy, kể cả khi nhiều request tới cùng lúc | request còn lại `410` |
| 9 | Mỗi App chỉ giữ N build `Ready` mới nhất (`Retention:KeepLatestBuilds`) | job đêm tự xóa file + record build cũ |
| 10 | Không để rác: file trong MinIO luôn có Build tương ứng và ngược lại; không để file tạm trên đĩa | tự dọn ngay khi lỗi, job dọn phần còn sót |

## 4. User stories

- Là developer, tôi tạo App mới rồi upload file APK/IPA vừa build — Hangar tự đọc version, build number, package từ chính file.
- Là developer, tôi upload nhầm file của app khác — Hangar chặn lại và nói rõ lý do.
- Là developer, tôi tạo link cài đặt cho 1 build, giới hạn 5 lượt tải trong 3 ngày, gửi qua Slack/Zalo.
- Là người nhận link, tôi mở link thấy tên app, version, release notes, bấm "Tải về" là cài được — không cần tài khoản.
- Là hệ thống, tôi tự dọn build cũ của mỗi App để kho không đầy dần.

## 5. API endpoint sơ bộ

Chi tiết hoàn thiện dần theo từng ngày trong `docs/1-ke-hoach-chi-tiet.md`.

| Method | Route | Ghi chú |
|---|---|---|
| POST | `/api/apps` | Tạo App |
| GET | `/api/apps` | Danh sách App |
| GET/PUT/DELETE | `/api/apps/{appId}` | |
| POST | `/api/apps/{appId}/builds` | Upload build (multipart, streaming) |
| GET | `/api/apps/{appId}/builds` | Danh sách build, mới nhất trước, có cache |
| GET/DELETE | `/api/apps/{appId}/builds/{buildId}` | |
| GET | `/api/apps/{appId}/builds/{buildId}/download-url` | Presigned URL tải thẳng từ MinIO |
| POST | `/api/apps/{appId}/builds/{buildId}/install-links` | Tạo link cài đặt |
| GET | `/install/{token}` | Trang HTML cho người nhận link (không tính lượt) |
| GET | `/install/{token}/download` | Tính lượt tải rồi `302` sang presigned URL |
| GET | `/health/live`, `/health/ready` | Health checks |
| GET | `/hangfire` | Dashboard job nền — chỉ mở từ máy local |
