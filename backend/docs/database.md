# Cơ sở dữ liệu chuẩn — JP Center ERP

> **Tài liệu sống.** Mỗi khi thêm / sửa / xóa thuộc tính của entity trong `src/Modules/*/Domain/**` thì sửa bảng tương ứng
> ở đây **trong cùng commit**. Nghiệp vụ gốc: [nghiep-vu.md](nghiep-vu.md).

## Quy ước chung

- PostgreSQL, **1 schema / module**. Tên bảng, cột: `snake_case`, bảng số nhiều. Thuộc tính C# `PascalCase` → cột `snake_case`
  (vd `NextFollowUpAt` → `next_follow_up_at`); JSON API dùng `camelCase` khớp type frontend.
- Khóa chính `id` = `uuid` (Guid v7, strongly-typed trong code: `LeadId`, `ClassId`...). Mã hiển thị (`code`) là cột riêng, duy nhất.
- **Tham chiếu sang module khác chỉ lưu Id** (không khóa ngoại xuyên schema) — ghi `→ module.bảng` trong cột Giải thích.
- Enum lưu dạng **chuỗi snake_case** (vd `test_scheduled`), giống giá trị frontend.
- Tiền: `numeric(18,0)` (VND). Ngày: `date`. Giờ học: `time`. Thời điểm: `timestamptz` (UTC).
- Danh sách nhỏ không cần truy vấn riêng: `text[]` / `uuid[]` / `int[]`; cấu trúc lồng: `jsonb`.

**Cột chung — không lặp lại trong từng bảng:**

| Thuộc tính | Kiểu | Giải thích | Có ở bảng |
|---|---|---|---|
| created_at, created_by | timestamptz, uuid | Thời điểm / người tạo — interceptor tự điền | Entity đánh dấu `IAuditable` |
| updated_at, updated_by | timestamptz, uuid | Lần sửa cuối | Entity đánh dấu `IAuditable` |
| is_deleted, deleted_at, deleted_by | bool, timestamptz, uuid | Xóa mềm (vào Thùng rác) | Entity đánh dấu `ISoftDeletable` |
| xmin | xid | Chống sửa đè (concurrency token của PostgreSQL, cột hệ thống) | Mọi bảng aggregate |

Ký hiệu cột **Kiểu**: `?` = được để trống · `PK` khóa chính · `FK` khóa ngoại (cùng schema) · `UQ` duy nhất.

**Bảng chung — mỗi schema module đều có** (ModuleDbContextBase tự thêm, không lặp lại trong từng module):

#### `<schema>.outbox_messages`
**Chức năng:** Sự kiện module gửi cho module khác (Outbox — [shared-helpers.md](shared-helpers.md)). Ghi trong CÙNG giao dịch với dữ liệu
nghiệp vụ; tiến trình nền của module đọc và gửi tới các handler, thử lại khi lỗi. Dữ liệu kỹ thuật (lớp `OutboxMessage` ở Infrastructure.Common).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | Id sự kiện |
| type | varchar(300) | Tên đầy đủ kiểu sự kiện (vd `JPCenter.Modules.Hr.Contracts.IntegrationEvents.EmployeeQuit`) |
| payload | jsonb | Nội dung sự kiện |
| occurred_at | timestamptz | Lúc phát |
| processed_at | timestamptz? | Đã gửi xong cho mọi handler — xóa sau 7 ngày |
| attempts | int | Số lần gửi lỗi |
| next_attempt_at | timestamptz | Lần thử kế tiếp (lùi dần: 10 giây × 2^(lần−1), tối đa 1 giờ) |
| last_error | varchar(2000)? | Lỗi gần nhất (handler nào, thông điệp) |
| handled_by | text[] | Handler đã xử lý xong — thử lại chỉ chạy handler còn lỗi |
| failed_at | timestamptz? | Hết số lần thử (mặc định 8) — ngưng gửi, cần người xem log |

Chỉ mục: `next_attempt_at` (lọc `processed_at IS NULL AND failed_at IS NULL`).

## Mục lục

| Module (schema) | Bảng |
|---|---|
| [System](#module-system--schema-system) | 1 – 13c · chi nhánh, role & quyền, tài khoản, phiên đăng nhập, mã OTP, tích hợp, nhật ký, duyệt 2 người, thùng rác, cài đặt |
| [Crm](#module-crm--schema-crm) | 14 – 24 · lead, tư vấn, liên kết phụ huynh, chia lead, test xếp lớp, lịch hẹn, chiến dịch, tin nhắn |
| [Students](#module-students--schema-students) | 25 – 34 · học viên, ghi danh, chứng nhận, cảnh báo bỏ học, JLPT, du học |
| [Academic](#module-academic--schema-academic) | 35 – 63 · khóa, lộ trình, câu hỏi, phòng, lớp, buổi học, điểm danh, bài tập, thi, đơn từ, lên lớp, flashcard |
| [Library](#module-library--schema-library) | 64 – 67 · thư viện, tài liệu GV |
| [Finance](#module-finance--schema-finance) | 68 – 79 · hóa đơn, thu / hoàn tiền, bảng giá, ưu đãi, đối soát, lương, hoa hồng |
| [Hr](#module-hr--schema-hr) | 80 – 89 · nhân viên, GV, chấm công, đơn từ, tuyển dụng, onboarding |
| [Work](#module-work--schema-work) | 90 – 97 · task, xếp ca, bàn giao ca |
| [Notifications](#module-notifications--schema-notifications) | 98 · thông báo trong app |

**Quy tắc cập nhật:** thêm bảng mới → đánh số tiếp theo cuối module (không đánh lại số cũ, được dùng số lẻ như 24a nếu cần chèn);
thêm / đổi / xóa thuộc tính → sửa dòng trong bảng + ghi 1 dòng ở [Lịch sử thay đổi](#lịch-sử-thay-đổi).

---

## Module System — schema `system`

### 1. branches
**Chức năng:** Chi nhánh của trung tâm; gốc của phân quyền theo chi nhánh. Entity `Branch`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | Id chi nhánh (`BranchId` — dùng ở mọi module) |
| code | text UQ | Mã ngắn: CG, Q1… — 2–4 chữ/số viết hoa, không đổi sau khi tạo |
| name | text | Tên chi nhánh |
| address | text | Địa chỉ |
| phone | text | SĐT |
| email | text | Email |
| manager_id | uuid? | Quản lý chi nhánh → hr.employees |
| open_hours | text | Giờ mở cửa dạng chữ |
| status | text | `preparing` đang chuẩn bị · `active` hoạt động · `closed` đã đóng |
| opened_at | date | Ngày khai trương |
| bank_name, bank_account, bank_holder | text? | Tài khoản nhận học phí (VietQR) |
| invoice_prefix | text UQ | Tiền tố số hóa đơn (thay `{CN}` trong mẫu mã) — 2–4 chữ/số, mặc định = `code`, không trùng chi nhánh khác |
| note | text? | Ghi chú |

### 2. roles
**Chức năng:** Vai trò (Super Admin, Giáo vụ, Sale…) = tập quyền. Entity `Role`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| name | text UQ | Tên vai trò |
| description | text | Mô tả |
| is_system | bool | Role mặc định — không xóa được |
| is_super_admin | bool | Toàn quyền, bỏ qua ma trận quyền |

### 3. role_grants
**Chức năng:** Ma trận quyền của role: mã quyền → phạm vi. Entity con `RoleGrant`. Khóa chính (role_id, permission).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| role_id | uuid FK | → roles |
| permission | text | Mã quyền `module.page.action`, vd `crm.leads.update` (trùng frontend) |
| scope | text | `own` của mình · `branch` chi nhánh được gán · `all` toàn hệ thống |

### 4. user_accounts
**Chức năng:** Tài khoản đăng nhập của nhân viên / giáo viên (1–1 với nhân viên). Entity `UserAccount`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| employee_id | uuid UQ | → hr.employees |
| email | text UQ | Email đăng nhập (chữ thường) |
| password_hash | text? | Mật khẩu đã băm (null khi mới mời) |
| status | text | `invited` đã mời · `active` · `locked` bị khóa |
| last_login_at | timestamptz? | Lần đăng nhập cuối |
| locked_reason | text? | Lý do khóa |
| failed_login_count | int | Số lần nhập sai mật khẩu liên tiếp (về 0 khi đăng nhập đúng / đặt lại mật khẩu) |
| locked_until | timestamptz? | Khóa tạm đến lúc này — sai 5 lần → khóa 15 phút (khác khóa bởi quản trị) |
| must_change_password | bool | Buộc đổi mật khẩu ở lần đăng nhập kế tiếp (mật khẩu tạm) |
| token_version | int | Phiên bản token — tăng khi đổi mật khẩu / khóa / đổi role / quyền lẻ → access token cũ hết hiệu lực ngay |

### 5. user_role_assignments
**Chức năng:** Gán role cho người dùng theo chi nhánh (1 người có thể là Giáo vụ ở CN A, Sale ở CN B). Entity con `UserRoleAssignment`. Khóa chính (user_account_id, role_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| user_account_id | uuid FK | → user_accounts |
| role_id | uuid FK | → roles |
| branch_ids | uuid[] | Chi nhánh áp dụng — rỗng = mọi chi nhánh |

### 6. permission_overrides
**Chức năng:** Cấp thêm / chặn 1 quyền lẻ cho 1 người, có thời hạn. Entity con `PermissionOverride`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| user_account_id | uuid FK | → user_accounts |
| permission | text | Mã quyền |
| effect | text | `grant` cấp thêm · `deny` chặn |
| scope | text? | Phạm vi (chỉ khi grant) |
| until | timestamptz? | Hết hạn — trống = vô thời hạn |
| reason | text | Lý do |
| granted_by | uuid | Người cấp → user_accounts |
| granted_at | timestamptz | Lúc cấp |

### 7. portal_accounts
**Chức năng:** Tài khoản đăng nhập Cổng học viên / phụ huynh. Entity `PortalAccount`. UQ (owner_type, owner_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| owner_type | text | `student` học viên · `guardian` phụ huynh |
| owner_id | uuid | → students.students hoặc crm.leads |
| display_name | text | Tên chủ tài khoản (lưu cứng) — hiển thị ở cổng khi chưa tra được hồ sơ gốc |
| username | text UQ | Tên đăng nhập |
| email | text? | Email |
| password_hash | text? | Mật khẩu đã băm |
| status | text | `active` · `inactive` (đã nghỉ học) · `locked` |
| last_login_at | timestamptz? | Lần đăng nhập cuối |
| failed_login_count | int | Số lần nhập sai mật khẩu liên tiếp (về 0 khi đăng nhập đúng / đặt lại mật khẩu) |
| locked_until | timestamptz? | Khóa tạm đến lúc này — sai 5 lần → khóa 15 phút (khác khóa bởi quản trị) |
| must_change_password | bool | Buộc đổi mật khẩu ở lần đăng nhập kế tiếp (mật khẩu tạm) |
| token_version | int | Phiên bản token (như user_accounts) |

### 8. integrations
**Chức năng:** Kết nối dịch vụ ngoài: Zalo OA, VietQR, Zoom / Meet, email, nguồn lead. Entity `Integration`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| key | text UQ | Khóa cố định: `zalo_oa`, `vietqr`, `zoom`… |
| name | text | Tên hiển thị |
| description | text | Mô tả |
| category | text | `notify` thông báo · `payment` thanh toán · `meeting` học online · `lead` nguồn lead |
| status | text | `connected` · `error` · `disconnected` |
| account | text? | Tài khoản / trang đã kết nối |
| connected_at | timestamptz? | Lúc kết nối |
| connected_by | uuid? | Người kết nối → user_accounts |
| last_sync_at | timestamptz? | Lần đồng bộ cuối |
| last_error | text? | Lỗi gần nhất |
| config | jsonb | Cấu hình key → value (trường bí mật mã hóa) |
| events | jsonb | Sự kiện dùng kênh này: key → bật/tắt |
| webhook_url | text? | URL hệ thống cấp cho bên ngoài gọi vào |

### 9. integration_logs
**Chức năng:** Nhật ký gọi / nhận của từng tích hợp (chỉ thêm). Entity `IntegrationLog`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| integration_id | uuid FK | → integrations |
| at | timestamptz | Thời điểm |
| ok | bool | Thành công? |
| message | text | Nội dung / lỗi |

### 10. audit_logs
**Chức năng:** Nhật ký hệ thống — ai làm gì, lúc nào (chỉ thêm, không sửa / xóa). Entity `AuditLog`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| at | timestamptz | Thời điểm |
| actor_id | uuid? | Người thao tác (null = hệ thống) |
| actor_name | text | Tên người thao tác (lưu cứng) |
| actor_role | text | Vai trò lúc thao tác |
| branch_id | uuid? | Chi nhánh liên quan |
| category | text | `auth` · `permission` · `data` · `export` · `finance` · `setting` · `security` (spam bị tạm khóa, token bị dùng lại) |
| severity | text | `info` · `warning` · `critical` |
| action | text | Hành động ngắn: "Cấp quyền", "Xóa học viên" |
| target | text | Đối tượng bị tác động |
| detail | text? | Chi tiết |
| changes | jsonb | Danh sách `{field, before, after}` |
| ip | text | IP |
| device | text | Thiết bị / trình duyệt |
| trace_id | text? | Mã truy vết request (header X-Request-Id) — nối với log kỹ thuật trong Seq |

### 11. approval_requests
**Chức năng:** Duyệt 2 người (maker–checker) cho hoàn tiền, hủy hóa đơn, cấp quyền tài chính. Người tạo không tự duyệt. Entity `ApprovalRequest`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã hiển thị |
| kind | text | `refund` hoàn tiền · `void_invoice` hủy hóa đơn · `finance_permission` cấp quyền tài chính |
| title | text | Mô tả ngắn: "Hoàn 2.000.000đ — HD-CG-2611-0003" |
| detail | text? | Chi tiết |
| branch_id | uuid? | Chi nhánh |
| payload | jsonb | Dữ liệu thao tác sẽ thực hiện khi duyệt (theo kind) |
| requested_by_id, requested_by_name | uuid, text | Người tạo |
| requested_at | timestamptz | Lúc tạo |
| status | text | `pending` chờ · `approved` duyệt · `rejected` từ chối · `withdrawn` rút lại |
| decided_by_id, decided_by_name | uuid?, text? | Người duyệt / từ chối |
| decided_at | timestamptz? | Lúc quyết định |
| note | text? | Lý do từ chối / ghi chú |

### 12. trash_items
**Chức năng:** Thùng rác — trỏ tới bản ghi đã xóa mềm ở module khác; khôi phục trong N ngày, quá hạn job xóa hẳn. Entity `TrashItem`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| kind | text | Loại dữ liệu `module.entity`, vd `finance.invoice` |
| entity_id | uuid | Id bản ghi gốc |
| label | text | Tên hiển thị |
| detail | text? | Chi tiết |
| branch_id | uuid? | Chi nhánh |
| deleted_at | timestamptz | Lúc xóa |
| deleted_by | uuid | Người xóa |
| purge_after | timestamptz | Xóa hẳn sau thời điểm này |

### 13. settings
**Chức năng:** Cài đặt chung dạng key → giá trị (SLA mặc định, giờ hành chính, mẫu mã hóa đơn, ngày khóa lương, ngưỡng bỏ học, thông báo phụ huynh, ngưỡng hoàn tiền, số ngày thùng rác). Entity `Setting`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| key | text UQ | `nhóm.tên`, vd `crm.defaultSlaMinutes`, `invoice.codePattern`, `atRisk.streak` |
| value | jsonb | Giá trị (số / chuỗi / bool / mảng) |

Khóa đã dùng: `academic.holidays` — mảng `[{ "date": "YYYY-MM-DD", "name": "…" }]`, ngày nghỉ lễ khi sinh lịch buổi học (DT-08.E5),
module khác đọc qua `IHolidayCalendar` (System.Contracts); seeder `SettingsSeeder` thêm nếu chưa có.

### 13a. user_sessions
**Chức năng:** Phiên đăng nhập trên từng thiết bị: giữ refresh token (băm, xoay vòng mỗi lần làm mới), ghi nhớ đăng nhập, danh sách thiết bị, đăng xuất từ xa. Token cũ bị dùng lại → thu hồi mọi phiên của tài khoản. Entity `UserSession`. User story AUTH-02, 03, 08.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| account_kind | text | `staff` (user_accounts) · `portal` (portal_accounts) |
| account_id | uuid | Id tài khoản — index (account_kind, account_id) |
| refresh_token_hash | text UQ | Băm SHA-256 refresh token hiện tại |
| previous_token_hash | text? | Băm token ngay trước — bị dùng lại = nghi đánh cắp |
| device | text | Thiết bị / trình duyệt |
| ip | text | IP lần dùng cuối |
| remember | bool | Có "Ghi nhớ đăng nhập" (refresh 30 ngày; không = theo phiên trình duyệt) |
| created_at | timestamptz | Lúc đăng nhập |
| last_seen_at | timestamptz | Lần làm mới cuối |
| expires_at | timestamptz | Hết hạn refresh token |
| revoked_at | timestamptz? | Lúc thu hồi |
| revoke_reason | text? | `logout` · `logout_others` · `password_changed` · `account_locked` · `token_reuse` · `admin` |

### 13b. one_time_tokens
**Chức năng:** Mã / link dùng 1 lần, chỉ lưu bản băm: link mời kích hoạt, OTP quên mật khẩu, vé đặt mật khẩu mới. Email không tồn tại vẫn tạo bản ghi giả (account_id trống) để phản hồi giống hệt — chống dò email. Entity `OneTimeToken`. User story AUTH-04, 05.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| purpose | text | `invite` (72 giờ) · `password_reset_otp` (5 phút) · `password_reset_grant` (vé đặt mật khẩu, 10 phút) |
| account_kind | text | `staff` · `portal` |
| account_id | uuid? | Tài khoản sở hữu — trống khi email / SĐT không tồn tại |
| recipient | text | Email / SĐT đã nhập (chuẩn hóa) — giới hạn 5 lần gửi / giờ |
| token_hash | text | Băm của mã |
| created_at | timestamptz | Lúc gửi |
| expires_at | timestamptz | Hết hạn |
| attempts | int | Số lần nhập sai |
| max_attempts | int | Tối đa (mặc định 5) |
| used_at | timestamptz? | Đã dùng |
| invalidated_at | timestamptz? | Bị thay bởi mã mới / hủy |

### 13c. idempotency_keys
**Chức năng:** Chống gửi trùng (docs/request-pipeline.md mục 5): lưu kết quả request theo header `Idempotency-Key` 24 giờ —
gửi lại cùng key thì trả response cũ, không chạy lại. Dữ liệu kỹ thuật (lớp `IdempotencyRecord` ở Infrastructure, không có Domain).
Khóa chính (scope, key).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| scope | text | Người gửi: `user:{id tài khoản}` hoặc `ip:{ip}` |
| key | text | Giá trị header Idempotency-Key |
| request_hash | text | SHA-256 nội dung request — cùng key khác nội dung → 422 |
| status_code | int? | Mã HTTP đã trả — trống = đang xử lý |
| body | text? | Nội dung response đã trả |
| content_type | text? | Content-Type response |
| created_at | timestamptz | Lúc nhận |
| expires_at | timestamptz | Hết hạn (24 giờ) — job dọn |

---

## Module Crm — schema `crm`

### 14. leads
**Chức năng:** Lead = người liên hệ (chính người học hoặc phụ huynh). Kanban giai đoạn, phát hiện trùng SĐT, SLA, chuyển thành học viên. Phụ huynh trong hệ thống = lead có liên kết học viên. Entity `Lead`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã hiển thị, vd LD0001 |
| full_name | text | Họ tên |
| phone | text | SĐT (chuẩn hóa) — index để phát hiện trùng |
| email | text? | Email |
| source | text | `facebook` · `website` · `zalo` · `tiktok` · `referral` giới thiệu · `walk_in` đến trực tiếp · `hotline` · `event` |
| status | text | `new` mới · `consulting` đang tư vấn · `test_scheduled` hẹn test · `won` đã chốt · `lost` mất |
| branch_id | uuid | → system.branches |
| assignee_id | uuid? | Sale phụ trách → hr.employees |
| interest | text? | Khóa học quan tâm |
| note | text? | Ghi chú |
| lost_reason | text? | Lý do mất (bắt buộc khi `lost`) |
| next_follow_up_at | timestamptz? | Hẹn gọi lại |
| last_contact_at | timestamptz? | Lần liên hệ cuối |
| hidden | bool | Đã ẩn (học viên liên kết không bị ảnh hưởng) |
| hidden_at | timestamptz? | Lúc ẩn |
| notify_channels | text[] | Kênh nhận thông báo khi là phụ huynh: `zalo` · `sms` · `email` |
| campaign_id | uuid? FK | Chiến dịch mang lead về → campaigns |
| sla_escalated_at | timestamptz? | Đã gửi cảnh báo quá SLA (1 lần) |
| sla_reassigned_at | timestamptz? | Đã tự chuyển Sale do quá 2×SLA (1 lần) |

### 15. consultations
**Chức năng:** Lịch sử tư vấn của lead (gọi, Zalo, gặp…), gồm cả dòng hệ thống tự sinh. Entity con `Consultation`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| lead_id | uuid FK | → leads |
| channel | text | `call` · `zalo` · `meeting` · `message` · `email` |
| result | text | `interested` quan tâm · `thinking` suy nghĩ · `test_booked` hẹn test · `no_answer` không nghe · `not_interested` · `enrolled` đã đăng ký |
| content | text | Nội dung |
| staff_id | uuid? | Người tư vấn → hr.employees (null = hệ thống) |
| staff_name | text | Tên người tư vấn (lưu cứng) |
| created_at | timestamptz | Thời điểm |
| next_follow_up_at | timestamptz? | Hẹn gọi lại |
| is_system | bool | Dòng hệ thống tự sinh |

### 16. lead_student_links
**Chức năng:** Liên kết lead ↔ học viên (nhiều–nhiều): phụ huynh nhiều con, bố mẹ cùng liên hệ 1 con, tự học. Xác định người quyết định / nhận hóa đơn / nhận thông báo. Entity con `LeadStudentLink`. Khóa chính (lead_id, student_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| lead_id | uuid FK | → leads |
| student_id | uuid | → students.students |
| relationship | text | `self` bản thân · `child` con · `sibling` anh chị em · `spouse` vợ/chồng · `other` |
| is_primary | bool | Người liên hệ CHÍNH (mỗi học viên tối đa 1) — người quyết định với HV nhỏ tuổi |
| billing | bool | Nhận hóa đơn & nhắc học phí |
| notify | bool | Nhận thông báo điểm danh / kết quả |
| created_at | timestamptz | Lúc liên kết |

### 17. lead_rules
**Chức năng:** Quy tắc tự chia lead mới cho Sale + SLA liên hệ lần đầu. Chạy theo thứ tự ưu tiên, quy tắc đầu tiên khớp chọn Sale. Entity `LeadRule`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| name | text | Tên quy tắc |
| priority | int | Thứ tự (nhỏ chạy trước) |
| active | bool | Đang bật |
| sources | text[] | Điều kiện nguồn — rỗng = mọi nguồn |
| branch_ids | uuid[] | Điều kiện chi nhánh — rỗng = mọi chi nhánh |
| interests | text[] | Điều kiện khóa quan tâm — rỗng = mọi khóa |
| method | text | `round_robin` xoay vòng · `least_load` ít lead mở nhất · `fixed` cố định |
| sale_ids | uuid[] | Nhóm Sale nhận → hr.employees |
| only_on_shift | bool | Chỉ chia cho Sale đang trong ca |
| sla_minutes | int | Hạn liên hệ lần đầu (phút) |
| escalate_to_ids | uuid[] | Quá SLA báo cho ai → hr.employees |
| reassign_on_breach | bool | Quá 2×SLA tự chuyển Sale khác |
| cursor | int | Con trỏ xoay vòng |

### 18. lead_assignment_logs
**Chức năng:** Nhật ký chia lead — giải thích vì sao chọn Sale (chỉ thêm). Entity `LeadAssignmentLog`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| lead_id | uuid FK | → leads |
| sale_id | uuid? | Sale được giao → hr.employees (null = chưa chia được) |
| rule_id | uuid? FK | Quy tắc áp dụng → lead_rules |
| at | timestamptz | Thời điểm |
| reason | text | Giải thích |

### 19. placement_tests
**Chức năng:** Bài test xếp lớp: bốc câu đã duyệt từ ngân hàng theo cấp độ + kỹ năng, thang điểm gợi ý trình độ / khóa. Entity `PlacementTest`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã bài test |
| name | text | Tên |
| description | text? | Mô tả |
| sections | jsonb | Cấu trúc đề: `[{level, skill, count}]` |
| question_ids | uuid[] | Câu đã bốc (cố định) → academic.questions |
| duration_minutes | int | Thời gian làm bài |
| bands | jsonb | Thang gợi ý: `[{minPct, level, course}]` |
| status | text | `draft` nháp · `active` đang dùng · `archived` lưu trữ |
| author_id | uuid | Người soạn → hr.employees |

### 20. placement_attempts
**Chức năng:** 1 lượt lead làm test online qua link có hạn; tự chấm, gợi ý trình độ, ghi vào lịch sử tư vấn. Entity `PlacementAttempt`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| test_id | uuid FK | → placement_tests |
| lead_id | uuid FK | → leads |
| token | text UQ | Mã bí mật trong link làm bài |
| channel | text | Kênh gửi link: `zalo` · `sms` · `email` |
| sent_by | uuid | Người gửi → hr.employees |
| sent_at | timestamptz | Lúc gửi |
| expires_at | timestamptz | Hạn làm |
| status | text | `sent` đã gửi · `in_progress` đang làm · `done` đã nộp · `expired` hết hạn |
| started_at | timestamptz? | Lúc bắt đầu |
| submitted_at | timestamptz? | Lúc nộp |
| answers | jsonb | questionId → đáp án chọn (-1 = bỏ trống) |
| correct | int? | Số câu đúng |
| total | int? | Tổng số câu |
| by_skill | jsonb | Kết quả theo kỹ năng `{skill: {correct, total}}` |
| suggested_level | text? | Trình độ gợi ý |
| suggested_course | text? | Khóa gợi ý |
| logged | bool | Đã ghi vào lịch sử tư vấn |

### 21. appointments
**Chức năng:** Lịch hẹn lead đến tư vấn / làm test tại trung tâm; kiểm tra trùng phòng, người tiếp, lead; nhắc trước 1 giờ. Entity `Appointment`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã lịch hẹn |
| lead_id | uuid FK | → leads |
| kind | text | `consult` tư vấn · `test` làm test |
| branch_id | uuid | → system.branches |
| room_id | uuid? | Phòng → academic.rooms |
| staff_id | uuid | Người tiếp / coi test → hr.employees |
| start, end | timestamptz | Thời gian hẹn |
| status | text | `scheduled` đã hẹn · `done` đã đến · `no_show` không đến · `cancelled` hủy |
| note | text? | Ghi chú |
| outcome | text? | Kết quả sau buổi hẹn |
| reminded_at | timestamptz? | Đã nhắc trước 1 giờ |
| cancel_reason | text? | Lý do hủy |

### 22. campaigns
**Chức năng:** Chiến dịch marketing: kênh, ngân sách, chỉ tiêu, mã UTM; đo số lead, tỷ lệ chốt, chi phí / lead. Entity `Campaign`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã chiến dịch |
| name | text | Tên |
| channel | text | Kênh chạy (= nguồn lead) |
| branch_ids | uuid[] | Chi nhánh áp dụng |
| start_date, end_date | date | Thời gian chạy |
| budget | numeric | Ngân sách |
| spent | numeric | Đã chi |
| target_leads | int? | Chỉ tiêu số lead |
| status | text | `planned` · `running` · `paused` · `ended` |
| owner_id | uuid | Người phụ trách → hr.employees |
| utm | text? | Mã UTM — lead từ form tự gắn chiến dịch |
| course_ids | uuid[] | Khóa quảng bá → academic.courses |
| note | text? | Ghi chú |

### 23. message_templates
**Chức năng:** Mẫu tin Zalo / SMS theo tình huống, tự điền biến `{ten}`, `{lop}`, `{so_tien}`. Entity `MessageTemplate`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| scenario | text | `welcome` · `test_reminder` · `consult_reminder` · `tuition` · `class_off` · `arrival` · `absent_alert` · `weekly_report` · `custom` |
| name | text | Tên mẫu |
| channel | text | Kênh gợi ý: `zalo` · `sms` |
| body | text | Nội dung có biến |
| active | bool | Đang dùng |

### 24. message_logs
**Chức năng:** Nhật ký tin đã gửi cho lead / học viên / phụ huynh (chỉ thêm). Entity `MessageLog`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| channel | text | `zalo` · `sms` · `email` |
| scenario | text | Tình huống |
| template_id | uuid? FK | → message_templates |
| to_name | text | Tên người nhận |
| to_phone | text | SĐT người nhận |
| lead_id | uuid? FK | → leads |
| student_id | uuid? | → students.students |
| body | text | Nội dung thực tế đã gửi |
| sent_by | uuid? | Người gửi → hr.employees (null = hệ thống) |
| sent_at | timestamptz | Lúc gửi |
| auto | bool | Hệ thống tự gửi |

---

## Module Students — schema `students`

### 25. students
**Chức năng:** Hồ sơ học viên (đã đăng ký học). Lớp đang học, trạng thái học, học phí suy ra từ ghi danh / hóa đơn. Phụ huynh = lead liên kết (crm.lead_student_links). Entity `Student`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã học viên, vd HV1000 |
| full_name | text | Họ tên |
| gender | text? | `male` · `female` · `other` |
| dob | date? | Ngày sinh — xác định HV nhỏ tuổi (phụ huynh quyết định) |
| phone | text? | SĐT |
| email | text? | Email |
| address | text? | Địa chỉ |
| note | text? | Ghi chú nội bộ (không trả về cổng) |
| branch_id | uuid | → system.branches |
| joined_at | date | Ngày vào trung tâm |
| hidden | bool | Đã ẩn (chỉ khi không còn ghi danh đang học / bảo lưu) |
| hidden_at | timestamptz? | Lúc ẩn |

### 26. enrollments
**Chức năng:** Ghi danh học viên vào lớp — sinh hóa đơn; theo dõi trạng thái học trong lớp (bảo lưu, chuyển lớp, nghỉ, hoàn thành). Entity `Enrollment`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid FK | → students |
| class_id | uuid | → academic.classes |
| branch_id | uuid | → system.branches |
| status | text | `studying` đang học · `reserved` bảo lưu · `completed` hoàn thành · `transferred` đã chuyển lớp · `dropped` nghỉ |
| enrolled_at | date | Ngày ghi danh |
| ended_at | date? | Ngày kết thúc |
| note | text? | Ghi chú / lý do nghỉ |
| reserved_until | date? | Bảo lưu đến ngày |
| transferred_to_id | uuid? FK | Chuyển sang ghi danh nào → enrollments |
| transferred_from_id | uuid? FK | Chuyển từ ghi danh nào → enrollments |
| promotion_candidate_id | uuid? | Tạo từ đề xuất lên lớp → academic.promotion_candidates |

### 27. certificates
**Chức năng:** Chứng nhận hoàn thành khóa do trung tâm cấp (khác JLPT): mã tra cứu + QR, PDF, gửi email, cấp ngoại lệ. Entity con `Certificate` (1–1 với ghi danh).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| enrollment_id | uuid FK UQ | → enrollments |
| code | text UQ | Mã tra cứu, vd CN-2026-N3K12-0007 |
| issued_at | timestamptz | Lúc cấp |
| sent_to | text | Email đã gửi (lần gần nhất) |
| sent_by | uuid | Người gửi → hr.employees |
| send_count | int | Số lần gửi |
| exception | text? | Lý do cấp ngoại lệ (chưa đủ điều kiện) |
| file_key | text? | File PDF trong kho file |

### 28. care_cases
**Chức năng:** Cảnh báo bỏ học của 1 học viên trong 1 lớp (vắng nhiều, điểm thấp, nợ quá hạn…): mức rủi ro, người chăm sóc, trạng thái, hẹn gọi lại. Entity `CareCase`. UQ (class_id, student_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid FK | → students |
| class_id | uuid | → academic.classes |
| branch_id | uuid | → system.branches |
| risk_level | text | `low` · `medium` · `high` (lần tính gần nhất) |
| flags | text[] | Dấu hiệu: vắng liên tiếp, chuyên cần thấp, không nộp bài, điểm thấp, nợ quá hạn… |
| assignee_id | uuid? | Người chăm sóc → hr.employees |
| status | text | `new` chưa liên hệ · `contacting` đang liên hệ · `monitoring` theo dõi · `resolved` ổn định |
| next_at | date? | Hẹn liên hệ lại |
| detected_at | timestamptz | Lúc phát hiện |

### 29. care_logs
**Chức năng:** Nhật ký liên hệ chăm sóc học viên / phụ huynh — chỉ nhân viên xem, **không** trả về cổng GV. Entity `CareLog`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid FK | → students |
| class_id | uuid | → academic.classes |
| branch_id | uuid | → system.branches |
| channel | text | `call` · `zalo` · `meet` |
| note | text | Nội dung |
| by, by_name | uuid, text | Người liên hệ → hr.employees |
| at | timestamptz | Thời điểm |

### 30. jlpt_sessions
**Chức năng:** Kỳ thi JLPT (tháng 7 & 12): ngày thi, hạn nhận hồ sơ, ngày có kết quả, lệ phí, địa điểm. Entity `JlptSession`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| label | text | "Tháng 12/2026" |
| exam_date | date | Ngày thi |
| registration_close | date | Hạn trung tâm nhận hồ sơ |
| result_date | date | Ngày có kết quả |
| fees | jsonb | Lệ phí theo cấp độ `{N1: 700000, …}` |
| venues | text[] | Địa điểm thi |

### 31. jlpt_registrations
**Chức năng:** Học viên đăng ký thi JLPT qua trung tâm: hồ sơ, lệ phí, nộp hội đồng, phiếu dự thi, điểm, nhận & phát chứng chỉ gốc. Entity `JlptRegistration`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid FK | → students |
| session_id | uuid FK | → jlpt_sessions |
| branch_id | uuid | → system.branches |
| level | text | `N5` … `N1` |
| venue | text | Địa điểm thi |
| status | text | `registered` · `paid` đóng phí · `submitted` nộp hội đồng · `admit` có phiếu dự thi · `done` có kết quả · `absent` vắng · `cancelled` hủy |
| doc_photo, doc_id_card, doc_form | bool | Đã nộp ảnh 3×4, CCCD, phiếu ĐK đã ký |
| fee | numeric | Lệ phí |
| paid_at | timestamptz? | Lúc đóng phí |
| admit_no | text? | Số báo danh |
| scores | int[] | Điểm từng phần |
| passed | bool? | Đỗ / trượt |
| cert_no | text? | Số chứng chỉ gốc |
| cert_received_at, cert_received_by | timestamptz?, uuid? | Chứng chỉ về trung tâm |
| cert_handed_at, cert_handed_by | timestamptz?, uuid? | Đã phát cho học viên |
| cert_handed_to | text? | Người nhận (HV / phụ huynh) |
| note | text? | Ghi chú |

### 32. abroad_cases
**Chức năng:** Hồ sơ du học / xuất khẩu lao động: chương trình, đối tác, giai đoạn (tư vấn → giấy tờ → phỏng vấn → COE → visa → xuất cảnh), phí. Entity `AbroadCase`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid FK | → students |
| branch_id | uuid | → system.branches |
| program | text | `study` du học · `tokutei` · `tts` thực tập sinh · `engineer` kỹ sư |
| partner | text | Trường / nghiệp đoàn / công ty tiếp nhận |
| destination | text | Tỉnh / thành ở Nhật |
| job | text? | Ngành nghề (XKLĐ) |
| intake | text | Kỳ nhập học / xuất cảnh dự kiến `YYYY-MM` |
| jlpt_required | text? | Yêu cầu tiếng Nhật |
| stage | text | `consult` · `documents` · `interview` · `coe` · `visa` · `departed` |
| cancelled_at | timestamptz? | Lúc hủy hồ sơ |
| cancel_reason | text? | Lý do hủy |
| interview_date | date? | Ngày phỏng vấn |
| interview_result | text? | `pass` · `fail` |
| coe_submitted_at, coe_granted_at | date? | Nộp / nhận COE |
| visa_applied_at, visa_issued_at | date? | Nộp / nhận visa |
| flight_date | date? | Ngày bay |
| fee_total | numeric | Tổng phí |
| fee_paid | numeric | Đã đóng |
| assignee_id | uuid | Người phụ trách → hr.employees |

### 33. abroad_documents
**Chức năng:** Checklist giấy tờ của hồ sơ du học. Entity con `AbroadDocument`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| abroad_case_id | uuid FK | → abroad_cases |
| name | text | Tên giấy tờ |
| state | text | `missing` chưa có · `received` đã nhận · `verified` đã kiểm tra |

### 34. abroad_notes
**Chức năng:** Ghi chú tiến trình hồ sơ du học. Entity con `AbroadNote`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| abroad_case_id | uuid FK | → abroad_cases |
| text | text | Nội dung |
| by, by_name | uuid, text | Người ghi |
| at | timestamptz | Thời điểm |

---

## Module Academic — schema `academic`

### 35. courses
**Chức năng:** Khóa học = sản phẩm bán (JLPT N3 nhóm offline GV Việt…). Lớp mở theo khóa. Entity `Course`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã khóa |
| name | text | Tên khóa |
| category | text | `jlpt` · `kaiwa` giao tiếp · `kids` thiếu nhi · `business` · `xkld` |
| level_from | text | Trình độ đầu vào: `beginner`, `N5` … `N1` |
| level_to | text | Trình độ đầu ra |
| format | text | `group` lớp nhóm · `private` 1 kèm 1 |
| teacher_type | text | `vn` GV Việt · `jp` GV Nhật · `mixed` kết hợp (quyết định tiền tố mã lớp) |
| mode | text | `offline` · `online` |
| sessions | int | Tổng số buổi |
| minutes_per_session | int | Phút / buổi |
| sessions_per_week | int | Buổi / tuần |
| fee | numeric | Nhóm: giá trọn khóa / HV · 1-1: giá mỗi buổi |
| max_students | int? | Sĩ số tối đa (chỉ lớp nhóm) |
| branch_ids | uuid[] | Chi nhánh được bán |
| description | text? | Mô tả |
| status | text | `draft` · `active` đang bán · `paused` tạm ngừng |

### 36. course_curricula
**Chức năng:** Khóa ↔ lộ trình (nhiều–nhiều); mỗi khóa đúng 1 lộ trình mặc định. Entity con `CourseCurriculum`. Khóa chính (course_id, curriculum_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| course_id | uuid FK | → courses |
| curriculum_id | uuid FK | → curricula |
| is_default | bool | Lộ trình mặc định cho lớp mở mới |
| linked_at | timestamptz | Lúc gắn |

### 37. curricula
**Chức năng:** Lộ trình = nội dung giảng dạy, có phiên bản (đã phát hành thì khóa nội dung). Entity `Curriculum`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text | Mã lộ trình (UQ cùng version) |
| name | text | Tên |
| level | text | Trình độ |
| tier | text | `standard` chuẩn · `advanced` nâng cao |
| entry_requirement | text? | Điều kiện áp dụng (bậc nâng cao) |
| version | text | Phiên bản dạng `v1.0`, `v1.1` (giống frontend) |
| previous_version_id | uuid? FK | Bản trước → curricula |
| textbook | text? | Giáo trình |
| description | text? | Mô tả |
| tags | text[] | Thẻ |
| status | text | `draft` nháp · `published` đã phát hành · `archived` |
| author_id | uuid | Người soạn → hr.employees |

### 38. lessons
**Chức năng:** Bài học trong lộ trình, có thứ tự. Entity con `Lesson`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| curriculum_id | uuid FK | → curricula |
| title | text | Tên bài |
| order | int | Thứ tự |
| has_quiz | bool | Có bài kiểm tra nhỏ cuối bài |

### 39. chapters
**Chức năng:** Phần nhỏ trong bài (từ vựng, ngữ pháp, nghe…) — GV mở buổi thấy chapter cần dạy. Entity con `Chapter`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| lesson_id | uuid FK | → lessons |
| title | text | Tên phần |
| order | int | Thứ tự |

### 40. materials
**Chức năng:** Tài liệu giảng dạy của chapter: **file** trong kho (`IFileStorage` — ADR 0003: pdf, Word, ppt, Excel, audio) hoặc **link** (video, Drive, web). Entity con `Material`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| chapter_id | uuid FK | → chapters |
| name | text | Tên tài liệu |
| type | text | `pdf` · `doc` · `ppt` · `xls` · `audio` · `video` · `drive` · `link` |
| url | text? | Link ngoài — chỉ tài liệu dạng link (`video`, `drive`, `link`) |
| file_key | text? | Khóa file trong kho — chỉ tài liệu dạng file; bản sao / phiên bản mới **dùng chung** key (xóa file khi không còn tài liệu nào trỏ tới) |
| file_name | text? | Tên tệp gốc (tải về đúng tên) |
| content_type | text? | Loại nội dung của file |
| size | bigint? | Byte (file tải lên) |
| shared_with_students | bool | HV xem được trên cổng; false = chỉ GV (giáo án, đáp án) |

### 41. exam_milestones
**Chức năng:** Mốc thi giữa / cuối kỳ trong lộ trình, thi sau 1 bài; chỉ đánh dấu, đề tạo khi tổ chức thi từ ngân hàng câu hỏi. Entity con `ExamMilestone`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| curriculum_id | uuid FK | → curricula |
| kind | text | `midterm` giữa kỳ · `final` cuối kỳ |
| title | text | Tên mốc thi |
| after_lesson_id | uuid FK | Thi sau bài này → lessons |
| note | text? | Ghi chú cho người tạo kỳ thi |

### 42. questions
**Chức năng:** Ngân hàng câu hỏi — nguồn duy nhất cho quiz, kỳ thi, test xếp lớp; chỉ câu đã duyệt được bốc. Entity `Question`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã câu |
| level | text | `N5` … `N1` |
| skill | text | `vocab` · `kanji` · `grammar` · `reading` · `listening` |
| format | text | `choice` chọn 1/4 · `star` sắp xếp ★ |
| difficulty | text | `easy` · `medium` · `hard` |
| stem | text | Đề bài (furigana `漢字{かんじ}`) |
| passage | text? | Đoạn văn đọc hiểu |
| audio_key | text? | File nghe trong kho file |
| audio_duration_sec | int? | Độ dài file nghe |
| transcript | text? | Lời thoại — chỉ GV / NV xem |
| options | text[] | Các đáp án |
| answer | int | Vị trí đáp án đúng (0-based) — không trả cổng HV trước khi nộp |
| explanation | text? | Giải thích |
| curriculum_id | uuid? FK | → curricula |
| lesson_ids | uuid[] | Bài học gắn (để bốc theo bài) |
| tags | text[] | Thẻ |
| status | text | `draft` · `review` chờ duyệt · `approved` đã duyệt · `retired` ngừng dùng |
| author_id | uuid | Người soạn → hr.employees |
| reviewed_by | uuid? | Người duyệt (khác người soạn) |
| reviewed_at | timestamptz? | Lúc duyệt / trả lại |
| review_note | text? | Lý do trả lại |
| used_count | int | Số đề đã dùng câu này |
| attempt_count | int | Lượt làm |
| correct_count | int | Lượt đúng |
| picks | int[] | Số lần chọn từng đáp án |

### 43. rooms
**Chức năng:** Phòng học theo chi nhánh; lịch sử dụng / lấp đầy / trùng lịch suy ra từ buổi học. Entity `Room`. UQ (branch_id, name).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| branch_id | uuid | → system.branches |
| name | text | Tên phòng, vd P.301 |
| floor | text? | Tầng / khu |
| type | text | `classroom` · `lab` · `workshop` · `meeting` |
| capacity | int | Sức chứa |
| area | numeric? | Diện tích m² |
| equipment | text[] | `projector` · `tv` · `whiteboard` · `aircon` · `speaker` · `mic` · `camera` · `computer` |
| status | text | `active` · `maintenance` bảo trì · `inactive` ngừng |
| maintenance_reason | text? | Lý do bảo trì |
| maintenance_until | date? | Dự kiến xong |
| note | text? | Ghi chú |

### 44. room_issues
**Chức năng:** Báo hư hỏng phòng / thiết bị: mở → đang sửa → đã xong; mức cao gợi ý đổi phòng. Entity `RoomIssue`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| room_id | uuid FK | → rooms |
| equipment | text? | Thiết bị hỏng (null = cơ sở vật chất khác) |
| title | text | Tiêu đề |
| description | text? | Mô tả |
| severity | text | `low` · `medium` · `high` (không dạy được) |
| status | text | `open` · `in_progress` · `resolved` |
| reported_by | uuid | Người báo → hr.employees |
| reported_at | timestamptz | Lúc báo |
| assignee | text? | Người / đơn vị sửa |
| due_date | date? | Hẹn xong |
| started_at | timestamptz? | Bắt đầu sửa |
| resolved_at | timestamptz? | Sửa xong |
| resolution | text? | Đã làm gì |
| cost | numeric? | Chi phí sửa |

### 45. classes
**Chức năng:** Lớp học mở theo khóa, chốt lộ trình; GV, phòng, lịch lặp, sĩ số, học phí; vòng đời chờ khai giảng → đang học ⇄ tạm dừng → kết thúc / hủy. Entity `Class`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| course_id | uuid FK | → courses |
| curriculum_id | uuid? FK | Lộ trình chốt lúc mở lớp → curricula |
| code | text UQ | Mã lớp, vd GVV_N3-K12 |
| name | text | Tên lớp |
| branch_id | uuid | → system.branches |
| teacher_ids | uuid[] | 1–2 GV → hr.employees |
| assistant_ids | uuid[] | Trợ giảng → hr.employees |
| room_id | uuid? FK | Phòng mặc định → rooms |
| online | bool | Lớp online |
| meeting_url | text? | Link Zoom / Meet |
| planned_start_date, planned_end_date | date | Dự kiến khai giảng / kết thúc |
| actual_start_date, actual_end_date | date? | Thực tế |
| weekdays | int[] | Thứ học: 1 = T2 … 7 = CN |
| start_time, end_time | time | Giờ học |
| total_hours | numeric | Tổng giờ → số buổi = tổng giờ ÷ giờ mỗi buổi |
| min_students, max_students | int | Sĩ số tối thiểu / tối đa |
| fee | numeric | Học phí / HV |
| status | text | `enrolling` chờ khai giảng · `ongoing` · `paused` · `finished` · `cancelled` |
| previous_class_id | uuid? FK | Lớp trước (lên lớp) → classes |
| pause_reason | text? | Lý do tạm dừng (xóa khi học lại) |
| cancel_reason | text? | Lý do hủy |
| note | text? | Ghi chú |

### 46. class_sessions
**Chức năng:** Buổi học (thường / học bù / học thêm): dời lịch, hủy, đổi GV / phòng cho riêng buổi, QR điểm danh, sự cố GV, báo cáo buổi, xác nhận đã dạy (căn cứ lương GV). Entity `ClassSession`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| class_id | uuid FK | → classes |
| branch_id | uuid | → system.branches |
| no | int | Số thứ tự buổi (học thêm = 0, không thuộc chuỗi) |
| date | date | Ngày học |
| start_time, end_time | time | Giờ học |
| kind | text | `lesson` học · `exam` thi |
| lesson_id | uuid? | Bài dự kiến → lessons (không ràng buộc FK — bài thuộc lộ trình, bản nháp xóa được bài) |
| exam_id | uuid? | Mốc thi → exam_milestones (không ràng buộc FK) |
| status | text | `scheduled` chưa xác nhận (sắp tới / đang diễn ra / chờ xác nhận — suy theo giờ) · `completed` đã xác nhận · `cancelled` |
| origin | text | `regular` · `makeup` học bù · `extra` học thêm |
| makeup_for_id | uuid? FK | Buổi bị hủy được bù → class_sessions |
| teacher_ids | uuid[] | Ghi đè GV cho buổi (dạy thay) — rỗng = theo lớp |
| assistant_ids | uuid[]? | Ghi đè trợ giảng — null = theo lớp |
| room_id | uuid? FK | Đổi phòng cho buổi → rooms |
| attendance_qr_token | text UQ | Mã QR điểm danh (cố định theo buổi) |
| note | text? | Lý do hủy / ghi chú |
| rescheduled_from_date, rescheduled_from_start, rescheduled_from_end | date?, time? | Lịch gốc trước khi dời |
| reschedule_reason | text? | Lý do dời |
| cancelled_by, cancelled_at | uuid?, timestamptz? | Ai hủy, lúc nào |
| attendance_by, attendance_at | uuid?, timestamptz? | Người điểm danh lần cuối |
| confirmed_by, confirmed_at | uuid?, timestamptz? | Nhân viên xác nhận buổi đã dạy |
| confirm_note | text? | Ghi chú xác nhận |
| incident_at | time? | Giờ xảy ra sự cố GV |
| incident_reason | text? | `faint` · `sick` · `family` · `accident` · `tech` · `other` |
| incident_detail | text? | Chi tiết |
| incident_action | text? | `pause` tạm dừng · `substitute` GV khác dạy · `assistant` trợ giảng dạy · `end_early` kết thúc sớm |
| incident_takeover_id | uuid? | Người dạy tiếp → hr.employees |
| incident_notified | bool? | Đã báo HV / PH |
| incident_reported_by, incident_reported_at | uuid?, timestamptz? | Người báo sự cố |
| incident_outcome | text? | `resumed` học tiếp · `ended` cho nghỉ |
| incident_outcome_at, incident_outcome_teacher_id, incident_outcome_by | timestamptz?, uuid?, uuid? | Kết quả xử lý |
| report_content | text? | Báo cáo GV: đã dạy gì, lớp tiếp thu thế nào |
| report_homework | text? | Bài tập về nhà |
| report_issues | text? | Vấn đề cần trung tâm xử lý |
| report_coverage | text? | `full` xong bài · `partial` một phần · `none` chưa dạy |
| report_covered_chapter_ids | uuid[]? | Chapter đã dạy xong |
| report_ended_at | timestamptz? | Lúc GV bấm "Kết thúc buổi học" lần đầu |
| report_author_id, report_updated_at | uuid?, timestamptz? | Người viết, lần sửa cuối |

### 47. attendance_records
**Chức năng:** Điểm danh từng học viên trong buổi (QR / GV / nhân viên), về sớm, vắng theo đơn, học bù. Entity con `AttendanceRecord`. Khóa chính (class_session_id, student_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| class_session_id | uuid FK | → class_sessions |
| student_id | uuid | → students.students |
| status | text | `present` · `late` (tính có mặt) · `excused` vắng có phép · `absent` vắng không phép |
| note | text? | Ghi chú |
| check_in_at | time? | Giờ vào lớp |
| method | text? | `qr` · `teacher` · `staff` |
| left_at | time? | Về sớm lúc |
| leave_reason | text? | Lý do về sớm |
| leave_request_id | uuid? FK | Vắng theo đơn xin nghỉ → leave_requests |
| made_up_session_id | uuid? FK | Buổi vắng đã học bù ở buổi này → class_sessions |
| made_up_status | text? | Kết quả buổi học bù (`present` / `late`) |
| makeup_for_session_id | uuid? FK | HV lớp KHÁC đến học bù: buổi gốc bị vắng (không thuộc sĩ số) |

### 48. teacher_attendances
**Chức năng:** Chấm công GV / trợ giảng theo buổi (GV không chấm công văn phòng) — căn cứ lương GV. Entity con `TeacherAttendance`. Khóa chính (class_session_id, teacher_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| class_session_id | uuid FK | → class_sessions |
| teacher_id | uuid | → hr.employees |
| role | text | `teacher` GV lớp · `substitute` dạy thay · `assistant` trợ giảng |
| source | text | `self` GV tự chấm · `staff` giáo vụ chấm hộ |
| checked_at | timestamptz | Lần chấm cuối |
| status | text | `present` · `late` · `absent` |
| check_in, check_out | time? | Giờ vào / ra |
| substitute_id | uuid? | GV vắng → người dạy thay |
| note | text? | Ghi chú |

### 49. session_support_entries
**Chức năng:** Nhật ký hỗ trợ của nhân viên trong buổi (điểm danh hộ, HV về sớm, sự cố, phòng…). Entity con `SupportEntry`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| class_session_id | uuid FK | → class_sessions |
| kind | text | `attendance` · `early_leave` · `teacher_checkin` · `incident` · `room` · `other` |
| note | text | Nội dung |
| by | uuid | Nhân viên → hr.employees |
| at | timestamptz | Thời điểm |

### 50. assignments
**Chức năng:** Bài tập giao cho lớp: `upload` (HV nộp file, GV chấm) hoặc `quiz` (bốc câu từ ngân hàng theo tiêu chí, tự chấm). Entity `Assignment`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| class_id | uuid FK | → classes |
| type | text | `upload` · `quiz` |
| title | text | Tiêu đề |
| lesson_id | uuid? FK | Bài học liên quan → lessons |
| description | text? | Mô tả |
| assigned_at | timestamptz | Lúc giao |
| due_date | timestamptz | Hạn nộp |
| max_score | numeric | Thang điểm (mặc định 10) |
| quiz | jsonb? | Tiêu chí bốc câu: `{lessonIds, skills, difficulty, questionCount, timeLimitMinutes, attempts, shuffle}` |
| attachments | jsonb | Đề / tài liệu GV gửi: `[{name, kind, url, size}]` |
| created_by_teacher_id | uuid | GV tạo → hr.employees |

### 51. submissions
**Chức năng:** Bài nộp của 1 HV cho 1 bài tập; quiz giữ lần điểm cao nhất; GV chấm + nhận xét. Entity `Submission`. UQ (assignment_id, student_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| assignment_id | uuid FK | → assignments |
| student_id | uuid | → students.students |
| submitted_at | timestamptz | Lúc nộp |
| files | jsonb | Tệp nộp (bài upload) |
| content | text? | Bài làm / lời nhắn dạng chữ |
| quiz_correct, quiz_total | int? | Kết quả quiz lần cao nhất |
| quiz_attempts | int | Số lần đã làm |
| quiz_duration_minutes | int? | Thời gian làm |
| score | numeric? | Điểm |
| comment | text? | Nhận xét của GV |
| graded_at | timestamptz? | Lúc chấm |
| graded_by | uuid? | Người chấm → hr.employees |

### 52. exam_results
**Chức năng:** Kết quả 1 HV trong 1 kỳ thi (mốc thi của lộ trình) của lớp: điểm từng phần, vắng thi. Entity `ExamResult`. UQ (class_id, exam_id, student_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| class_id | uuid FK | → classes |
| exam_id | uuid FK | → exam_milestones |
| student_id | uuid | → students.students |
| status | text | `done` đã thi · `absent` vắng |
| sections | jsonb | Điểm từng phần `[{name, score, max}]` |
| total_score | numeric | Tổng điểm |
| max_score | numeric | Điểm tối đa |
| started_at | timestamptz? | Lúc bắt đầu |
| duration_minutes | int? | Thời gian làm |
| comment | text? | Nhận xét |

> Chưa có bảng "kỳ thi" (đề cụ thể bốc từ ngân hàng) vì frontend chưa dựng trang tạo kỳ thi — khi làm sẽ thêm `class_exams`.

### 53. class_announcements
**Chức năng:** Thông báo cho lớp (HV / PH / GV) qua cổng / Zalo / email; lọc HV nợ phí; hẹn giờ; đếm lượt xem. Entity `ClassAnnouncement`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| class_id | uuid FK | → classes |
| title | text | Tiêu đề |
| content | text | Nội dung |
| audience | text[] | `student` · `parent` · `teacher` |
| channels | text[] | `portal` · `zalo` · `email` |
| debtors_only | bool | Chỉ gửi HV còn nợ học phí |
| student_ids | uuid[] | Chỉ gửi các HV này — rỗng = cả lớp |
| recipients | int | Số người nhận lúc gửi |
| read_count | int | Số người đã xem |
| sent_by | uuid | Người gửi → hr.employees |
| sent_at | timestamptz? | Lúc gửi (null = chờ hẹn giờ) |
| scheduled_at | timestamptz? | Hẹn giờ gửi |

### 54. teacher_reviews
**Chức năng:** HV đánh giá GV (khảo sát giữa / cuối kỳ), ẩn danh được. Entity `TeacherReview`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| class_id | uuid FK | → classes |
| teacher_id | uuid | → hr.employees |
| student_id | uuid | → students.students (ẩn với GV nếu anonymous) |
| period | text | `mid` giữa kỳ · `final` cuối kỳ |
| overall | int | Tổng 1–5 sao |
| method, clarity, enthusiasm, punctuality | int | Phương pháp · dễ hiểu · nhiệt tình · đúng giờ (1–5) |
| comment | text? | Góp ý |
| anonymous | bool | Ẩn tên HV với GV |
| created_at | timestamptz | Lúc đánh giá |

### 55. student_evaluations
**Chức năng:** Nhận xét CUỐI KHÓA của GV về từng HV — dành cho Sale tư vấn khóa tiếp, **không** hiện cho HV / PH. Entity `StudentEvaluation`. UQ (class_id, student_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| class_id | uuid FK | → classes |
| student_id | uuid | → students.students |
| teacher_id | uuid | GV viết → hr.employees |
| vocab, grammar, reading, listening, speaking | int | Kỹ năng 1–5 sao |
| attitude | int | Thái độ, chuyên cần 1–5 |
| strengths | text? | Điểm mạnh |
| weaknesses | text? | Điểm yếu |
| recommendation | text | `next_level` · `advanced` · `jlpt` · `repeat` học lại · `other` |
| advice | text? | Ghi chú cho Sale (lớp phù hợp, lịch học, lưu ý) |

### 56. promotion_candidates
**Chức năng:** Lên lớp: giữ chỗ tạm HV lớp sắp kết thúc vào lớp kỳ sau → giao Sale tư vấn người quyết định → chốt (ghi danh) / từ chối; hết hạn tự trả chỗ. Entity `PromotionCandidate`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid | → students.students |
| branch_id | uuid | → system.branches |
| from_class_id | uuid FK | Lớp đang học → classes |
| to_class_id | uuid? FK | Lớp đề xuất lên → classes |
| seat_held | bool | Đang giữ chỗ tạm (không tính sĩ số, không hóa đơn) |
| hold_until | date? | Hạn giữ chỗ |
| status | text | `pending` · `consulting` · `unreachable` không liên lạc được · `confirmed` · `declined` |
| source | text | `teacher` GV đề xuất · `stats` số liệu · `manual` thêm tay |
| handed_over_at | timestamptz? | Lúc giao Sale |
| sale_id | uuid? | Sale phụ trách → hr.employees |
| interest_at | timestamptz? | HV / PH bấm "Quan tâm" |
| interest_by | text? | `student` · `parent` |
| decline_reason | text? | Lý do không học tiếp |
| enrollment_id | uuid? | Ghi danh đã tạo → students.enrollments |
| confirmed_at, confirmed_by | timestamptz?, uuid? | Chốt lúc nào, ai chốt |

### 57. promotion_notes
**Chức năng:** Ghi chú tư vấn lên lớp. Entity con `PromotionNote`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| promotion_candidate_id | uuid FK | → promotion_candidates |
| text | text | Nội dung |
| channel | text? | `call` · `zalo` · `meeting` · `message` · `email` |
| by, by_name | uuid, text | Người ghi |
| at | timestamptz | Thời điểm |

### 58. session_cancel_requests
**Chức năng:** Đơn hủy 1 buổi của GV; duyệt → mời GV dạy thay hoặc hủy buổi & xếp học bù. Entity `SessionCancelRequest`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| session_id | uuid FK | → class_sessions |
| class_id | uuid FK | → classes |
| branch_id | uuid | → system.branches |
| teacher_id | uuid | GV gửi → hr.employees |
| reason_type | text | Loại lý do |
| reason | text | Chi tiết lý do (≥ 10 ký tự) |
| proposed_makeup_at | timestamptz? | GV đề xuất ngày dạy bù |
| status | text | `pending` · `approved` · `rejected` · `withdrawn` GV thu hồi |
| requested_at | timestamptz | Lúc gửi |
| decided_by, decided_at | uuid?, timestamptz? | Người duyệt |
| note | text? | Kết quả xử lý |

### 59. leave_requests
**Chức năng:** Đơn xin nghỉ 1 buổi của HV / PH, kèm đăng ký học bù ở lớp khác. Entity `LeaveRequest`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid | → students.students |
| class_id | uuid FK | → classes |
| session_id | uuid FK | Buổi xin nghỉ → class_sessions |
| branch_id | uuid | → system.branches |
| reason_type | text | Loại lý do |
| reason | text | Chi tiết lý do (≥ 5 ký tự) |
| by | text | `student` · `parent` |
| by_account_id | uuid | Tài khoản cổng gửi → system.portal_accounts |
| by_name | text | Tên người gửi |
| status | text | `pending` · `approved` · `rejected` · `withdrawn` người gửi thu hồi |
| requested_at | timestamptz | Lúc gửi |
| decided_by, decided_at | uuid?, timestamptz? | Người duyệt |
| makeup_session_id | uuid? FK | Buổi học bù đã đăng ký → class_sessions |
| makeup_result | text? | Kết quả điểm danh buổi bù |
| note | text? | Ghi chú |

### 60. substitute_invites
**Chức năng:** Lời mời GV dạy thay 1 buổi; ai nhận trước thì dạy, lời mời khác tự hết hiệu lực; tự cộng lương vai trò dạy thay. Entity `SubstituteInvite`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| session_id | uuid FK | → class_sessions |
| class_id | uuid FK | → classes |
| branch_id | uuid | → system.branches |
| cancel_request_id | uuid? FK | Đơn hủy buổi dẫn tới lời mời → session_cancel_requests |
| from_teacher_id | uuid | GV vắng → hr.employees |
| to_teacher_id | uuid | GV được mời → hr.employees |
| reason | text | Lý do |
| invited_by | uuid | Giáo vụ mời → hr.employees |
| invited_at | timestamptz | Lúc mời |
| respond_by | timestamptz | Hạn trả lời |
| status | text | `pending` · `accepted` · `declined` · `expired` |
| responded_at | timestamptz? | Lúc trả lời / hết hiệu lực |
| decline_reason | text? | Lý do từ chối, hoặc lý do hết hiệu lực (GV khác đã nhận · quá hạn · giáo vụ thu hồi) |

### 61. flash_decks
**Chức năng:** Bộ flashcard từ vựng / Kanji theo bài học (cổng HV). Entity `FlashDeck`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| title | text | Tên bộ |
| level | text | Trình độ |
| kind | text | `vocab` · `kanji` |
| lesson_id | uuid? FK | → lessons |
| lesson_label | text | Nhãn bài, vd "Bài 5" |

### 62. flash_cards
**Chức năng:** Thẻ trong bộ flashcard. Entity con `FlashCard`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| deck_id | uuid FK | → flash_decks |
| front | text | Mặt trước: từ / Kanji |
| reading | text? | Cách đọc |
| meaning | text | Nghĩa |
| example | text? | Ví dụ |
| order | int | Thứ tự |

### 63. card_progress
**Chức năng:** Tiến độ ôn từng thẻ của HV theo hộp Leitner 0–5 (nhớ → lên hộp, quên → về 0); tính chuỗi ngày học. Entity `CardProgress`. UQ (student_id, card_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| student_id | uuid | → students.students |
| deck_id | uuid FK | → flash_decks |
| card_id | uuid FK | → flash_cards |
| box | int | Hộp 0–5 |
| due | date | Ngày cần ôn tiếp |
| reviews | int | Số lần ôn |
| lapses | int | Số lần quên |
| last_reviewed_at | timestamptz? | Lần ôn cuối |

---

## Module Library — schema `library`

### 64. documents
**Chức năng:** Thư viện tài liệu điện tử (sách, đề JLPT, tự học, audio / video) — không gắn lộ trình; watermark, chặn tải; số liệu đọc tổng hợp. Entity `LibraryDocument`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| title | text | Tên tài liệu |
| subtitle | text? | Tên tiếng Nhật / phụ đề |
| description | text? | Mô tả |
| category | text | `textbook` · `jlpt` · `reference` · `self_study` · `media` |
| file_type | text | `pdf` · `audio` · `video` · `link` |
| file_key | text? | Khóa file / link ngoài |
| file_name | text? | Tên file |
| size | bigint? | Byte |
| pages | int? | Số trang |
| duration | int? | Phút (audio / video) |
| levels | text[] | Trình độ |
| skills | text[] | Kỹ năng |
| tags | text[] | Thẻ |
| publisher | text? | Nhà xuất bản |
| year | int? | Năm |
| audience | text[] | Ai xem được: `student` · `teacher` · `staff` |
| branch_ids | uuid[] | Chi nhánh xem được — rỗng = tất cả |
| watermark | bool | Hiện watermark tên người xem + thời gian |
| allow_download | bool | Cho tải về |
| status | text | `published` · `hidden` |
| uploaded_by | uuid | → hr.employees |
| uploaded_at | timestamptz | Lúc tải lên |
| view_count | int | Tổng lượt xem |
| reader_count | int | Số người đọc |
| avg_minutes | numeric | Phút đọc trung bình / lượt |
| last_viewed_at | timestamptz? | Lần xem cuối |

### 65. document_views
**Chức năng:** Từng lượt xem tài liệu (chỉ thêm) — dựng biểu đồ 14 ngày, người đọc nhiều nhất. Entity `DocumentView`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| document_id | uuid FK | → documents |
| viewer_type | text | `staff` · `student` · `guardian` |
| viewer_id | uuid | → hr.employees / students.students / crm.leads |
| viewer_name | text | Tên người xem (in vào watermark) |
| branch_id | uuid? | Chi nhánh người xem |
| started_at | timestamptz | Lúc mở |
| minutes | numeric | Số phút đọc |

### 66. teacher_documents
**Chức năng:** Tài liệu GV tự soạn, gửi cho lớp mình dạy (có thể gắn buổi). Entity `TeacherDocument`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| teacher_id | uuid | → hr.employees |
| title | text | Tên |
| description | text? | Mô tả |
| file_name | text | Tên file |
| kind | text | `pdf` · `doc` · `image` · `audio` · `video` · `link` |
| size | bigint? | Byte |
| url | text? | Khóa file / link |
| class_ids | uuid[] | Lớp được xem → academic.classes |
| session_id | uuid? | Buổi học gắn → academic.class_sessions |

### 67. teacher_document_views
**Chức năng:** Học viên đã xem tài liệu GV. Entity con `TeacherDocumentView`. Khóa chính (teacher_document_id, student_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| teacher_document_id | uuid FK | → teacher_documents |
| student_id | uuid | → students.students |
| viewed_at | timestamptz | Lần xem đầu |

---

## Module Finance — schema `finance`

### 68. invoices
**Chức năng:** Hóa đơn học phí sinh từ ghi danh: thu nhiều đợt, VietQR riêng, hoàn tiền, hủy phần còn lại, hủy hóa đơn (duyệt 2 người). Công nợ tính từ đây. Entity `Invoice`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã theo mẫu Cài đặt, vd HD-CG-2611-0003 |
| enrollment_id | uuid | → students.enrollments |
| student_id | uuid | → students.students |
| class_id | uuid | → academic.classes |
| branch_id | uuid | → system.branches |
| amount | numeric | Học phí gốc |
| discount | numeric | Số tiền giảm |
| discount_id | uuid? FK | Ưu đãi áp dụng → discounts |
| installment_plan_id | uuid? FK | Chính sách nhiều đợt → installment_plans |
| due_date | date | Hạn đóng |
| issued_at | timestamptz | Lúc lập |
| transfer_content | text UQ | Nội dung CK VietQR riêng — dùng tự đối soát |
| status | text | Chỉ lưu trạng thái do người làm: `open` đang hiệu lực · `cancelled` hủy phần còn lại · `voided` hủy hóa đơn. `unpaid` / `partial` / `paid` / `overdue` / `refunded` **tính khi đọc** từ `payments`, `refunds`, `due_date` (TC-05) |
| cancelled_at, cancel_reason | timestamptz?, text? | Hủy phần còn lại (nghỉ / lớp hủy) |
| voided_at, void_reason | timestamptz?, text? | Hủy hóa đơn lập sai |
| void_requested_by, void_approved_by | uuid? | Người đề nghị / người duyệt (khác nhau) |

> Không lưu: phải thu (`amount − discount`), đã thu (`Σ payments − Σ refunds`), còn nợ — tính từ bảng con.

### 69. payments
**Chức năng:** Các lần thu tiền của hóa đơn (tiền mặt, CK, VietQR, thẻ). Entity con `Payment`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| invoice_id | uuid FK | → invoices |
| amount | numeric | Số tiền |
| method | text | `cash` · `transfer` · `vietqr` · `card` (trùng giá trị frontend) |
| paid_at | timestamptz | Lúc thu |
| collector_id | uuid | Người thu → hr.employees (tiền mặt vào bàn giao ca) |
| bank_transaction_id | uuid? FK | Giao dịch ngân hàng đã khớp → bank_transactions |

### 70. refunds
**Chức năng:** Các lần hoàn tiền; từ ngưỡng cấu hình phải có người thứ 2 duyệt. Entity con `Refund`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| invoice_id | uuid FK | → invoices |
| amount | numeric | Số tiền |
| method | text | Hình thức hoàn |
| refunded_at | timestamptz | Lúc hoàn |
| reason | text | Lý do |
| by | uuid | Người tạo → hr.employees |
| approved_by | uuid? | Người duyệt (khác người tạo) |

### 71. branch_prices
**Chức năng:** Giá riêng của khóa tại chi nhánh (không có = giá niêm yết của khóa). Entity `BranchPrice`. UQ (course_id, branch_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| course_id | uuid | → academic.courses |
| branch_id | uuid | → system.branches |
| fee | numeric | Giá tại chi nhánh |

### 72. discounts
**Chức năng:** Ưu đãi học phí: %, số tiền, combo khóa; thời gian, giới hạn lượt (frontend gọi là Promotion). Entity `Discount`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã ưu đãi |
| name | text | Tên |
| kind | text | `percent` · `amount` · `combo` |
| value | numeric | % hoặc số tiền giảm |
| course_ids | uuid[] | Combo: các khóa đăng ký cùng → academic.courses |
| branch_ids | uuid[] | Rỗng = mọi chi nhánh |
| from, to | date | Thời gian áp dụng |
| limit | int? | Giới hạn lượt (null = không giới hạn) |
| used | int | Đã dùng |
| active | bool | Đang bật |
| note | text? | Ghi chú |

### 73. installment_plans
**Chức năng:** Chính sách đóng nhiều đợt: % mỗi đợt, hạn theo ngày khai giảng, phụ thu. Entity `InstallmentPlan`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| name | text | Tên chính sách |
| parts | jsonb | Các đợt `[{percent, offsetDays}]` (tổng 100%) |
| min_fee | numeric | Áp dụng cho khóa có học phí từ |
| surcharge | numeric | Phụ thu (%) |
| active | bool | Đang dùng |

### 74. bank_transactions
**Chức năng:** Giao dịch ngân hàng nhận qua webhook → tự khớp hóa đơn theo nội dung CK; danh sách chưa khớp xử lý tay. Entity `BankTransaction`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| ref | text UQ | Mã giao dịch ngân hàng |
| at | timestamptz | Thời điểm giao dịch |
| amount | numeric | Số tiền |
| content | text | Nội dung chuyển khoản |
| payer | text | Người chuyển |
| account | text | Tài khoản nhận |
| branch_id | uuid | → system.branches |
| status | text | `unmatched` · `auto` tự khớp · `matched` khớp tay · `ignored` bỏ qua |
| invoice_id | uuid? FK | → invoices |
| matched_by | uuid? | Người khớp tay (null = tự khớp) |
| matched_at | timestamptz? | Lúc khớp |
| note | text? | Ghi chú |

### 75. payment_reminders
**Chức năng:** Lịch sử nhắc học phí (chỉ thêm). Entity `PaymentReminder`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| invoice_id | uuid FK | → invoices |
| channel | text | `zalo` · `email` · `sms` · `call` |
| at | timestamptz | Lúc nhắc |
| by | uuid? | Người nhắc (null = hệ thống) |
| note | text? | Ghi chú |

### 76. pay_adjustments
**Chức năng:** Thưởng / phạt / phụ cấp thêm cho 1 người trong 1 tháng (cộng vào lương GV / NV). Entity `PayAdjustment`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| month | text | Tháng `YYYY-MM` |
| employee_id | uuid | → hr.employees |
| amount | numeric | Dương = thưởng / phụ cấp, âm = phạt |
| reason | text | Lý do |
| by | uuid | Người nhập |

### 77. payroll_locks
**Chức năng:** Bảng lương tháng đã chốt (snapshot) — lương GV tính từ buổi đã xác nhận × đơn giá; lương NV từ công + phụ cấp + hoa hồng. Entity `PayrollLock`. UQ (kind, month, branch_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| kind | text | `teacher` · `staff` |
| month | text | `YYYY-MM` |
| branch_id | uuid | → system.branches |
| locked_by | uuid | Người chốt |
| locked_at | timestamptz | Lúc chốt |
| total | numeric | Tổng quỹ lương |
| lines | jsonb | Chi tiết từng người lúc chốt |
| paid_at | timestamptz? | Đã chuyển khoản lương |

### 78. commission_tiers
**Chức năng:** Bậc hoa hồng Sale theo doanh thu thực thu. Entity `CommissionTier`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| from | numeric | Doanh thu từ (đ) |
| percent | numeric | % hoa hồng |

### 79. sale_targets
**Chức năng:** Chỉ tiêu doanh số tháng từng Sale (KPI, bảng xếp hạng). Entity `SaleTarget`. UQ (employee_id, month).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| employee_id | uuid | → hr.employees |
| month | text | `YYYY-MM` |
| target | numeric | Chỉ tiêu |

---

## Module Hr — schema `hr`

### 80. employees
**Chức năng:** Nhân viên / giáo viên / trợ giảng (1 bảng, phân biệt `kind`); hợp đồng, cây quản lý, làm theo ca, phép năm. Entity `Employee`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | Dùng ở mọi module khi tham chiếu "nhân viên / GV / Sale" |
| code | text UQ | Mã nhân viên |
| full_name | text | Họ tên |
| gender | text | `male` · `female` · `other` |
| dob | date? | Ngày sinh |
| phone | text | SĐT |
| email | text UQ | Email |
| branch_id | uuid | Chi nhánh chính → system.branches |
| kind | text | `staff` · `teacher` · `assistant` |
| department | text | `board` · `academic` · `sales` · `marketing` · `finance` · `reception` · `it` · `teaching` |
| position | text | Chức danh |
| shift_based | bool | Làm theo ca → xuất hiện ở Xếp ca |
| status | text | `probation` thử việc · `active` · `leave` nghỉ dài hạn · `quit` đã nghỉ |
| joined_at | date | Ngày vào làm |
| quit_at | date? | Ngày nghỉ việc (ở tương lai + status ≠ `quit` = đã hẹn ngày, job hằng ngày tự chuyển khi tới ngày) |
| quit_reason | varchar(500)? | Lý do nghỉ việc |
| manager_id | uuid? FK | Quản lý trực tiếp → employees |
| contract_type | text | `probation` · `fixed` · `indefinite` · `part_time` · `freelance` |
| contract_no | text | Số hợp đồng (`HĐ-{năm}/{mã NV}-{số thứ tự}`) |
| contract_start, contract_end | date, date? | Hiệu lực hợp đồng |
| contract_base_salary | numeric? | Lương cơ bản / tháng (GV để trống) |
| contract_allowance | numeric? | Phụ cấp |
| leave_balance | numeric | Ngày phép năm còn lại |
| has_account | bool | Đã có tài khoản đăng nhập |
| note | text? | Ghi chú |

### 81. teacher_profiles
**Chức năng:** Hồ sơ giảng dạy của GV / trợ giảng: cấp độ dạy, bằng cấp, đơn giá, lịch rảnh, chi nhánh dạy thêm — dùng xếp lớp, dạy thay, tính lương. Entity con `TeacherProfile` (1–1).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| employee_id | uuid PK FK | → employees |
| nationality | text | `vn` GV Việt · `jp` GV Nhật |
| levels | text[] | Cấp độ dạy được |
| certificates | text[] | Bằng cấp (JLPT N1, sư phạm…) |
| rate_offline | numeric | Đơn giá / giờ offline |
| rate_online | numeric | Đơn giá / giờ online |
| availability | jsonb | Lịch rảnh: thứ (1–7) → ca (`morning` / `afternoon` / `evening`) |
| extra_branch_ids | uuid[] | Chi nhánh dạy thêm |
| bio | text? | Giới thiệu |

### 81a. employment_contracts
**Chức năng:** Lịch sử hợp đồng lao động — mỗi lần gia hạn / ký mới, hợp đồng cũ được lưu vào đây (hợp đồng hiện hành nằm ở `employees.contract_*`). Entity con `ContractHistoryEntry`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| employee_id | uuid FK | → employees (xóa theo) |
| type | text | Như `employees.contract_type` |
| no | varchar(50) | Số hợp đồng |
| start, end | date, date? | Hiệu lực |
| base_salary, allowance | numeric? | Lương cơ bản, phụ cấp (ẩn với người không có quyền xem lương) |
| replaced_at | timestamptz | Lúc bị thay bằng hợp đồng mới |
| replaced_by | uuid? | Tài khoản thực hiện → system.user_accounts |

### 82. time_records
**Chức năng:** Chấm công ngày của nhân viên (check-in / out), sửa tay có lý do; so với giờ hành chính / ca → muộn, sớm, vắng. Entity `TimeRecord`. UQ (employee_id, date).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| employee_id | uuid FK | → employees |
| branch_id | uuid | → system.branches |
| date | date | Ngày |
| check_in | time? | Giờ vào |
| check_out | time? | Giờ ra |
| edited_by | uuid? | Quản lý sửa tay |
| edit_reason | text? | Lý do sửa |

### 83. hr_requests
**Chức năng:** Đơn từ nhân viên (nghỉ phép, đổi ca, công tác, muộn / sớm, đề xuất, tạm ứng) duyệt nhiều cấp. Entity `HrRequest`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã đơn |
| type | text | `leave` · `shift_swap` · `business_trip` · `late_early` · `proposal` · `advance` |
| employee_id | uuid FK | Người gửi → employees |
| branch_id | uuid | → system.branches |
| from, to | timestamptz | Thời gian áp dụng |
| days | numeric? | Số ngày nghỉ |
| paid_leave | bool? | Nghỉ có lương |
| swap_my_shift_id | uuid? | Ca của mình → work.shift_assignments |
| swap_with_employee_id | uuid? FK | Đổi với ai → employees |
| swap_their_shift_id | uuid? | Ca của đồng nghiệp → work.shift_assignments |
| amount | numeric? | Số tiền (đề xuất / tạm ứng) |
| title | text? | Tiêu đề (đề xuất) |
| reason | text | Lý do |
| status | text | `pending` · `approved` · `rejected` · `cancelled` |
| requested_at | timestamptz | Lúc gửi |

### 84. hr_request_steps
**Chức năng:** Các bước duyệt của đơn, duyệt lần lượt; từ chối ở bước nào cũng kết thúc đơn. Entity con `ApprovalStep`. Khóa chính (hr_request_id, order).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| hr_request_id | uuid FK | → hr_requests |
| order | int | Thứ tự bước |
| role | text | Vai trò duyệt ("Quản lý trực tiếp", "Nhân sự") |
| approver_id | uuid | → employees |
| decision | text? | `approved` · `rejected` |
| at | timestamptz? | Lúc quyết định |
| comment | text? | Ý kiến |

### 85. jobs
**Chức năng:** Tin tuyển dụng. Entity `Job`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| title | text | Vị trí |
| branch_id | uuid | → system.branches |
| department | text | Phòng ban |
| kind | text | `staff` · `teacher` · `assistant` |
| quantity | int | Số lượng cần tuyển |
| salary_range | text? | Khoảng lương hiển thị |
| status | text | `open` · `paused` · `closed` |
| posted_at | date | Ngày đăng |
| deadline | date | Hạn nộp |
| channels | text[] | Kênh đăng |
| description | text? | Mô tả |

### 86. candidates
**Chức năng:** Ứng viên theo pipeline Ứng tuyển → Sàng lọc → Phỏng vấn → Offer → Nhận việc / Loại. Entity `Candidate`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| job_id | uuid FK | → jobs |
| full_name | text | Họ tên |
| phone | text | SĐT |
| email | text | Email |
| source | text | Nguồn ứng viên |
| stage | text | `applied` · `screening` · `interview` · `offer` · `hired` · `rejected` |
| applied_at | date | Ngày ứng tuyển |
| cv_key, cv_name | text? | File CV |
| japanese | text? | Trình độ tiếng Nhật |
| expected_salary | numeric? | Lương mong muốn |
| interview_at | timestamptz? | Lịch phỏng vấn |
| interviewer_id | uuid? | Người phỏng vấn → employees |
| interview_mode | text? | `offline` · `online` |
| interview_score | numeric? | Điểm phỏng vấn |
| interview_note | text? | Nhận xét |
| offer_salary | numeric? | Lương offer |
| offer_start_date | date? | Ngày bắt đầu |
| offer_sent_at | timestamptz? | Lúc gửi offer |
| offer_accepted | bool? | Đồng ý offer |
| reject_reason | text? | Lý do loại |

### 87. candidate_notes
**Chức năng:** Ghi chú về ứng viên. Entity con `CandidateNote`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| candidate_id | uuid FK | → candidates |
| text | text | Nội dung |
| by, by_name | uuid, text | Người ghi |
| at | timestamptz | Thời điểm |

### 88. onboardings
**Chức năng:** Hồ sơ nhận việc: checklist + người hướng dẫn; xong → tạo hồ sơ nhân viên + tài khoản. Entity `Onboarding`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| candidate_id | uuid? FK | → candidates |
| full_name, phone, email | text | Thông tin người mới |
| position | text | Vị trí |
| department | text | Phòng ban |
| kind | text | `staff` · `teacher` · `assistant` |
| branch_id | uuid | → system.branches |
| start_date | date | Ngày bắt đầu |
| mentor_id | uuid FK | Người hướng dẫn → employees |
| employee_id | uuid? FK | Hồ sơ nhân viên đã tạo → employees |

### 89. onboarding_items
**Chức năng:** Việc trong checklist nhận việc. Entity con `OnboardingItem`. Khóa chính (onboarding_id, key).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| onboarding_id | uuid FK | → onboardings |
| key | text | Khóa việc |
| group | text | `docs` · `account` · `equipment` · `training` |
| label | text | Tên việc |
| done | bool | Đã xong |
| done_by, done_at | uuid?, timestamptz? | Ai đánh dấu, lúc nào |

---

## Module Work — schema `work`

### 90. tasks
**Chức năng:** Công việc giao cho nhân viên (Kanban): hạn, ưu tiên, người theo dõi, lặp lại, gắn lead / lớp / HV. Dùng cho "Task toàn hệ thống", "Giao việc cấp dưới", "Việc của tôi". Entity `WorkTask`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| code | text UQ | Mã task |
| title | text | Tiêu đề |
| description | text? | Mô tả |
| branch_id | uuid | → system.branches |
| assignee_id | uuid | Người làm → hr.employees |
| creator_id | uuid | Người giao → hr.employees |
| watcher_ids | uuid[] | Người theo dõi |
| priority | text | `low` · `normal` · `high` · `urgent` |
| status | text | `todo` · `doing` · `review` · `done` |
| due | timestamptz? | Hạn |
| repeat | text | `none` · `daily` · `weekly` · `monthly` |
| link_type | text? | `lead` · `class` · `student` |
| link_id | uuid? | Id đối tượng gắn |
| link_label | text? | Nhãn hiển thị |
| completed_at | timestamptz? | Lúc hoàn thành |

### 91. task_checklist_items
**Chức năng:** Checklist trong task. Entity con `TaskChecklistItem`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| task_id | uuid FK | → tasks |
| text | text | Nội dung |
| done | bool | Đã xong |
| order | int | Thứ tự |

### 92. task_comments
**Chức năng:** Bình luận trong task. Entity con `TaskComment`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| task_id | uuid FK | → tasks |
| text | text | Nội dung |
| by | uuid | Người viết → hr.employees |
| at | timestamptz | Thời điểm |

### 93. shift_assignments
**Chức năng:** Ca làm được xếp cho nhân viên làm theo ca (theo tuần); căn cứ chia lead theo ca, chấm công, lương. Entity `ShiftAssignment`. UQ (employee_id, date, shift).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| employee_id | uuid | → hr.employees |
| branch_id | uuid | → system.branches |
| date | date | Ngày |
| shift | text | `morning` · `afternoon` · `evening` |

### 94. shift_registrations
**Chức năng:** Nhân viên tự khai ca mong muốn tuần tới / rảnh / bận (trang "Lịch rảnh / bận"). Entity `ShiftRegistration`. UQ (employee_id, date, shift, kind).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| employee_id | uuid | → hr.employees |
| date | date | Ngày |
| shift | text | Ca |
| kind | text | `preference` mong muốn · `available` rảnh · `busy` bận |
| updated_at | timestamptz | Lần khai cuối |

### 95. handovers
**Chức năng:** Bàn giao cuối ca: tiền mặt, lead đang dở, việc tồn; ca sau xác nhận nhận / khiếu nại khi lệch tiền. Entity `Handover`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| branch_id | uuid | → system.branches |
| date | date | Ngày |
| from_shift | text | Ca bàn giao |
| from_id | uuid | Người bàn giao → hr.employees |
| to_id | uuid | Người nhận → hr.employees |
| cash_expected | numeric | Hệ thống tính: tồn đầu ca + tiền mặt thu trong ca |
| cash_counted | numeric | NV đếm thực tế |
| cash_received | numeric? | Người nhận đếm lại |
| cash_note | text? | Ghi chú tiền |
| issues | text? | Sự cố trong ca |
| status | text | `pending` · `received` · `disputed` |
| handed_at | timestamptz | Lúc bàn giao |
| received_at | timestamptz? | Lúc nhận |
| receiver_note | text? | Ghi chú người nhận |

### 96. handover_leads
**Chức năng:** Lead đang dở bàn giao cho ca sau. Entity con `HandoverLead`. Khóa chính (handover_id, lead_id).

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| handover_id | uuid FK | → handovers |
| lead_id | uuid | → crm.leads |
| note | text | Ghi chú |

### 97. handover_items
**Chức năng:** Việc tồn bàn giao cho ca sau. Entity con `HandoverItem`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| handover_id | uuid FK | → handovers |
| text | text | Nội dung |
| done | bool | Ca sau đã làm xong |

---

## Module Notifications — schema `notifications`

### 98. notifications
**Chức năng:** Thông báo trong app (chuông, realtime) cho nhân viên / GV / HV / PH. Tin Zalo / SMS / email gửi đi ghi ở `crm.message_logs`. Entity `Notification`.

| Thuộc tính | Kiểu | Giải thích |
|---|---|---|
| id | uuid PK | |
| recipient_type | text | `staff` (system.user_accounts) · `portal` (system.portal_accounts) |
| recipient_id | uuid | Id tài khoản nhận |
| title | text | Tiêu đề |
| description | text | Nội dung |
| tone | text | `info` · `success` · `warning` · `danger` |
| link | text? | Đường dẫn khi bấm |
| event_key | text? | Sự kiện sinh ra, vd `crm.lead.sla_breached` |
| created_at | timestamptz | Lúc tạo |
| read_at | timestamptz? | Lúc đọc |

---

## Module Reporting — schema `reporting`

Không có bảng nghiệp vụ. Chỉ **view / read model** dựng từ integration event của các module khác (doanh thu, chuyển đổi lead,
sĩ số, chuyên cần, lấp đầy phòng). Thêm view nào thì ghi vào đây.

---

## Những gì KHÔNG lưu (tính ra khi đọc)

| Số liệu | Tính từ |
|---|---|
| Trạng thái học tổng hợp của HV (đang học / bảo lưu / đã học xong / đã nghỉ) | students.enrollments |
| Sĩ số chính thức của lớp | students.enrollments (status = studying) |
| Lịch sử dụng phòng, tỷ lệ lấp đầy, trùng lịch phòng / GV | academic.class_sessions + crm.appointments |
| Tiến độ lớp theo lộ trình | class_sessions.report_covered_chapter_ids |
| Công nợ, tuổi nợ | finance.invoices + payments + refunds |
| Lương GV tạm tính | class_sessions đã xác nhận + teacher_attendances × hr.teacher_profiles.rate_* |
| Phễu chuyển đổi, thời gian mỗi bước | crm.leads + consultations + placement_attempts |
| Chuỗi ngày học flashcard (streak) | academic.card_progress.last_reviewed_at |

## Lịch sử thay đổi

| Ngày | Thay đổi |
|---|---|
| 2026-11-17 | Tạo tài liệu: 98 bảng / 9 schema, dựng song song với entity Domain |
| 2026-11-17 | Đăng nhập & bảo mật (user story 01): thêm bảng `user_sessions` (13a), `one_time_tokens` (13b); `user_accounts` + `failed_login_count`, `locked_until`, `must_change_password`; `portal_accounts` + `failed_login_count`, `locked_until`, `must_change_password` |
| 2026-11-17 | Bỏ đăng nhập 2 lớp (2FA) — không làm: xóa `user_accounts.two_factor_enabled`, `two_factor_secret`, `two_factor_required`; `user_sessions.trusted_until`; `one_time_tokens.purpose` bỏ `two_factor`, `two_factor_setup` |
| 2026-11-17 | Code giai đoạn 3: `user_accounts` + `token_version`; `portal_accounts` + `display_name`, `token_version`; `audit_logs` + `trace_id`, category thêm `security`; thêm bảng `idempotency_keys` (13c). Migration đầu tiên của schema `system`, `hr` (hr mới có `employees`, `teacher_profiles`) |
| 2026-11-17 | Giai đoạn 8: thêm bảng chung `outbox_messages` vào mọi schema module (migration `AddOutbox` cho `system`, `hr`) |
| 2026-11-17 | Giai đoạn 9 (user story 03): `branches.invoice_prefix` thêm UQ (trong số chưa xóa) — migration `BranchInvoicePrefixUnique` |
| 2026-11-17 | Giai đoạn 10 (user story 04): `employees` + `quit_reason`; thêm bảng `employment_contracts` (81a) lưu hợp đồng cũ — migration `HrEmployeeHistory` |
| 2026-11-17 | Giai đoạn 12 (user story 09): `materials` + `file_key`, `file_name`, `content_type`, `url` thành tùy chọn (chỉ cho link); `curricula.version` dạng `v1.0` |
| 2026-11-17 | Giai đoạn 13 (user story 07): `classes` + `pause_reason`; `class_sessions.status` `completed` = đã xác nhận (bỏ xác nhận → `scheduled`); tạo bảng `rooms`, `room_issues`, `classes`, `class_sessions`, `attendance_records`, `teacher_attendances`, `session_support_entries` — migration `AcademicClassesSessions` |
| 2026-11-20 | Giai đoạn 13 phase 3 (user story 07): `settings` thêm khóa `academic.holidays` (ngày nghỉ lễ, không đổi cấu trúc bảng); `room_issues` mở lại được (`resolved` → `open` / `in_progress`, xóa kết quả sửa) |
| 2026-11-20 | Giai đoạn 13 phase 4: `attendance_records.student_id`, `teacher_attendances.teacher_id` (thuộc khóa chính) khai báo không tự sinh — EF thêm dòng mới vào buổi có sẵn bằng INSERT (trước đó UPDATE 0 dòng → lỗi). Migration `AcademicOwnedKeysNotGenerated` không đổi cấu trúc bảng |
| 2026-11-20 | Giai đoạn 14 phase 2 (user story 06): tạo bảng `students`, `enrollments`, `certificates` đúng thiết kế (thêm UQ `certificates.code`, chỉ mục `enrollments (class_id, status)`, `(student_id, status)`, `students.phone`) — migration `Initial` của schema `students`; `enrollments.branch_id` = chi nhánh của lớp |
| 2026-11-20 | Giai đoạn 15 phase 1 (user story 07, HV-14): `session_cancel_requests.status`, `leave_requests.status` thêm `withdrawn` (thu hồi khi còn chờ); `substitute_invites.decline_reason` ghi cả lý do hết hiệu lực. Không đổi cấu trúc bảng |
| 2026-11-17 | Chốt thiết kế (user story 08): `invoices.status` chỉ lưu `open` / `cancelled` / `voided`, các trạng thái tiền tính khi đọc; `payments.method` dùng `vietqr` (không `viet_qr`). Domain `InvoiceStatus`, `PaymentMethod` sửa theo khi code module Tài chính |
