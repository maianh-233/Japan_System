# Danh mục helper dùng chung (BuildingBlocks)

**Tra file này TRƯỚC khi viết helper / base class / extension mới.** Có rồi thì dùng; chưa có mà ≥ 2 module cần
→ thêm vào BuildingBlocks rồi ghi thêm 1 dòng ở đây. Trạng thái: 📝 dự kiến · ✅ đã có code.

## JPCenter.SharedKernel — không phụ thuộc gì

| Thành phần | Dùng khi | Trạng thái |
|---|---|---|
| `Entity<TId>`, `AggregateRoot<TId>` (`Domain/`) | Base cho entity; aggregate giữ danh sách domain event (`Raise`) | ✅ |
| `ValueObject` | So sánh theo giá trị (Money, PhoneNumber, TimeRange...) | 📝 |
| `record struct XxxId(Guid Value)` + `XxxId.New()`, `IdGenerator.NewId()` (Guid v7) (`Primitives/`) | Id của mọi aggregate — mỗi Id 1 file trong thư mục aggregate | ✅ |
| `Result`, `Result<T>`, `Error`, `ErrorType` (`Primitives/`) — có chuyển ngầm `Error → Result`, `T → Result<T>` | Trả lỗi nghiệp vụ, không throw | ✅ |
| `IDomainEvent` (`Domain/`) — event là `sealed record` | Sự kiện trong module | ✅ |
| `Money` (`Primitives/`): `Create` (kiểm tra ≥ 0, không lẻ), `FromTrusted`, `+ − < >`, `Percent()` | Học phí, lương, thanh toán | ✅ |
| `PhoneNumber` (chuẩn hóa số VN: 0xxx / +84) | Lead, học viên, nhân viên — kiểm tra trùng SĐT | 📝 |
| `DateRange`, `TimeRange` (kiểm tra chồng lấn) | Lịch lớp, ca làm, trùng phòng / GV | 📝 |
| `IClock` (`Domain/`: `UtcNow`, `Today` theo giờ VN) | Thay `DateTime.Now` ở mọi nơi | ✅ interface |
| `Guard` (`Against.NullOrEmpty`, `Against.OutOfRange`) | Kiểm tra tham số trong factory | 📝 |
| `BranchId` (`Primitives/`), `IBranchScoped`, `IAuditable`, `ISoftDeletable` (`Domain/`) | Đánh dấu entity để interceptor / filter chung xử lý | ✅ |
| Enum dùng chung (`Primitives/`): `JlptLevel`, `JapaneseSkill`, `Gender`, `ShiftKey`, `ContactChannel` | Giá trị chung toàn hệ thống (không phải nghiệp vụ của 1 module) — KHÔNG thêm enum nghiệp vụ vào đây | ✅ |

## JPCenter.Application.Common

| Thành phần | Dùng khi | Trạng thái |
|---|---|---|
| `ICommand`, `ICommand<T>`, `IQuery<T>`, `ICommandHandler`, `IQueryHandler`, `ISender` | Mọi use case (thay MediatR) | 📝 |
| Pipeline: `LoggingBehavior`, `ValidationBehavior`, `TransactionBehavior` | Tự chạy cho mọi command | 📝 |
| `PagedQuery`, `PagedResult<T>`, `ToPagedResultAsync()` | Danh sách có phân trang | 📝 |
| `ICurrentUser` (UserId, EmployeeId, Portal, BranchIds, Permissions, OwnerId cho cổng HV/PH/GV) | Lấy người đang gọi | 📝 |
| `PermissionAction`, `PermissionScope` (own/branch/all), `IPermissionService` | Kiểm tra quyền trong handler khi cần chi tiết hơn endpoint | 📝 |
| `IIntegrationEventPublisher` | Phát event cho module khác (đi qua Outbox) | 📝 |
| `ICodeGenerator` (sinh mã HV1000, HD2400, LD0001 theo dãy số trong DB) | Mã hiển thị tăng dần, không trùng | 📝 |
| `IFileStorage` (upload, link tạm có hạn, xóa) | Tài liệu, đề bài, bài nộp, ảnh hồ sơ | 📝 |
| `INotificationSender` (in-app / Zalo / email / SMS) | Gửi thông báo từ mọi module | 📝 |
| `ValidationRules` mở rộng FluentValidation: `.VietnamPhone()`, `.VndMoney()`, `.Code()` | Validator dùng lại | 📝 |

## JPCenter.Infrastructure.Common

| Thành phần | Dùng khi | Trạng thái |
|---|---|---|
| `ModuleDbContextBase` (schema, snake_case, global filter chi nhánh + xóa mềm, xmin) | Base cho DbContext mọi module | 📝 |
| `AuditInterceptor`, `SoftDeleteInterceptor`, `DomainEventOutboxInterceptor` | Tự gắn vào DbContext base | 📝 |
| `OutboxMessage`, `OutboxProcessor` (BackgroundService) | Gửi integration event chắc chắn | 📝 |
| `InboxMessage` (chống xử lý event 2 lần) | Handler integration event | 📝 |
| `S3FileStorage` (MinIO / S3) | Hiện thực `IFileStorage` | 📝 |
| `SequenceCodeGenerator` (PostgreSQL sequence) | Hiện thực `ICodeGenerator` | 📝 |
| `SystemClock` | Hiện thực `IClock` | 📝 |

## JPCenter.Api.Common

| Thành phần | Dùng khi | Trạng thái |
|---|---|---|
| `IModuleEndpoints` + `app.MapModules()` | Mỗi module tự khai báo endpoint | 📝 |
| `.RequirePermission(code, action)` | Gắn quyền cho endpoint | 📝 |
| `ResultExtensions.ToHttpResult()` (Result → 200/201/204 hoặc ProblemDetails theo `Error.Type`) | Trả kết quả thống nhất | 📝 |
| `GlobalExceptionHandler` → ProblemDetails | Lỗi hệ thống | 📝 |
| `[AsParameters] PagingRequest` | Đọc `page`, `pageSize`, `sort` từ query | 📝 |
