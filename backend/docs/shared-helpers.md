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
| `ICommand`, `ICommand<T>`, `IQuery<T>`, `ICommandHandler`, `IQueryHandler`, `ISender` (`Messaging/`) | Mọi use case (thay MediatR). Module gọi `services.AddHandlersFromAssembly(assembly)` để đăng ký handler + validator | ✅ |
| Pipeline `LoggingBehavior` → `ValidationBehavior` (`Behaviors/`), `IPipelineBehavior` | Tự chạy cho mọi request; validate sai → `validation.failed` + lỗi từng trường (camelCase), không gọi handler | ✅ |
| ~~`TransactionBehavior`~~ | **Không làm**: handler gọi `SaveChangesAsync` 1 lần = 1 transaction. Cần nhiều lần lưu trong 1 giao dịch → mở transaction ngay trong handler | — |
| `ResultFactory.Failure<TResponse>(error)` | Tạo `Result`/`Result<T>` thất bại khi chỉ biết kiểu lúc chạy (viết behavior) | ✅ |
| `PageRequest`, `PagedResult<T>`, `ToPagedResultAsync()`, `SortBy(sort, allowed, fallback)` (`Paging/`) | Danh sách có phân trang; `sort` chỉ nhận cột trong danh sách cho phép | ✅ |
| `ICurrentUser` (`Security/`): `AccountId`, `SessionId`, `Access` (`UserAccess`: cổng, tên, vai trò, EmployeeId, OwnerId, chi nhánh, quyền hiệu lực) | Lấy người đang gọi trong handler | ✅ |
| `user.Can(page, action)`, `user.Permission(page, action)` → `EffectivePermission(Scope, BranchIds)` (`CurrentUserExtensions`) | Kiểm tra quyền trong handler khi cần chi tiết hơn endpoint (vd lọc theo phạm vi) | ✅ |
| `PermissionAction`, `Portal` (`Security/`); `PermissionScope` (SharedKernel) | Mã quyền `module.page.action`, cổng staff/teacher/student/parent | ✅ |
| `IUserAccessProvider` (module System hiện thực), `IUserAccessCache` (`Invalidate` / `InvalidateAll` khi đổi quyền) | Nạp + cache quyền theo tài khoản | ✅ |
| `IRequestInfo` (IP, thiết bị, traceId) | Ghi phiên đăng nhập / nhật ký | ✅ |
| `ISessionRevocationList` (`Revoke(sessionId)`) | Đăng xuất / thu hồi phiên → access token của phiên đó bị chặn NGAY (middleware kiểm tra claim `sid`) | ✅ |
| `IAuditTrail.Record(category, severity, action, target, ...)` (`Auditing/`) | Ghi nhật ký từ handler (vd đăng nhập sai); tự điền người, IP, thiết bị, thay đổi dữ liệu | ✅ |
| `IAuditSink` (module System ghi `audit_logs`), `IAuditWriter` (hàng đợi) | Hạ tầng nhật ký | ✅ |
| `IIdempotencyStore` (`Idempotency/`) | Lưu kết quả theo Idempotency-Key | ✅ |
| `IIntegrationEventPublisher` | Phát event cho module khác (đi qua Outbox) | 📝 |
| `ICodeGenerator` (sinh mã HV1000, HD2400, LD0001 theo dãy số trong DB) | Mã hiển thị tăng dần, không trùng | 📝 |
| `IFileStorage` (upload, link tạm có hạn, xóa) | Tài liệu, đề bài, bài nộp, ảnh hồ sơ | 📝 |
| `INotificationSender` (in-app / Zalo / email / SMS) | Gửi thông báo từ mọi module | 📝 |
| `ValidationRules` mở rộng FluentValidation: `.VietnamPhone()`, `.VndMoney()`, `.Code()` | Validator dùng lại | 📝 |

## JPCenter.Infrastructure.Common

| Thành phần | Dùng khi | Trạng thái |
|---|---|---|
| `ModuleDbContextBase` (`Persistence/`) | Base cho DbContext mọi module: schema, Id strongly-typed ↔ `uuid` (+ `List<XxxId>` ↔ `uuid[]`), enum ↔ chuỗi snake_case, `Money` ↔ `numeric(18,0)`, cột chung created/updated, xóa mềm + query filter tên `soft_delete`, `xmin` cho aggregate. Cấu hình EF đặt trong namespace `*.Infrastructure.*` của module được nạp tự động | ✅ |
| `ShadowColumns` | Tên shadow property cột chung; `IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])` để xem Thùng rác | ✅ |
| `AuditFieldsInterceptor` | Tự điền created/updated, đổi xóa → xóa mềm (gắn sẵn qua `AddModuleDbContext`) | ✅ |
| `AddInfrastructureCommon()`, `AddModuleDbContext<T>(config, schema)` | Đăng ký đồng hồ, interceptor, DbContext module (PostgreSQL, snake_case, bảng migration riêng mỗi schema) | ✅ |
| `IDataSeeder` + `InitializeDatabaseAsync()` | Development: tự chạy migration + nạp dữ liệu mẫu theo `Order`; seeder chỉ nạp khi bảng trống, **không xóa** | ✅ |
| `SystemClock` (`Time/`) | Hiện thực `IClock` (Today theo giờ VN) | ✅ |
| `.HasJsonConversion()` (`JsonColumnExtensions`) | Thuộc tính có cấu trúc (dictionary, danh sách record) → cột `jsonb` | ✅ |
| `EnumListConverter` (tự áp cho mọi `List<Enum>`) | Danh sách enum → `text[]` snake_case | ✅ |
| `SnakeCaseEnumConverter<T>.ToText()` / `.Parse()` | Đổi enum ↔ chuỗi DB / frontend ngoài EF (DTO Contracts, đọc seed) | ✅ |
| `SeedData.IdOf(mockId)`, `SeedData.Read<T>()`, `db.AddWithId(entity, id)` | Nạp dữ liệu mẫu xuất từ mock frontend (`npm run export-seed`) với Id cố định giữa các module | ✅ |
| `DataChangeAuditInterceptor` + `AuditChangeCollector` (`Auditing/`) | Gom before → after (che mật khẩu / token / khóa) vào dòng nhật ký của hành động | ✅ |
| `ChannelAuditWriter` + `AuditBackgroundWriter` (`Auditing/`) | Hàng đợi nhật ký ghi nền theo lô 200 dòng, không làm chậm request | ✅ |
| `LoggingAuditSink`, `InMemoryIdempotencyStore` | Mặc định khi chưa có module System (System thay bằng bản PostgreSQL) | ✅ |
| `AddScheduledJobs(config)` + `JobServiceCollectionExtensions.IsEnabled(config)` (`Jobs/`) | Bật Quartz (Host gọi 1 lần); module khai báo job bằng `services.AddQuartz(q => q.ScheduleJob<TJob>(...))` khi `IsEnabled`. Tắt bằng `Jobs:Enabled=false` (test tích hợp) | ✅ |
| `DomainEventOutboxInterceptor` | Sau này | 📝 |
| `OutboxMessage`, `OutboxProcessor` (BackgroundService) | Gửi integration event chắc chắn | 📝 |
| `InboxMessage` (chống xử lý event 2 lần) | Handler integration event | 📝 |
| `S3FileStorage` (MinIO / S3) | Hiện thực `IFileStorage` | 📝 |
| `SequenceCodeGenerator` (PostgreSQL sequence) | Hiện thực `ICodeGenerator` | 📝 |

## JPCenter.Api.Common

| Thành phần | Dùng khi | Trạng thái |
|---|---|---|
| `IModule` + `AddModules(...)` / `MapModules()` (`Modules/`) | Mỗi module có `<Module>Module.cs` khai báo dịch vụ + endpoint trên group `/api/v1/<module>` | ✅ |
| `AddApiCommon()` | JSON camelCase, enum snake_case, **trường lạ → 400**, ProblemDetails, bắt lỗi toàn cục | ✅ |
| `ResultExtensions.ToHttpResult()` / `ToCreatedResult()` / `Error.ToProblem()` (`Results/`) | Result → 200/201/204 hoặc ProblemDetails theo `Error.Type` (400/401/403/404/409/500) | ✅ |
| `ProblemFactory` (`Errors/`) | Dựng lỗi `{ code, title, status, errors?, traceId }` | ✅ |
| `GlobalExceptionHandler` (`Errors/`) | JSON hỏng → 400, sửa đè (`DbUpdateConcurrencyException`) → 409, còn lại → 500 không lộ stack (trừ Development) | ✅ |
| `[AsParameters] PagingRequest` (`Paging/`) | Đọc `page`, `pageSize`, `sort` từ query | ✅ |
| `AddRequestPipeline(config)` / `UseRequestPipeline()` | Toàn bộ middleware theo đúng thứ tự (docs/request-pipeline.md) — Host chỉ gọi 2 hàm này | ✅ |
| `.RequirePermission(page, action)` / `.RequireSignedIn()` / `.RequirePortal(...)` / `.AllowAnonymous()` (`Security/`) | **Mọi endpoint phải có 1 trong 4** | ✅ |
| `AccessTokenIssuer.Issue(accountId, portal, tokenVersion, sessionId)`, `JwtOptions`, `JwtClaims` | Phát access token khi đăng nhập / làm mới phiên | ✅ |
| `.RequireRateLimiting(RateLimitPolicies.WriteSensitive / Anonymous / Export / Import)` (`RateLimiting/`) | Giới hạn riêng; mọi request đã có giới hạn chung + giới hạn ghi | ✅ |
| `.Idempotent()` (`Idempotency/`) | Bắt buộc header Idempotency-Key — thao tác tiền / ghi danh / gửi tin / nhập file | ✅ |
| `.Audited(action, category, severity, target?)` (`Auditing/`) | Ghi 1 dòng nhật ký khi thành công | ✅ |
| `.WithBodyLimit(bytes)` (`Validation/`) | Nới giới hạn body (mặc định 1 MB) cho endpoint nhập file | ✅ |
| `ErrorHttpResult` (`Results/`) | Lỗi nghiệp vụ → ProblemDetails, traceId = X-Request-Id | ✅ |
