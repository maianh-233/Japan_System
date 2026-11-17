# Đường đi của một request (Request Pipeline) — Middleware & Filter

> Trạng thái: **Đề xuất** (chưa có code) · Ngày: 2026-11-17
> Ký hiệu: 🧑 ý tưởng của chủ dự án · 💡 đề xuất bổ sung · ⚙️ con số có thể chỉnh trong cấu hình
>
> **Đã chốt (2026-11-17):** chạy **1 instance**, **không dùng Redis** (dự án cá nhân) · **không** đăng nhập 2 lớp ·
> hạn lưu audit log theo mục 8 · chưa trường nào cần Rich Text / HTML (mục 6.1) · nhập liệu hàng loạt theo mục 4.1.

Tài liệu mô tả mọi request vào `JPCenter.Api` đi qua những lớp kiểm tra nào, theo thứ tự nào, mỗi lớp chặn được gì
và trả lỗi ra sao. Mục tiêu:

1. 🧑 Tự động kiểm tra **token** hợp lệ.
2. 🧑 Kiểm tra người gọi **có được phép** làm hành động đó không.
3. 🧑 **Giới hạn tần suất** (rate limit) — chặn spam, ví dụ bấm nút "Đăng ký" liên tục khi mạng lỗi; nhắc người dùng.
4. 🧑 **Kiểm tra dữ liệu gửi lên** để chống tấn công.
5. 🧑 **Xử lý dữ liệu trả về** thống nhất.
6. 🧑 Ghi **audit log** (nhật ký hệ thống) sau khi xử lý xong.
7. 💡 Bổ sung: mã truy vết request, chống gửi trùng (Idempotency-Key), bắt lỗi toàn cục, header bảo mật, timeout.

---

## 1. Tổng quan thứ tự

Thứ tự **rất quan trọng**: lớp rẻ, chặn được nhiều đặt trước; lớp cần biết "ai đang gọi" đặt sau xác thực;
lớp cần biết "endpoint nào" (quyền, validate, audit) chạy sau khi định tuyến.

```
 Trình duyệt
    │  (1) Frontend: khóa nút khi đang gửi, debounce, gắn Idempotency-Key         💡
    ▼
 Reverse proxy (Nginx)
    │  (2) Giới hạn theo IP, giới hạn kích thước body, chặn sớm trước khi tới app  💡
    ▼
 ── ASP.NET Core ───────────────────────────────────────────────────────────────
  M1  ForwardedHeaders        lấy IP thật của client sau proxy
  M2  CorrelationId           gắn X-Request-Id, đưa vào log                         💡
  M3  ExceptionHandler        mọi lỗi hệ thống → ProblemDetails, không lộ stack     💡
  M4  SecurityHeaders + HSTS  header bảo mật                                        💡
      Routing + BodySize      body quá lớn (Content-Length) → 413 trước khi đọc
  M5  CORS                    chỉ cho phép domain của frontend
  M7  Authentication          kiểm tra token (JWT)                                  🧑
  M8  CurrentUser             nạp quyền (cache), chặn token đã thu hồi
      Penalty                 người đang bị phạt vì spam → chặn thao tác ghi
  M6/M9 RateLimit             theo IP (chưa đăng nhập) / theo người + endpoint       🧑
  M10 Authorization           bắt buộc đăng nhập; quyền chi tiết: filter RequirePermission 🧑
  ── Endpoint filter (chạy quanh từng endpoint) ──
  F1  Idempotency             request trùng → trả lại kết quả cũ, không chạy lại   💡
  F2  Validation              kiểm tra & làm sạch dữ liệu vào                       🧑
  F3  Audit                   ghi nhật ký sau khi xử lý                             🧑
        ▼
      Endpoint → ISender → Command/Query Handler → DbContext
        ▲                         └─ DataChangeAuditInterceptor gom before→after vào dòng nhật ký của hành động
  R1  Response shaping        Result → HTTP, bọc lỗi chuẩn, ẩn/che dữ liệu nhạy cảm 🧑
```

Code thật: `RequestPipelineExtensions.UseRequestPipeline()` trong `JPCenter.Api.Common` (thứ tự cố định, có chú thích M1…M10).
Host chỉ gọi `builder.Services.AddRequestPipeline(config)` + `app.UseRequestPipeline()` + `app.MapModules()`.

Rate limit đặt **sau** xác thực (khác bản đề xuất đầu) để phân vùng được theo tài khoản; chưa đăng nhập thì tính theo IP.
Đường dẫn không tồn tại mà chưa đăng nhập → 401 (không lộ đường dẫn nào có thật).

> Ghi chú kỹ thuật: Middleware chạy cho **mọi** request; Endpoint filter chỉ chạy cho endpoint được gắn và **biết
> kiểu dữ liệu đầu vào** → validate, idempotency, audit dùng endpoint filter để không phải đọc lại body thô.

---

## 2. Xác thực token (M7) 🧑

### Cách làm
- **Access token**: JWT ký bằng khóa bí mật (HS256) hoặc cặp khóa (RS256), sống **ngắn** ⚙️ 15 phút.
- **Refresh token**: chuỗi ngẫu nhiên, lưu **dạng băm** trong DB, sống ⚙️ 7–30 ngày, **xoay vòng** mỗi lần dùng
  (dùng rồi là hết hạn; phát hiện dùng lại token cũ → thu hồi cả chuỗi phiên vì nghi bị đánh cắp). 💡
- Lưu ở frontend 💡: access token trong bộ nhớ (biến JS), refresh token trong **cookie `HttpOnly; Secure; SameSite=Strict`**
  → JavaScript độc hại không đọc được.

### Middleware kiểm tra gì
| Kiểm tra | Sai thì |
|---|---|
| Có header `Authorization: Bearer …` (trừ endpoint `AllowAnonymous`) | 401 |
| Chữ ký đúng, **thuật toán cố định** (không chấp nhận `alg: none`) | 401 |
| `iss`, `aud` đúng, chưa hết hạn (lệch giờ cho phép ⚙️ 30 giây) | 401 `auth.token_expired` → frontend tự gọi refresh |
| `token_version` trong token = phiên bản hiện tại của tài khoản 💡 | 401 `auth.token_revoked` |
| Tài khoản chưa bị khóa / nghỉ việc | 401 `auth.account_disabled` |

💡 **Thu hồi token tức thì**: mỗi tài khoản có `token_version`. Đổi mật khẩu, bị khóa, bị đổi vai trò, bấm "Đăng xuất
mọi thiết bị" → tăng số này → mọi token cũ hết hiệu lực ngay (kiểm tra qua cache, không truy vấn DB mỗi request).

### Token chứa gì
Chỉ thông tin ít đổi: `sub` (UserId), `portal` (staff/teacher/student/parent), `token_version`.
**Không** nhét danh sách quyền vào token (dài, và khi quyền đổi thì token cũ vẫn mang quyền cũ đến lúc hết hạn).

---

## 3. Nạp người dùng & kiểm tra quyền (M8, M10) 🧑

### M8 — CurrentUser
Sau khi token hợp lệ, nạp `ICurrentUser` (UserId, EmployeeId, Portal, BranchIds, Permissions, OwnerId) từ
**cache** ⚙️ 5 phút, khóa theo `UserId + token_version`. Đổi vai trò / quyền → xóa cache của người đó.

### M10 — Ba tầng phân quyền
| Tầng | Ở đâu | Ví dụ | Sai thì |
|---|---|---|---|
| **1. Quyền chức năng** | Endpoint: `.RequirePermission("crm.leads", PermissionAction.Update)` | Sale được sửa lead, GV thì không | 403 `auth.forbidden` |
| **2. Phạm vi dữ liệu** (own / branch / all) | Global query filter trong DbContext | Sale chi nhánh A không thấy lead chi nhánh B | Bản ghi "không tồn tại" → 404 |
| **3. Chủ sở hữu (cổng)** | Filter theo `OwnerId` | PH chỉ thấy con mình, GV chỉ thấy lớp mình dạy | 404 |

- Kiểm tra theo **mã quyền**, không theo tên vai trò (đúng [CLAUDE.md](../CLAUDE.md) mục 6).
- 💡 Với dữ liệu không thuộc phạm vi, trả **404 chứ không 403** — để kẻ xấu không dò được "mã HV1234 có tồn tại".
- 💡 Endpoint **mặc định phải có quyền**: endpoint nào quên khai báo `RequirePermission` / `AllowAnonymous` →
  test kiến trúc báo đỏ (fallback policy = yêu cầu đăng nhập).
- 💡 `BranchId` gửi lên trong body **không được tin**: luôn kiểm tra thuộc `CurrentUser.BranchIds`.
- 403 được ghi audit mức `warning` (nhiều lần liên tiếp → có thể là dò quyền).

---

## 4. Giới hạn tần suất — Rate limit (M6, M9) 🧑

### Vấn đề
Người dùng bấm "Đăng ký" 10 lần khi mạng chập chờn → 10 request → có thể tạo **10 bản ghi trùng** và làm nặng server.
Đây thực ra là **hai vấn đề khác nhau**:

| Vấn đề | Giải pháp đúng |
|---|---|
| Quá nhiều request làm quá tải | **Rate limit** (mục này) |
| Cùng một thao tác bị gửi nhiều lần → dữ liệu trùng | **Idempotency-Key** (mục 5) 💡 — rate limit *không* giải quyết được, vì 2 request trùng vẫn có thể lọt dưới ngưỡng |

### 💡 Chặn ở 3 lớp — càng xa server càng tốt
"Không cho request chạm tới server" chỉ làm được hoàn toàn ở **phía trước** ứng dụng:

1. **Frontend** (rẻ nhất, chặn 95% trường hợp vô tình): nút chuyển sang trạng thái *đang gửi* và bị khóa đến khi có
   phản hồi; debounce ô tìm kiếm ⚙️ 300 ms; khi nhận 429 → hiện thông báo và đếm ngược theo `Retry-After`.
2. **Nginx** (`limit_req`, `limit_conn`, `client_max_body_size`): chặn spam theo IP **trước khi** vào .NET — gần như
   không tốn tài nguyên ứng dụng.
3. **Ứng dụng** (`Microsoft.AspNetCore.RateLimiting` — có sẵn trong .NET, không thêm thư viện): chặn theo
   **người dùng + endpoint**, vì nhiều người dùng chung 1 IP (wifi trung tâm) nên Nginx không phân biệt được.

Ở lớp 3, request bị từ chối vẫn tới app nhưng bị trả 429 **ngay ở middleware** — không xác thực lại DB, không chạy
handler, không chạm database → chi phí gần bằng 0.

### Các policy ⚙️ (ĐÃ CODE — chỉnh ở mục `RateLimit` của cấu hình, vd `RateLimit__WriteBurst=10` trong `.env`)

| Policy | Áp dụng | Khóa phân vùng | Ngưỡng | Thuật toán |
|---|---|---|---|---|
| *(tự động)* chung | Mọi request (lưới an toàn) | Tài khoản, chưa đăng nhập thì IP | 300 / phút | Sliding window |
| *(tự động)* ghi | Mọi POST / PUT / PATCH / DELETE | Tài khoản + mẫu đường dẫn | gửi liền 10, sau đó 1 / giây | Token bucket |
| `anon` | Đăng nhập, quên mật khẩu, form đăng ký công khai | IP + mẫu đường dẫn | 20 / phút (nhiều người chung wifi trung tâm) | Fixed window |
| `write-sensitive` | Thanh toán, ghi danh, tạo hóa đơn, gửi tin hàng loạt | Tài khoản + mẫu đường dẫn | 3 / 10 giây | Fixed window |
| `export`, `import` | Xuất / nhập file | Tài khoản | 1 lần chạy cùng lúc | Concurrency |

Gắn vào endpoint: `.RequireRateLimiting(RateLimitPolicies.WriteSensitive)`. Chống dò mật khẩu theo **tài khoản** do
domain làm (`LoginThrottle`: sai 5 lần → khóa 15 phút), không dựa vào IP.

### Khi vượt ngưỡng
- Trả **429 Too Many Requests**, header `Retry-After: <giây>`, body ProblemDetails:
  ```json
  { "code": "system.too_many_requests",
    "title": "Bạn thao tác quá nhanh. Vui lòng đợi 10 giây rồi thử lại, không bấm liên tục.",
    "retryAfter": 10, "traceId": "…" }
  ```
- Frontend hiện toast cảnh báo + khóa nút trong thời gian `retryAfter`.
- 💡 **Phạt tăng dần** cho người cố tình: vi phạm ⚙️ 3 lần / 10 phút → khóa thao tác ghi ⚙️ 5 phút, ghi audit
  nhóm `security` mức `warning` (1 lần); trong thời gian khóa vẫn xem (GET) được. Không khóa hẳn tài khoản tự động.
- 💡 **Đăng nhập sai**: sai ⚙️ 5 lần liên tiếp cho cùng tài khoản → khóa đăng nhập ⚙️ 15 phút (chống dò mật khẩu),
  ghi audit `critical`.

### Lưu bộ đếm ở đâu — ĐÃ CHỐT
- Chạy **1 instance** → bộ đếm rate limit, cache quyền, danh sách khóa tạm đều để **bộ nhớ trong** (`IMemoryCache`,
  rate limiter có sẵn của .NET). **Không dùng Redis.**
- Hệ quả chấp nhận được: khởi động lại app thì bộ đếm về 0 (người đang bị khóa tạm được mở sớm).
- Chỉ dữ liệu **phải bền** mới lưu PostgreSQL: Idempotency-Key (mục 5), khóa đăng nhập do sai mật khẩu, audit log.
- Nếu sau này chạy ≥ 2 instance → viết ADR thêm Redis; code đã đi qua interface nên chỉ đổi phần hiện thực.

### 4.1 Nhập liệu hàng loạt — làm sao không bị rate limit chặn nhầm 💡

**Nguyên tắc:** thao tác hàng loạt = **1 request chở nhiều dòng**, KHÔNG phải N request liên tiếp.
Như vậy rate limit vẫn giữ ngưỡng chặt cho người spam mà người làm việc thật không bao giờ chạm ngưỡng.

Hệ thống đang có 3 kiểu nhập nhiều:

| Kiểu | Đang có ở frontend | Cách làm ở backend |
|---|---|---|
| **A. Nhập file Excel** | `ImportDialog` dùng chung — lead, học viên, câu hỏi; tối đa 5.000 dòng; frontend đã kiểm tra & chia Hợp lệ / Lỗi / Trùng | 1 endpoint import riêng mỗi loại, policy `import` |
| **B. Thao tác trên các dòng đã chọn** | Thanh thao tác hàng loạt của `DataTable` (vd cho nghỉ học, gửi thông báo cho nhiều HV ở tab Học viên của lớp) | 1 endpoint `…/bulk` nhận danh sách id |
| **C. Nhập tay nhanh liên tục** | Sale nhập lead lần lượt, GV điểm danh từng HV | Token bucket cho phép "nhanh từng đợt"; điểm danh lưu cả bảng 1 lần |

**A. Nhập file Excel**
1. `POST /api/v1/crm/lead-imports/preview` — gửi các dòng (JSON, không gửi file thô). Server **kiểm tra lại toàn bộ**
   (không tin kết quả kiểm tra của frontend: trùng SĐT với DB, chi nhánh trong phạm vi quyền, Sale tồn tại…),
   trả lỗi theo từng dòng. Không ghi gì vào DB.
2. `POST /api/v1/crm/lead-imports` — ghi các dòng hợp lệ, **bắt buộc Idempotency-Key** (mạng lỗi bấm lại không nhập 2 lần).
   Ghi theo lô ⚙️ 500 dòng / transaction; trả `{ imported, skipped, errors[] }`.
3. Giới hạn riêng cho endpoint import:
   - Policy `import`: ⚙️ 5 lần / phút / người, **chỉ 1 lần nhập chạy cùng lúc** mỗi người (concurrency limiter).
   - Body tối đa ⚙️ 5 MB (thay vì 1 MB chung), số dòng ≤ 5.000 (khớp `MAX_ROWS` frontend), timeout ⚙️ 120 giây.
   - Quyền riêng: `crm.leads` + `create` (dùng lại quyền tạo, không cần mã quyền mới) — import vẫn đi qua domain
     `Lead.Create(...)` từng dòng, không insert thẳng SQL, để giữ đúng quy tắc nghiệp vụ.
4. Câu hỏi nhập vào luôn ở trạng thái Nháp / Chờ duyệt (đúng như frontend đang làm).
5. Khi dữ liệu lớn hơn (nếu sau này cần > 5.000 dòng): chuyển thành **job nền** — trả `jobId` ngay, chạy bằng
   `BackgroundService`, báo tiến độ qua SignalR. Chưa cần làm bây giờ.

**B. Thao tác trên dòng đã chọn**
- `POST /api/v1/academic/classes/{id}/students/bulk-drop` body `{ enrollmentIds: [...] }`, tối đa ⚙️ 200 id / lần.
- Kết quả **từng phần**: `{ succeeded: [...], failed: [{ id, code, message }] }` — một HV lỗi không làm hỏng cả lô
  (trừ thao tác tiền bạc: tất cả hoặc không gì cả).
- Tính là **1 request** trong policy `write-sensitive`.

**C. Nhập tay nhanh**
- Policy `write` dùng **token bucket** ⚙️ 10 token, nạp 1 token/giây: người nhập nhanh vẫn gửi được 10 lần liền,
  sau đó ~1 lần/giây — tốc độ người thật không vượt nổi, còn script spam thì bị chặn.
- Điểm danh: frontend lưu **cả bảng điểm danh của buổi** trong 1 request (`PUT …/class-sessions/{id}/attendance`),
  không gửi request mỗi lần bấm 1 HV. Tương tự cho nhập điểm cả lớp.

**Audit cho thao tác hàng loạt:** ghi **1 dòng tổng hợp** ("Nhập 1.200 lead từ file leads-t11.xlsx — 1.180 thành công,
20 lỗi"), lưu danh sách id vào `detail`; **không** ghi 1.200 dòng before→after (làm phình bảng audit).

---

## 5. 💡 Chống gửi trùng — Idempotency-Key (F1)

Giải quyết đúng tình huống "mạng lỗi, bấm Đăng ký nhiều lần":

1. Frontend tạo một mã ngẫu nhiên (UUID) **khi mở form**, gửi kèm mọi lần bấm: `Idempotency-Key: 7f3c…`.
2. Server lưu `(UserId, Key, hash body)` → trạng thái `đang xử lý` / `xong + response`.
3. Request thứ 2 cùng key:
   - lần 1 **đang chạy** → 409 `system.request_in_progress` ("Yêu cầu đang được xử lý, vui lòng chờ").
   - lần 1 **đã xong** → trả lại **đúng response cũ**, không chạy handler lần nữa → không có bản ghi trùng.
   - cùng key nhưng body khác → 422 (frontend dùng sai key).
4. Key hết hạn sau ⚙️ 24 giờ.

Bắt buộc cho các endpoint tạo tiền / ghi danh / gửi tin: thu học phí, tạo hóa đơn, ghi danh, hoàn tiền, gửi thông báo
hàng loạt, nhập file Excel. Lưu trong bảng `system.idempotency_keys` (PostgreSQL); job Quartz dọn key hết hạn mỗi đêm.

Ngoài ra, **ràng buộc unique trong DB** (vd SĐT lead trong 1 chi nhánh) là hàng rào cuối cùng chống trùng.

---

## 6. Kiểm tra dữ liệu gửi lên (F2) 🧑

Nguyên tắc: **chặn sai kiểu ở cửa, kiểm tra nghiệp vụ trong domain, mã hóa khi xuất ra**.

| Mối đe dọa | Cách chặn |
|---|---|
| Body quá lớn / JSON lồng sâu (làm treo server) | Giới hạn body ⚙️ 1 MB cho JSON (file đi đường riêng), `MaxDepth` ⚙️ 32, timeout request ⚙️ 30 giây |
| Sai `Content-Type` | Chỉ nhận `application/json` (và `multipart/form-data` ở endpoint upload) → 415 |
| **Mass assignment** — gửi thêm `"isPaid": true`, `"branchId": …` | Mỗi use case 1 DTO riêng chỉ có trường được phép; **không bao giờ** bind thẳng vào entity; 💡 bật `UnmappedMemberHandling.Disallow` → trường lạ = 400 |
| Dữ liệu sai định dạng | **FluentValidation** chạy tự động qua filter: độ dài, SĐT VN, email, tiền ≥ 0, ngày hợp lệ, enum hợp lệ → 400 kèm `errors` theo trường |
| **SQL injection** | EF Core luôn dùng tham số; cấm nối chuỗi SQL (đã có trong CLAUDE.md mục 7); 💡 test kiến trúc cấm `FromSqlRaw` có nối chuỗi |
| **XSS** (chèn `<script>`) | Mọi trường là **chữ thuần**: lưu nguyên văn, **mã hóa khi hiển thị** (React tự escape). Không có trường HTML nào ở thời điểm này — xem mục 6.1 |
| Chuỗi bẩn | Tự `Trim()`, chuẩn hóa Unicode (NFC — quan trọng với tiếng Việt / tiếng Nhật để so sánh trùng), loại ký tự điều khiển |
| **Upload file độc** | Kiểm tra đuôi trong danh sách cho phép **và** magic bytes (nội dung thật), giới hạn dung lượng ⚙️, đổi tên ngẫu nhiên, lưu S3/MinIO (không lưu trong thư mục web), tải xuống qua link tạm có hạn |
| Path traversal (`../../`) | Không dùng tên file người dùng gửi để ghép đường dẫn |
| Id giả mạo | Id kiểu strongly-typed (Guid); kiểm tra phạm vi ở tầng phân quyền (mục 3) |
| Phân trang lạm dụng (`pageSize=100000`) | `pageSize ≤ 100` (CLAUDE.md mục 8), `sort` chỉ nhận cột trong danh sách cho phép |

Lỗi validate trả 400 dạng:
```json
{ "code": "validation.failed", "title": "Dữ liệu không hợp lệ",
  "errors": { "phone": ["Số điện thoại không đúng định dạng"] }, "traceId": "…" }
```

Phân biệt: **validator** kiểm tra *hình dạng* dữ liệu (không cần DB); **domain** kiểm tra *quy tắc nghiệp vụ*
(lớp đã đủ chỗ, lead đã có Sale…) và trả `Result` lỗi.

### 6.1 Trường Rich Text / HTML — rà soát 2026-11-17

**Kết luận: hiện KHÔNG có trường nào cần HTML → chưa thêm HTML Sanitizer.** Không chuyển trường `text` nào sang HTML
khi chưa có yêu cầu thật.

Rà soát [database.md](database.md) và giao diện `frontend/src`: không có trình soạn Rich Text nào (không có
TipTap / Quill / Lexical, không có `contentEditable`, không có `dangerouslySetInnerHTML`). Mọi trường dài đều là
`TextArea` chữ thuần, hiển thị bằng `whitespace-pre-line` (giữ xuống dòng, không hiểu thẻ HTML).

| Nhóm nội dung thường cần Rich Text | Hiện trạng trong hệ thống | Quyết định |
|---|---|---|
| Mô tả khóa học | `academic.courses.description` — `TextArea` ≤ 1.000 ký tự (`CourseFormFields.tsx`) | Chữ thuần |
| Mô tả lộ trình | `academic.curricula.description` — `TextArea` ≤ 1.000 ký tự (`CurriculumFormFields.tsx`) | Chữ thuần |
| Nội dung bài học | **Không có trường nội dung**: `lessons` / `chapters` chỉ có tiêu đề + thứ tự; nội dung là file đính kèm (`materials`: pdf, ppt, audio, video, link) | Không áp dụng |
| Thông báo lớp | `academic.class_announcements.content` — `TextArea` ≤ 1.000 ký tự; gửi cả qua **Zalo** (không hiển thị HTML) | Chữ thuần |
| Mẫu tin nhắn | `crm.message_templates.body` — Zalo / SMS, có biến `{ten}` | **Luôn** chữ thuần (Zalo/SMS không hiển thị HTML) |
| Mẫu email | **Chưa có** bảng / màn hình | Chưa làm |
| Chính sách / nội quy / điều khoản | **Chưa có** bảng / màn hình | Chưa làm |
| Ghi chú, lý do, nhận xét, báo cáo buổi, bài làm chữ, mô tả công việc… | `TextArea` chữ thuần | Chữ thuần — không bao giờ cho HTML |

**Khi nào mới thêm Rich Text** (vd làm mẫu email, trang nội quy, hoặc nội dung bài học có bảng từ vựng / furigana):
1. Viết ADR: trường nào, vì sao cần định dạng, thêm thư viện `HtmlSanitizer` (Ganss.Xss, MIT).
2. Đặt tên cột rõ ràng có đuôi `_html` (vd `body_html`) để ai đọc schema cũng biết trường này chứa HTML.
3. **Lọc ở server khi ghi** bằng allowlist — chỉ cho phép:
   - Thẻ: `p br strong em u s h2 h3 ul ol li blockquote a table thead tbody tr th td ruby rt rp img`
   - Thuộc tính: `a[href]` (chỉ `https:`, `mailto:`), `img[src, alt]` (chỉ link file của hệ thống), `th/td[colspan, rowspan]`
   - Cấm: `script style iframe object embed form input`, mọi `on*=`, `style=`, `javascript:`, `data:`
   - Tự thêm `rel="noopener noreferrer"` cho link.
4. Frontend chỉ render HTML ở **đúng component** của trường đó; ngoài ra cấm `dangerouslySetInnerHTML` (luật ESLint).
5. Gửi kèm Zalo / SMS → chuyển HTML sang chữ thuần trước khi gửi.

---

## 7. Xử lý dữ liệu trả về (R1) 🧑

| Việc | Cách làm |
|---|---|
| **Định dạng thống nhất** | Thành công: trả thẳng DTO (danh sách: `{ items, total, page, pageSize }`). Lỗi: luôn ProblemDetails `{ code, title, errors?, traceId }`. Chuyển bằng `ResultExtensions.ToHttpResult()` — endpoint không tự chọn mã HTTP |
| **Bảng mã lỗi → HTTP** | Validation 400 · Unauthorized 401 · Forbidden 403 · NotFound 404 · Conflict (trùng, sửa đè `xmin`) 409 · Business rule 422 · TooManyRequests 429 · lỗi hệ thống 500 |
| **Không lộ lỗi hệ thống** | Môi trường production: 500 chỉ trả `"Có lỗi hệ thống, mã tra cứu: <traceId>"`; chi tiết nằm trong log Seq |
| 💡 **Che dữ liệu nhạy cảm** 📝 *chưa làm — làm khi có module Crm / Finance* | Người không có quyền xem đầy đủ → SĐT `0912***678`, số tài khoản `****1234`. Đánh dấu thuộc tính DTO `[Sensitive(MaskType.Phone, "crm.leads.view_contact")]`, filter tự che |
| **Ẩn dữ liệu nội bộ ở cổng** | Endpoint cổng GV / HV / PH dùng **DTO riêng**, không có trường lịch sử chăm sóc, học phí, ghi chú nội bộ (CLAUDE.md mục 6). DTO riêng là chính; che dữ liệu chỉ là lưới an toàn |
| **Không trả trường thừa** | Không bao giờ trả entity; không trả `password_hash`, `token`, `is_deleted`… |
| 💡 **Header** | `Cache-Control: no-store` cho dữ liệu cá nhân; `X-Request-Id`; nén gzip/brotli cho response lớn |
| Ngày giờ | ISO 8601 có múi giờ; ngày thuần `YYYY-MM-DD`; giờ `HH:mm` (khớp frontend) |

---

## 8. Audit log — Nhật ký hệ thống (F3) 🧑

### Hai loại log, đừng trộn
| | **Log kỹ thuật** (Serilog → Seq) | **Audit log** (bảng `system.audit_logs`) |
|---|---|---|
| Cho ai đọc | Lập trình viên | Quản lý / chủ trung tâm (trang "Nhật ký hệ thống") |
| Nội dung | Mọi request, thời gian xử lý, lỗi, stack trace | Ai làm **hành động nghiệp vụ** gì, trên đối tượng nào, thay đổi gì |
| Lưu bao lâu | ⚙️ 30 ngày | Theo bảng "Hạn lưu" bên dưới |
| Sửa / xóa | Tự xoay vòng | **Chỉ thêm**, không sửa, không xóa |

### Ghi ở hai chỗ
1. **AuditFilter (endpoint filter)** — ghi *hành động*: ai, vai trò, chi nhánh, endpoint, kết quả (thành công / mã lỗi),
   IP, thiết bị, `traceId`. Endpoint khai báo:
   ```csharp
   group.MapDelete("/{id}", DeleteStudent.Handle)
        .RequirePermission("students.list", PermissionAction.Delete)
        .Audited("Xóa học viên", AuditCategory.Data, AuditSeverity.Warning);
   ```
2. **DataChangeAuditInterceptor (EF Core)** — gom *thay đổi dữ liệu* `{field, before, after}` của entity `IAuditable`
   trong request, rồi **gắn vào chính dòng nhật ký của hành động** ở bước 1 (1 hành động = 1 dòng, đủ trước → sau).
   Chốt khi code: không ghi cùng transaction (audit_logs ở schema `system`, dữ liệu ở schema module khác);
   chỉ ghi khi hành động **thành công**. Handler tự ghi được qua `IAuditTrail` (vd đăng nhập sai).

### Ghi những gì (cột theo [database.md](database.md) bảng `audit_logs`)
| Nhóm (`category`) | Sự kiện | Mức |
|---|---|---|
| `auth` | Đăng nhập thành công / thất bại, đăng xuất, đổi mật khẩu, khóa do sai nhiều lần | info / warning / critical |
| `permission` | Cấp / thu quyền, đổi vai trò, bị từ chối 403 | warning / critical |
| `data` | Tạo / sửa / xóa dữ liệu nghiệp vụ (không ghi GET thông thường) | info / warning |
| `export` | Xuất Excel / báo cáo, tải danh sách SĐT | warning |
| `finance` | Thu tiền, hoàn tiền, hủy hóa đơn, duyệt maker–checker | warning / critical |
| `setting` | Đổi Cài đặt chung, cấu hình tích hợp | warning |
| 💡 `security` | Vượt rate limit lặp lại, token bị dùng lại, dò Id | warning / critical |

### Hạn lưu — ĐÃ CHỐT
| Nhóm | Hạn lưu ⚙️ | Lý do |
|---|---|---|
| `finance` | **10 năm** | Đối chiếu thu / hoàn tiền, hủy hóa đơn — khớp thời hạn lưu tài liệu kế toán lâu nhất thường gặp |
| `permission`, `setting` | **3 năm** | Truy lại ai cấp quyền / đổi cấu hình khi có sự cố |
| `auth`, `data`, `export`, `security` | **1 năm** | Đủ để điều tra sự việc trong năm |

Job Quartz chạy hằng tháng xóa các partition / dòng quá hạn (đây là việc **duy nhất** được xóa trên bảng audit,
chạy bằng tài khoản DB riêng có quyền xóa).

### Quy tắc
- 💡 **Không bao giờ ghi bí mật**: tự che `password`, `token`, `otp`, `secret`, số thẻ… trước khi lưu `changes` / `detail`.
- 💡 **Không làm chậm request**: audit hành động đẩy vào hàng đợi (`Channel<T>`) → `BackgroundService` ghi theo lô.
  Audit thay đổi dữ liệu thì ghi cùng transaction (chấp nhận chậm một chút để đảm bảo đầy đủ).
- 💡 **Chống sửa nhật ký**: user DB của ứng dụng chỉ có quyền `INSERT`, `SELECT` trên `audit_logs`
  (không `UPDATE`, `DELETE`); chia partition theo tháng để truy vấn nhanh và dọn theo hạn lưu.
- `actor_name`, `actor_role` lưu cứng tại thời điểm thao tác (nhân viên đổi tên / nghỉ việc vẫn xem lại được).

---

## 9. 💡 Các middleware bổ sung đề xuất

| Middleware | Tác dụng | Ưu tiên |
|---|---|---|
| **CorrelationId** (M2) | Mỗi request có `X-Request-Id`, trả về cho frontend, có trong log + audit + lỗi. Người dùng báo lỗi kèm mã → tra ra ngay | Cao |
| **Global exception handler** (M3) | Một chỗ bắt mọi exception → ProblemDetails 500, ghi log đầy đủ | Cao |
| **Idempotency** (F1) | Chống tạo trùng khi bấm nhiều lần (mục 5) | Cao |
| **Security headers** (M4) | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Content-Security-Policy`, HSTS | Trung bình |
| **CORS allowlist** (M5) | Chỉ domain frontend chính thức; không dùng `*` khi có cookie | Cao |
| **Request timeout** | Request quá ⚙️ 30 giây bị hủy (báo cáo nặng chạy nền thay vì giữ kết nối) | Trung bình |
| **Health check** `/health` | Kiểm tra DB, MinIO, Seq — cho Docker / giám sát | Trung bình |
| **Chế độ bảo trì** | Bật trong Cài đặt → mọi request trả 503 + thông báo, trừ quản trị viên | Thấp |
| **Đo hiệu năng** (OpenTelemetry) | Thời gian xử lý từng endpoint, cảnh báo endpoint chậm | Thấp |

---

## 10. Vị trí code (theo [module-template.md](module-template.md) & [shared-helpers.md](shared-helpers.md))

Toàn bộ là hạ tầng dùng chung → nằm trong **BuildingBlocks**, không chứa nghiệp vụ.

| Thành phần | Project |
|---|---|
| Ghép pipeline: `AddRequestPipeline()` / `UseRequestPipeline()` | `JPCenter.Api.Common/RequestPipelineExtensions.cs` |
| `CorrelationIdMiddleware`, `SecurityHeadersMiddleware` | `JPCenter.Api.Common/Middleware/` |
| `GlobalExceptionHandler`, `ProblemFactory` | `JPCenter.Api.Common/Errors/` |
| JWT (`AddJpAuthentication`, `AccessTokenIssuer`), `CurrentUserMiddleware`, `UserAccessCache`, `RequirePermission()` / `RequireSignedIn()` / `RequirePortal()` | `JPCenter.Api.Common/Security/` |
| `AddJpRateLimiting`, `RateLimitPolicies`, `PenaltyBox`, `PenaltyMiddleware` | `JPCenter.Api.Common/RateLimiting/` |
| `.Idempotent()` | `JPCenter.Api.Common/Idempotency/` |
| `.Audited()` | `JPCenter.Api.Common/Auditing/` |
| `NormalizedStringConverter`, `BodySizeMiddleware`, `.WithBodyLimit()` | `JPCenter.Api.Common/Validation/` |
| `ICurrentUser`, `UserAccess`, `IUserAccessProvider`, `IAuditTrail`, `IIdempotencyStore` | `JPCenter.Application.Common` |
| `AuditCategory`, `AuditSeverity`, `PermissionScope` | `JPCenter.SharedKernel/Primitives` (mọi module dùng) |
| `ChannelAuditWriter` + `AuditBackgroundWriter`, `DataChangeAuditInterceptor`, `InMemoryIdempotencyStore` | `JPCenter.Infrastructure.Common` |
| Lưu nhật ký vào `audit_logs` (`IAuditSink`), nạp quyền (`IUserAccessProvider`), Idempotency trong PostgreSQL | Module **System** (giai đoạn 3) |

---

## 11. Kiểm thử bắt buộc

- Không token / token hết hạn / token sai chữ ký / token đã thu hồi → 401.
- Có token nhưng thiếu quyền → 403; dữ liệu ngoài chi nhánh / không phải con mình → 404.
- Gửi 11 request ghi trong 10 giây → request thứ 11 nhận 429 + `Retry-After`.
- Nhập file 5.000 dòng → 1 request, không bị 429; gửi 2 lần nhập cùng lúc → lần 2 bị từ chối; có đúng 1 dòng audit tổng hợp.
- Gửi 2 request cùng `Idempotency-Key` → chỉ tạo 1 bản ghi, response giống nhau.
- Body có trường lạ / quá lớn / sai Content-Type → 400 / 413 / 415.
- Thao tác có `.Audited()` → có đúng 1 dòng `audit_logs`, không chứa mật khẩu / token.
- Lỗi 500 ở production không lộ stack trace, có `traceId`.
- Test kiến trúc: mọi endpoint phải có `RequirePermission`, `RequireSignedIn`, `RequirePortal` hoặc `AllowAnonymous`.

Đã có: `tests/JPCenter.IntegrationTests/Middleware/RequestPipelineTests.cs` (20 test — token, quyền, rate limit, phạt,
Idempotency, audit, trường lạ, chuẩn hóa chuỗi, 413, 500, mã truy vết, header, CORS).

---

## 12. Quyết định đã chốt (2026-11-17)

| # | Câu hỏi | Quyết định |
|---|---|---|
| 1 | Nhiều instance? Redis? | **1 instance, không Redis** — mọi bộ đếm / cache để bộ nhớ trong (mục 4) |
| 2 | Ngưỡng rate limit với nhập liệu hàng loạt | Hàng loạt = 1 request nhiều dòng + policy `import` riêng (mục 4.1) |
| 3 | Hạn lưu audit log | Tài chính 10 năm · quyền / cài đặt 3 năm · còn lại 1 năm (mục 8) |
| 4 | Đăng nhập 2 lớp | **Không làm** |
| 5 | Trường HTML | **Chưa có trường nào** — chưa thêm HtmlSanitizer (mục 6.1) |
