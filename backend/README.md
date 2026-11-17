# JP Center ERP — Backend

.NET 10 (LTS) · ASP.NET Core Minimal API · EF Core + PostgreSQL · **Modular Monolith**

> Trạng thái: solution build được, có Domain cho 10 module, Docker local, test kiến trúc.
> Đang làm: đăng nhập & phân quyền (module System) + middleware ([docs/request-pipeline.md](docs/request-pipeline.md)).

## Chạy local

Cần: .NET SDK 10, Docker Desktop.

```bash
cd backend
cp .env.example .env                                              # 1 lần — sửa nếu cần
docker compose -f deploy/docker-compose.yml --env-file .env up -d # PostgreSQL, Adminer, Mailpit, Seq
dotnet run --project src/Host/JPCenter.Api                        # API: http://localhost:5080
dotnet test --project tests/JPCenter.ArchitectureTests            # test kiến trúc
```

| Dịch vụ | Địa chỉ | Ghi chú |
|---|---|---|
| API | http://localhost:5080 | `/health` kiểm tra sống |
| PostgreSQL | `localhost:5433` | user / mật khẩu / DB trong `.env` (5433 để tránh Postgres cài sẵn trên máy) |
| Adminer (xem DB) | http://localhost:8081 | Hệ quản trị **PostgreSQL**, Server `postgres`, user / mật khẩu trong `.env` |
| Mailpit (hộp thư giả) | http://localhost:8025 | Email OTP quên mật khẩu, email mời kích hoạt |
| Seq (log) | http://localhost:5341 | |

Dữ liệu DB nằm trong volume Docker `jpcenter_pgdata` — tắt máy / `docker compose down` vẫn còn.
Xóa sạch làm lại: `docker compose -f deploy/docker-compose.yml down -v`.

- Quy tắc bắt buộc: [CLAUDE.md](CLAUDE.md)
- Nghiệp vụ & chức năng (đọc từ frontend): [docs/nghiep-vu.md](docs/nghiep-vu.md)
- **Cơ sở dữ liệu chuẩn** (mọi bảng, thuộc tính, giải thích): [docs/database.md](docs/database.md)
- Khuôn một module: [docs/module-template.md](docs/module-template.md)
- Helper dùng chung (tra trước khi viết mới): [docs/shared-helpers.md](docs/shared-helpers.md)
- Quyết định kiến trúc (ADR): [docs/decisions/](docs/decisions/)

## Vì sao Modular Monolith

Một ứng dụng, một lần deploy, một database — nhưng chia thành các **module độc lập** theo nghiệp vụ, mỗi module
có schema DB riêng và chỉ nói chuyện với nhau qua **Contracts**. Đủ đơn giản cho 1 đội nhỏ, mà sau này tách
module nào ra service riêng cũng không phải viết lại.

## Cấu trúc thư mục (CỐ ĐỊNH — không thêm thư mục ngoài khuôn)

```
backend/
├─ CLAUDE.md                         Quy tắc dự án (bắt buộc đọc)
├─ README.md
├─ docs/
│  ├─ module-template.md             Khuôn 1 module + 1 use case
│  ├─ shared-helpers.md              Danh mục helper dùng chung
│  └─ decisions/                     ADR: 0001-tech-stack.md, ...
├─ deploy/                           docker-compose (PostgreSQL, MinIO, Seq), cấu hình môi trường
├─ src/
│  ├─ Host/
│  │  └─ JPCenter.Api/               Điểm chạy duy nhất: Program.cs, gom module, middleware, cấu hình
│  ├─ BuildingBlocks/                Code DÙNG CHUNG — không chứa nghiệp vụ
│  │  ├─ JPCenter.SharedKernel/          Entity, AggregateRoot, ValueObject, Result/Error, Money, Id, IClock...
│  │  ├─ JPCenter.Application.Common/    Command/Query, pipeline (validate, log, transaction), phân trang, người dùng hiện tại, quyền
│  │  ├─ JPCenter.Infrastructure.Common/ DbContext gốc, interceptor (audit, xóa mềm), Outbox, lưu file, gửi email/Zalo
│  │  └─ JPCenter.Api.Common/            Đăng ký endpoint, ProblemDetails, RequirePermission, phân trang HTTP
│  └─ Modules/
│     └─ <Module>/
│        ├─ JPCenter.Modules.<Module>/            Domain/ · Application/ · Infrastructure/ · Api/
│        └─ JPCenter.Modules.<Module>.Contracts/  Thứ DUY NHẤT module khác được tham chiếu
└─ tests/
   ├─ JPCenter.ArchitectureTests/    Kiểm tra tự động các quy tắc phụ thuộc trong CLAUDE.md
   ├─ JPCenter.IntegrationTests/     API + DB thật (Testcontainers PostgreSQL)
   └─ Modules/<Module>/JPCenter.Modules.<Module>.Tests/   Unit test domain + use case
```

## Các module (khớp menu frontend `frontend/src/config/navigation.ts`)

| Module | Schema DB | Phạm vi | Trang frontend tương ứng |
|---|---|---|---|
| **System** | `system` | Tài khoản, đăng nhập, vai trò & quyền, chi nhánh, cài đặt, nhật ký, thùng rác, duyệt 2 người, tích hợp | `/admin/system/*`, đăng nhập, tài khoản cổng |
| **Crm** | `crm` | Lead, lịch sử tư vấn, liên kết lead ↔ học viên, chia lead & SLA, test xếp lớp, lịch hẹn, chiến dịch, mẫu tin nhắn, tư vấn lên lớp | `/admin/crm/*` |
| **Students** | `students` | Hồ sơ học viên, phụ huynh / người giám hộ, ghi danh, cảnh báo bỏ học, JLPT, chứng nhận hoàn thành, du học / XKLĐ | `/admin/students/*` |
| **Academic** | `academic` | Khóa học, lộ trình, lớp, buổi học, điểm danh, phòng, ngân hàng câu hỏi, bài tập, bài thi, nhận xét, lên lớp, đơn hủy buổi / xin nghỉ, dạy thay, flashcard | `/admin/academic/*`, cổng GV / HV |
| **Library** | `library` | Thư viện tài liệu, tài liệu của giáo viên, lượt xem | `/admin/library/*`, Tài liệu (cổng) |
| **Finance** | `finance` | Học phí, hóa đơn, thanh toán, hoàn tiền, đối soát VietQR, công nợ, lương GV / NV, hoa hồng | `/admin/finance/*` |
| **Hr** | `hr` | Nhân viên, giáo viên, chấm công, đơn từ, tuyển dụng, onboarding | `/admin/hr/*` |
| **Work** | `work` | Task, xếp ca, bàn giao ca, lịch rảnh | `/admin/work/*`, `/admin/me/*` |
| **Notifications** | `notifications` | Thông báo trong app, Zalo OA, email, SMS, mẫu & lịch gửi | Chuông thông báo, thông báo lớp |
| **Reporting** | `reporting` | Báo cáo / dashboard — chỉ ĐỌC (read model dựng từ integration event) | `/admin/dashboard/*` |

> Ranh giới module là đề xuất ban đầu — sẽ chốt lại ở bước thiết kế entity (ghi vào `docs/decisions/`).

## Chuẩn bị máy (khi bắt đầu code)

1. Cài **.NET SDK 10** (máy hiện có SDK 8): `winget install Microsoft.DotNet.SDK.10`
2. Docker Desktop (đã có) → `deploy/docker-compose.yml` chạy PostgreSQL + MinIO + Seq
3. IDE: Visual Studio 2022 17.14+ / Rider / VS Code + C# Dev Kit
