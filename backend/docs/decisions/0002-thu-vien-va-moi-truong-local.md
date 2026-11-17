# ADR 0002 — Thư viện & môi trường chạy local

- Ngày: 2026-11-17
- Trạng thái: **Chấp nhận**

## Bối cảnh

Bắt đầu viết code (đăng nhập, phân quyền, middleware — [request-pipeline.md](../request-pipeline.md)). Cần chốt thư viện,
version và cách chạy local. Dự án cá nhân, chạy 1 instance, không Redis.

## Quyết định

### Version
- .NET SDK 10 (`global.json`, `rollForward: latestFeature`). .NET 11 đã ra nhưng không phải LTS → **giữ .NET 10**;
  gói của Microsoft ghim **10.0.x** (gói 11.x không chạy trên net10.0).
- Version mọi gói khai báo tập trung ở `Directory.Packages.props`.
- `dotnet test` dùng Microsoft.Testing.Platform (bắt buộc từ SDK 10 với xUnit v3): `dotnet test --project <thư mục test>`.

### Thư viện thêm

| Gói | Dùng cho | License |
|---|---|---|
| Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore(.Design) | EF Core + PostgreSQL, migration | PostgreSQL / MIT |
| EFCore.NamingConventions | Tự đặt tên bảng / cột snake_case | Apache-2.0 |
| FluentValidation (+ DependencyInjectionExtensions) | Kiểm tra dữ liệu vào | Apache-2.0 |
| Microsoft.AspNetCore.Authentication.JwtBearer | Kiểm tra access token | MIT |
| Serilog.AspNetCore, Serilog.Sinks.Seq | Log có cấu trúc → console + Seq | Apache-2.0 |
| Quartz.Extensions.Hosting | Job định kỳ (quyền lẻ sắp hết hạn, dọn token / audit) | Apache-2.0 |
| xunit.v3, Shouldly, NetArchTest.Rules, Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing | Kiểm thử | Apache-2.0 / BSD / MIT |

**Không thêm thư viện** cho: băm mật khẩu (dùng `PasswordHasher<T>` có sẵn trong ASP.NET Core — PBKDF2-SHA512),
gửi email (`System.Net.Mail.SmtpClient` đủ cho SMTP local / Mailpit), đọc `.env` (~20 dòng, `DotEnv.cs`),
rate limit (`Microsoft.AspNetCore.RateLimiting` có sẵn), cache (`IMemoryCache` có sẵn).

### Môi trường local (Docker)
`deploy/docker-compose.yml`: PostgreSQL 17 (cổng **5433**, tránh Postgres cài sẵn chiếm 5432), Adminer (xem DB trên web),
Mailpit (hộp thư giả xem OTP / email mời), Seq (xem log). Cấu hình trong `.env` (mẫu: `.env.example`) — docker compose và API
cùng đọc.

### Dữ liệu mẫu
Môi trường Development: API tự chạy migration + nạp dữ liệu mẫu (lấy từ mock của frontend) **khi bảng còn trống**.
Dữ liệu mẫu **giữ lại**, không tự xóa — để xem giao diện có dữ liệu.

## Hệ quả
- Lên .NET 12 LTS (11/2027) → ADR mới, nâng các gói 10.0.x cùng lúc.
- Gửi email thật (SMTP có TLS / API nhà cung cấp) → cân nhắc MailKit, ghi ADR.
