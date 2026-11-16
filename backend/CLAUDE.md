# Quy tắc dự án — Backend JP Center ERP

Áp dụng cho MỌI thay đổi trong `backend/`. Quy tắc phụ thuộc được kiểm tra tự động bởi `tests/JPCenter.ArchitectureTests`
— vi phạm là build đỏ. Muốn đổi quy tắc: viết ADR trong `docs/decisions/` trước, rồi mới sửa file này.

## 1. Công nghệ

| Hạng mục | Chọn | Ghi chú |
|---|---|---|
| Runtime | .NET 10 (LTS), C# 14 | `Nullable` bật, `TreatWarningsAsErrors` bật |
| Web | ASP.NET Core **Minimal API** | Không dùng MVC Controller |
| Dữ liệu | EF Core 10 + PostgreSQL (Npgsql) | 1 database, **mỗi module 1 schema + 1 DbContext** |
| Validate | FluentValidation | |
| Command/Query | **Tự viết** trong `Application.Common` | KHÔNG dùng MediatR / AutoMapper (đã đổi sang license thương mại) |
| Mapping | Viết tay (extension `ToDto()` / projection LINQ) | |
| Log | Serilog → console + Seq | Log có cấu trúc, không nối chuỗi |
| Job nền | `BackgroundService` + Outbox; lịch định kỳ: Quartz.NET | |
| Realtime | SignalR | Chuông thông báo, lời mời dạy thay, điểm danh QR |
| File | Lưu trữ S3-compatible (MinIO local) qua `IFileStorage` | |
| Test | xUnit, FluentAssertions-free (dùng `Assert` / Shouldly), Testcontainers, NetArchTest | |

Thêm thư viện mới → ghi lý do + license vào ADR. Không thêm thư viện cho việc vài dòng code làm được.

## 2. Cấu trúc — CHỈ MỘT KHUÔN

- Mọi module giống hệt [docs/module-template.md](docs/module-template.md). **Không** tạo thư mục ngoài khuôn
  (`Helpers/`, `Utils/`, `Services/` tùy hứng, `Common2/`...).
- Module mới = sao chép khuôn, đổi tên. Không có module "đặc biệt".
- Một use case = một thư mục: `Application/<Feature>/<UseCase>/` chứa Command/Query + Handler + Validator + Dto của riêng nó.
- Một aggregate = một thư mục: `Domain/<Aggregate>/` chứa entity gốc, entity con, value object, event, lỗi của nó.

## 3. Phụ thuộc (kiểm tra tự động)

```
SharedKernel           ← không phụ thuộc gì (trừ BCL)
Application.Common     → SharedKernel
Infrastructure.Common  → Application.Common, SharedKernel
Api.Common             → Application.Common

Module.Domain          → SharedKernel
Module.Application     → Module.Domain, Application.Common, <Module khác>.Contracts
Module.Infrastructure  → Module.Application, Infrastructure.Common
Module.Api             → Module.Application, Api.Common
Module.Contracts       → SharedKernel (chỉ kiểu dữ liệu nguyên thủy / DTO / event)

Host (JPCenter.Api)    → tất cả module (chỉ để đăng ký), BuildingBlocks
```

- Module A **không bao giờ** tham chiếu bên trong module B — chỉ `JPCenter.Modules.B.Contracts`.
- Không join bảng của module khác, không dùng DbContext của module khác. Cần dữ liệu module khác:
  - **Đọc đồng bộ**: gọi interface query trong `B.Contracts` (vd `IStudentLookup`).
  - **Phản ứng khi có thay đổi**: nghe **integration event** trong `B.Contracts/IntegrationEvents` (qua Outbox).
- Domain không biết EF Core, HTTP, JSON. Không attribute `[Table]`, `[JsonProperty]` trong Domain — cấu hình ở `Infrastructure/Persistence/Configurations`.

## 4. Helper dùng chung — TẬN DỤNG, KHÔNG VIẾT LẠI

1. Trước khi viết helper / extension / base class: **tra [docs/shared-helpers.md](docs/shared-helpers.md)**.
2. Helper chỉ 1 module dùng → để trong module, đúng tầng (`Domain/Common/` hoặc `Application/Common/`).
3. Helper **≥ 2 module** dùng → chuyển vào `BuildingBlocks` đúng project theo tầng, rồi **thêm 1 dòng vào shared-helpers.md** (tên, chỗ, khi nào dùng).
4. BuildingBlocks **không chứa nghiệp vụ** (không có "học phí", "lớp học"...). Nghiệp vụ dùng chung giữa module → Contracts + event.
5. Cấm: lớp `XxxHelper`/`XxxUtils` tĩnh khổng lồ, copy-paste code giữa module, service locator, `static` giữ trạng thái.

## 5. Domain

- Entity: setter `private`, tạo bằng factory `static Result<T> Create(...)`, thay đổi qua method nghiệp vụ (`lead.AssignTo(saleId)`), kiểm tra bất biến ngay trong method.
- Id dạng **strongly-typed** (`LeadId`, `ClassId`...) bọc `Guid` v7. Mã hiển thị (HV1000, GVV_N3-K12, HD2400) là thuộc tính riêng, không dùng làm khóa.
- Lỗi nghiệp vụ trả về `Result` / `Error` (có `code` + thông điệp tiếng Việt) — **không throw exception để điều khiển luồng**. Exception chỉ cho lỗi hệ thống.
- Tiền: value object `Money` (VND, `decimal`, không số lẻ). Không dùng `double` cho tiền / điểm.
- Thời gian: lưu `DateTimeOffset` (UTC); ngày thuần → `DateOnly`, giờ học → `TimeOnly`. Lấy giờ hiện tại qua `IClock` (không `DateTime.Now`). Đổi sang giờ Việt Nam (Asia/Ho_Chi_Minh) chỉ ở rìa (API/báo cáo).
- Thay đổi quan trọng phát **domain event** (`ClassFinished`, `StudentEnrolled`...). Event cần module khác biết → ánh xạ thành integration event trong Contracts.
- Trạng thái dùng `enum` + method chuyển trạng thái có kiểm tra (không gán thẳng `Status = ...` từ ngoài).
- **Thêm / sửa / xóa entity hoặc thuộc tính → sửa [docs/database.md](docs/database.md) trong CÙNG commit** (bảng, kiểu, giải thích + 1 dòng Lịch sử thay đổi).
  Mỗi entity ghi tên bảng trong comment `/// Bảng: schema.table` — phải trùng với database.md.
- Tham chiếu sang module khác chỉ lưu `Guid` (ghi chú `Module.Entity` trong comment), không dùng Id strongly-typed của module khác.

## 6. Đa chi nhánh & phân quyền

- Mọi dữ liệu thuộc chi nhánh có `BranchId`. Lọc theo phạm vi quyền (`own` / `branch` / `all`) bằng **global query filter** trong DbContext gốc — không tự viết `Where(BranchId == ...)` rải rác.
- Mã quyền dạng `<module>.<page>` + hành động (`view, create, update, delete, approve, export`), **trùng với frontend** (`frontend/src/config/navigation.ts`, `mocks/data/system.ts`).
- Endpoint khai báo quyền: `.RequirePermission("crm.leads", PermissionAction.Update)`. Không kiểm tra theo **tên vai trò**.
- Cổng (staff / teacher / student / parent) là loại tài khoản; dữ liệu cổng luôn lọc theo **chủ tài khoản** (GV chỉ thấy lớp mình dạy, PH chỉ thấy con mình).
- Thông tin chỉ nhân viên trung tâm được xem (lịch sử chăm sóc, học phí, ghi chú nội bộ) **không** trả về ở endpoint cổng GV / HV / PH.

## 7. Dữ liệu (EF Core)

- Mỗi module: `<Module>DbContext : ModuleDbContextBase`, `HasDefaultSchema("<module>")`, migration riêng trong `Infrastructure/Persistence/Migrations`.
- Đặt tên bảng/cột **snake_case** (convention chung), bảng số nhiều (`leads`, `class_sessions`).
- Interceptor chung tự điền `created_at/by`, `updated_at/by`; **xóa mềm** (`is_deleted`, `deleted_at/by`) cho dữ liệu nghiệp vụ — xóa cứng chỉ khi dọn Thùng rác.
- Chống sửa đè: concurrency token (`xmin` của PostgreSQL).
- Không lazy loading. Đọc dữ liệu: `AsNoTracking()` + projection thẳng ra DTO. Không repository generic bọc DbContext.
- Không viết SQL nối chuỗi. Truy vấn phức tạp / báo cáo → raw SQL có tham số hoặc view trong schema `reporting`.

## 8. API

- Đường dẫn: `/api/v1/<module>/<resource>` — kebab-case, danh từ số nhiều (`/api/v1/academic/class-sessions`).
- JSON camelCase; **tên trường khớp type frontend** (`frontend/src/features/**/types.ts`) để thay mock không phải sửa UI.
- Lỗi: `ProblemDetails` có `code` (vd `crm.lead.duplicate_phone`), `title` tiếng Việt, `errors` theo trường khi validate.
- Phân trang chung: query `page`, `pageSize` (≤ 100), `sort`; trả `{ items, total, page, pageSize }`.
- Ngày giờ ISO 8601; ngày thuần `YYYY-MM-DD`, giờ `HH:mm` — giống frontend.
- Endpoint mỏng: nhận request → gửi Command/Query → trả kết quả. Không chứa logic nghiệp vụ.
- Mỗi module tự đăng ký endpoint trong `Api/<Module>Endpoints.cs`; Host chỉ gọi `app.MapModules()`.

## 9. Đặt tên & ngôn ngữ

- Code, tên bảng, tên API: **tiếng Anh**. Comment / tài liệu / thông điệp lỗi cho người dùng: **tiếng Việt**.
- Thuật ngữ cố định (dùng thống nhất ở mọi module):
  `Lead` lead · `Student` học viên · `Guardian` phụ huynh (lead có liên kết) · `Course` khóa học · `Curriculum` lộ trình ·
  `Class` lớp · `ClassSession` buổi học · `Enrollment` ghi danh · `Invoice` hóa đơn · `Branch` chi nhánh ·
  `Certificate` chứng nhận hoàn thành (của trung tâm) · `JlptRegistration` / `JlptCertificate` chứng chỉ JLPT (hội đồng thi cấp) ·
  `Promotion` lên lớp · `StudentEvaluation` nhận xét cuối khóa.
- File: 1 kiểu public / 1 file, tên file = tên kiểu. Command `XxxCommand`, Query `GetXxxQuery`, handler `XxxHandler`, validator `XxxValidator`, DTO `XxxDto`/`XxxResponse`.

## 10. Kiểm thử

- Domain: unit test mọi method chuyển trạng thái và bất biến.
- Use case: test handler với DbContext thật (Testcontainers), không mock DbContext.
- Mỗi endpoint mới: ít nhất 1 integration test happy path + 1 test sai quyền.
- ArchitectureTests phải xanh trước khi merge.

## 11. Quy trình thêm một chức năng

1. Xác định module sở hữu dữ liệu (nếu phân vân → ADR).
2. Domain: aggregate / method / event / lỗi.
3. Application: thư mục use case (Command/Query + Handler + Validator + Dto).
4. Infrastructure: cấu hình EF + migration.
5. Api: endpoint + quyền.
6. Contracts: integration event / interface nếu module khác cần.
7. Test. Cập nhật `docs/shared-helpers.md` nếu có helper dùng chung mới.
8. Đối chiếu type frontend + thay mock (`frontend/src/mocks`).
