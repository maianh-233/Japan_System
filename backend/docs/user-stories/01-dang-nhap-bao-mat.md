# 01 · Đăng nhập & bảo mật tài khoản

Module: **System** · Bảng: `system.user_accounts`, `system.portal_accounts`, `system.audit_logs` ([database.md](../database.md))
Frontend: `pages/auth/login/*`, `pages/common/account/SecurityPage.tsx`, `features/auth/authApi.ts`

**Hai loại tài khoản đăng nhập chung 1 màn hình:**

| Loại | Ai | Đăng nhập bằng | Vào cổng |
|---|---|---|---|
| Tài khoản nhân viên (`user_accounts`) | Nhân viên, giáo viên, trợ giảng | Email | Nhân viên `/admin` — riêng giáo viên (`employee.kind = teacher`) vào Cổng GV `/teacher` |
| Tài khoản cổng (`portal_accounts`) | Học viên, phụ huynh | Tên đăng nhập hoặc email | Học viên `/student` · Phụ huynh `/parent` |

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| AUTH-01 | Đăng nhập | Mọi người dùng | Cao |
| AUTH-02 | Ghi nhớ đăng nhập & hết phiên | Mọi người dùng | Cao |
| AUTH-03 | Đăng xuất | Mọi người dùng | Cao |
| AUTH-04 | Quên mật khẩu (OTP qua email) | Mọi người dùng | Cao |
| AUTH-05 | Kích hoạt tài khoản được mời | Nhân viên mới | Cao |
| AUTH-06 | Đổi mật khẩu | Mọi người dùng | TB |
| AUTH-07 | Xác thực 2 lớp (2FA) | Nhân viên | TB |
| AUTH-08 | Thiết bị đang đăng nhập & đăng xuất từ xa | Mọi người dùng | TB |
| AUTH-09 | Lịch sử đăng nhập | Mọi người dùng | Thấp |
| AUTH-10 | Cấp tài khoản Cổng học viên / phụ huynh | Giáo vụ | Cao |
| AUTH-11 | Khóa tạm khi nhập sai mật khẩu nhiều lần 🆕 | Hệ thống | Cao |

---

## AUTH-01 · Đăng nhập

> Là **người dùng (nhân viên / GV / HV / PH)**, tôi muốn **đăng nhập bằng email hoặc tên đăng nhập + mật khẩu** để **vào đúng cổng làm việc của mình**.

**Nguồn:** `/login` · **Quyền cần:** không · **Ưu tiên:** Cao

**Điều kiện trước:** Đã có tài khoản ở trạng thái `active`.

**Các bước:**
1. Người dùng mở `/login`, nhập **email / tên đăng nhập** và **mật khẩu**, chọn (hoặc bỏ) "Ghi nhớ đăng nhập".
2. Hệ thống chuẩn hóa định danh (bỏ khoảng trắng, chữ thường).
3. Hệ thống tìm tài khoản: trước tiên `user_accounts.email`, không có thì `portal_accounts.username` / `email`.
4. So khớp mật khẩu với `password_hash`.
5. Kiểm tra trạng thái tài khoản (xem bảng dưới).
6. *Nhân viên:* tính **quyền hiệu lực** ([PQ-07](02-phan-quyen.md#pq-07--xem-quyền-hiệu-lực-của-người-dùng)), chi nhánh được gán, phạm vi (toàn hệ thống nếu có role gán "mọi chi nhánh").
   *Cổng:* lấy hồ sơ chủ tài khoản (học viên / phụ huynh), chi nhánh theo học viên.
7. Nếu tài khoản bật 2FA và đăng nhập từ **thiết bị mới** → sang [AUTH-07](#auth-07--xác-thực-2-lớp-2fa).
8. Cấp phiên (access token ngắn hạn + refresh token), ghi `last_login_at`, ghi nhật ký `audit_logs` (`category = auth`, "Đăng nhập").
9. Chuyển về trang chủ của cổng: `/admin/dashboard/overview` · `/teacher/dashboard` · `/student/dashboard` · `/parent/dashboard`
   (nếu trước đó bị chuyển về login từ 1 trang cụ thể → quay lại trang đó, miễn là thuộc cổng của mình).

**Trường hợp đặc biệt:**

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-01.E1 | Sai mật khẩu **hoặc** không tồn tại tài khoản | Cùng 1 thông báo "Email hoặc mật khẩu không đúng." (`auth.invalid_credentials`) — **không** tiết lộ email có tồn tại hay không. Tăng bộ đếm sai ([AUTH-11](#auth-11--khóa-tạm-khi-nhập-sai-mật-khẩu-nhiều-lần-)) |
| AUTH-01.E2 | Tài khoản bị khóa (`locked`) | "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." (`auth.account_locked`). Chỉ báo khi **mật khẩu đúng** |
| AUTH-01.E3 | Tài khoản nhân viên đã mời, chưa kích hoạt (`invited`) | "Tài khoản đang chờ kích hoạt." (`auth.account_invited`) + gợi ý mở email mời / nhờ quản trị gửi lại ([AUTH-05](#auth-05--kích-hoạt-tài-khoản-được-mời)) |
| AUTH-01.E4 | Tài khoản cổng ngừng (`inactive` — HV đã nghỉ học) | "Tài khoản đã ngừng hoạt động." (`auth.account_inactive`) |
| AUTH-01.E5 | Nhân viên có hồ sơ nhưng chưa có tài khoản / chưa gán role nào | "Tài khoản chưa được cấp quyền đăng nhập." (`auth.access_not_found`) |
| AUTH-01.E6 | Nhân viên đã nghỉ việc (`employee.status = quit`) | Từ chối như tài khoản bị khóa; khi nhân viên nghỉ việc hệ thống tự khóa tài khoản |
| AUTH-01.E7 | Nhân viên đăng nhập được nhưng không có quyền **Xem** ở mục nào | Vào được, trang Tổng quan hiển thị "Bạn chưa được cấp quyền — liên hệ quản trị"; vẫn dùng được mục **Cá nhân** (không cần quyền) |
| AUTH-01.E8 | 🆕 Giáo viên đồng thời có role nhân viên (vd GV kiêm Giáo vụ) | Mặc định vào Cổng GV; có nút chuyển sang Cổng nhân viên (frontend mới có thư mục `pages/auth/select-portal`, chưa có trang) |
| AUTH-01.E9 | Phụ huynh có nhiều con | Vào Cổng PH thấy tất cả các con (theo `crm.lead_student_links`), chọn con ở góc trên |
| AUTH-01.E10 | Đã đăng nhập mà mở lại `/login` | Chuyển thẳng về trang chủ cổng |
| AUTH-01.E11 | Mở URL của cổng khác (vd HV mở `/admin/...`) | Chuyển về trang chủ cổng của mình, không báo lỗi chi tiết |

**Tiêu chí chấp nhận:**
- [ ] 4 loại tài khoản đăng nhập đúng cổng.
- [ ] Thông báo sai mật khẩu và không tồn tại tài khoản giống hệt nhau.
- [ ] Mỗi lần đăng nhập thành công có 1 dòng `audit_logs` (IP, thiết bị).
- [ ] Mật khẩu chỉ lưu dạng băm (Argon2id / PBKDF2), không log mật khẩu.

**API:** `POST /api/v1/system/auth/login` `{ identifier, password, remember }` → `{ accessToken, user: SessionUser }` + cookie refresh (HttpOnly).
`SessionUser` khớp `frontend/src/types/auth.ts`.

---

## AUTH-02 · Ghi nhớ đăng nhập & hết phiên

> Là **người dùng**, tôi muốn **không phải đăng nhập lại mỗi lần mở trình duyệt** (trên máy của mình) để **làm việc nhanh hơn**.

**Các bước:**
1. Đăng nhập có tích "Ghi nhớ" → refresh token sống **30 ngày**; không tích → chỉ đến khi đóng trình duyệt (cookie phiên).
2. Access token hết hạn (15 phút) → frontend tự gọi làm mới bằng refresh token, người dùng không thấy gián đoạn.
3. Mỗi lần làm mới: **xoay vòng** refresh token (token cũ vô hiệu).

**Trường hợp đặc biệt:**

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-02.E1 | Refresh token hết hạn / bị thu hồi | Về `/login`, giữ đường dẫn đang mở để quay lại sau khi đăng nhập |
| AUTH-02.E2 | Refresh token cũ (đã xoay vòng) bị dùng lại | Nghi bị đánh cắp → thu hồi **toàn bộ** phiên của tài khoản, ghi `audit_logs` mức `critical` |
| AUTH-02.E3 | Tài khoản bị khóa / đổi quyền khi đang đăng nhập | Lần làm mới token kế tiếp bị từ chối (khóa) hoặc nhận quyền mới (đổi quyền) — tối đa sau 15 phút |
| AUTH-02.E4 | Đang nhập form dở thì hết phiên | Hiện hộp "Phiên đăng nhập đã hết" để đăng nhập lại tại chỗ, không mất dữ liệu đang nhập |

---

## AUTH-03 · Đăng xuất

> Là **người dùng**, tôi muốn **đăng xuất** để **người khác dùng chung máy không vào được tài khoản của tôi**.

**Các bước:** Bấm ảnh đại diện → "Đăng xuất" → xác nhận → hệ thống thu hồi refresh token của thiết bị này, xóa dữ liệu phiên phía trình duyệt, về `/login`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-03.E1 | Đăng xuất khi mất mạng | Vẫn xóa phiên phía trình duyệt; token phía server tự hết hạn |

---

## AUTH-04 · Quên mật khẩu (OTP qua email)

> Là **người dùng quên mật khẩu**, tôi muốn **đặt lại mật khẩu bằng mã gửi về email** để **tự lấy lại tài khoản mà không cần gọi quản trị**.

**Nguồn:** `ForgotPasswordDialog.tsx` · **Ưu tiên:** Cao

**Các bước:**
1. Ở `/login` bấm "Quên mật khẩu?", nhập email.
2. Hệ thống gửi **OTP 6 số** về email, hiển thị email đã che (`ng****@gmail.com`), nút "Gửi lại" mở sau **60 giây**.
3. Người dùng nhập OTP → hệ thống kiểm tra (hạn **5 phút**, tối đa **5 lần** sai).
4. OTP đúng → nhập mật khẩu mới + nhập lại → lưu, xóa trạng thái OTP.
5. Thu hồi mọi phiên đang đăng nhập của tài khoản, gửi email báo "Mật khẩu vừa được đổi", ghi nhật ký.
6. Quay về đăng nhập.

**Trường hợp đặc biệt:**

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-04.E1 | Email không tồn tại | Luôn trả như email thật: "Nếu email có tài khoản, mã đã được gửi" (chống dò email); vẫn tạo `one_time_tokens` giả, mã nào cũng sai. Frontend mock đã sửa theo (2026-11-17) |
| AUTH-04.E2 | Tài khoản bị khóa | Không gửi OTP; thông báo chung như E1, ghi nhật ký `warning` |
| AUTH-04.E3 | Nhập sai OTP | "Mã OTP không đúng. Còn N lần thử." (`auth.otp_invalid`) |
| AUTH-04.E4 | Sai quá 5 lần | Hủy OTP, "Nhập sai quá nhiều lần. Vui lòng gửi lại mã mới." (`auth.otp_locked`) |
| AUTH-04.E5 | OTP quá 5 phút | "Mã OTP đã hết hạn (5 phút). Vui lòng gửi lại." (`auth.otp_expired`) |
| AUTH-04.E6 | Gửi lại mã | Mã cũ hết hiệu lực ngay; tối đa 5 lần gửi / giờ / email |
| AUTH-04.E7 | Gọi bước đặt mật khẩu khi chưa xác thực OTP | Từ chối `auth.otp_required` |
| AUTH-04.E8 | Mật khẩu mới yếu | Tối thiểu 8 ký tự, có chữ và số; không trùng email / tên đăng nhập |
| AUTH-04.E9 | Học viên nhỏ không có email | Không tự đặt lại được → phụ huynh / giáo vụ đặt lại giúp ([AUTH-10.E3](#auth-10--cấp-tài-khoản-cổng-học-viên--phụ-huynh)) |
| AUTH-04.E10 | 🆕 Tài khoản cổng chỉ có SĐT (phụ huynh) | Cho nhập **tên đăng nhập hoặc SĐT** thay email → gửi OTP qua Zalo ZNS, lỗi thì SMS; các bước sau giống email |
| AUTH-04.E11 | Đặt lại mật khẩu thành công | Xóa khóa tạm do nhập sai ([AUTH-11](#auth-11--khóa-tạm-khi-nhập-sai-mật-khẩu-nhiều-lần-)), bỏ cờ bắt đổi mật khẩu lần đầu |
| AUTH-04.E12 | Tài khoản bật 2FA | Sau khi đặt lại vẫn phải qua 2FA ở lần đăng nhập kế tiếp (quên mật khẩu không bỏ qua 2FA) |
| AUTH-04.E13 | Tài khoản `invited` chưa kích hoạt | Không gửi OTP; gửi lại email mời thay thế ([AUTH-05](#auth-05--kích-hoạt-tài-khoản-được-mời)) |

**API:** `POST /auth/password/forgot` → `POST /auth/password/verify-otp` (trả `resetToken` ngắn hạn) → `POST /auth/password/reset { resetToken, newPassword }`.
OTP lưu băm, có hạn (cache / bảng tạm) — không lưu OTP dạng rõ.

---

## AUTH-05 · Kích hoạt tài khoản được mời

> Là **nhân viên mới**, tôi muốn **nhận email mời và tự đặt mật khẩu** để **bắt đầu dùng hệ thống**.

**Điều kiện trước:** Quản trị đã cấp tài khoản cho nhân viên ([PQ-08](02-phan-quyen.md#pq-08--cấp-tài-khoản-cho-nhân-viên-khóa--mở-khóa)) → `status = invited`.

**Các bước:**
1. Hệ thống gửi email mời có link kích hoạt (hạn **72 giờ**, dùng 1 lần).
2. Nhân viên bấm link → trang đặt mật khẩu (thấy tên, email, vai trò được gán).
3. Đặt mật khẩu → `status = active` → tự đăng nhập.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-05.E1 | Link hết hạn / đã dùng | "Link kích hoạt không còn hiệu lực" + nút nhờ quản trị gửi lại |
| AUTH-05.E2 | Quản trị bấm "Gửi lại lời mời" | Link cũ vô hiệu, gửi link mới |
| AUTH-05.E3 | Tài khoản bị khóa trước khi kích hoạt | Link không dùng được |
| AUTH-05.E4 | Nhân viên đến từ Onboarding | Tài khoản tạo tự động khi hoàn tất onboarding (module Hr phát event → System tạo tài khoản `invited`) |

---

## AUTH-06 · Đổi mật khẩu

> Là **người dùng đang đăng nhập**, tôi muốn **đổi mật khẩu** để **bảo vệ tài khoản**.

**Nguồn:** Cá nhân › Mật khẩu & bảo mật · **Quyền cần:** không (trang cá nhân)

**Các bước:** Nhập mật khẩu hiện tại + mật khẩu mới + nhập lại → lưu → hỏi "Đăng xuất các thiết bị khác?" (nên làm nếu nghi lộ mật khẩu) → ghi nhật ký.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-06.E1 | Mật khẩu hiện tại sai | "Mật khẩu hiện tại không đúng." (`auth.wrong_password`); tính vào bộ đếm sai |
| AUTH-06.E2 | Mật khẩu mới trùng mật khẩu cũ | "Mật khẩu mới phải khác mật khẩu hiện tại." (`auth.same_password`) |
| AUTH-06.E3 | Chọn đăng xuất thiết bị khác | Thu hồi mọi refresh token trừ thiết bị hiện tại |

---

## AUTH-07 · Xác thực 2 lớp (2FA)

> Là **nhân viên (đặc biệt quản lý, kế toán)**, tôi muốn **nhập thêm mã khi đăng nhập trên thiết bị mới** để **mất mật khẩu cũng không bị chiếm tài khoản**.

**Các bước:**
1. Bật ở Mật khẩu & bảo mật → xác nhận bằng mã gửi qua SMS (số điện thoại trong hồ sơ nhân viên) → bật.
2. Lần sau đăng nhập ở **thiết bị chưa tin cậy**: sau bước mật khẩu, hệ thống gửi mã SMS 6 số → nhập đúng → vào; có ô "Tin cậy thiết bị này 30 ngày".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-07.E1 | Tài khoản có role quản lý / quyền `finance.*` chưa bật 2FA | Trang Phân quyền đếm "Chưa bật 2FA"; 🆕 có thể bật cờ bắt buộc → lần đăng nhập sau buộc thiết lập 2FA trước khi vào |
| AUTH-07.E2 | Tắt 2FA | Phải nhập mật khẩu hiện tại; ghi nhật ký mức `warning` |
| AUTH-07.E3 | Mất điện thoại | Quản trị tắt 2FA hộ (ghi nhật ký `critical`) |
| AUTH-07.E4 | Nhập sai mã 5 lần | Hủy lượt đăng nhập, quay về bước mật khẩu |
| AUTH-07.E5 | SMS gửi lỗi | Gửi qua email dự phòng (tích hợp SMS Brandname / Email) |

---

## AUTH-08 · Thiết bị đang đăng nhập & đăng xuất từ xa

> Là **người dùng**, tôi muốn **xem mình đang đăng nhập ở thiết bị nào và đăng xuất thiết bị lạ** để **chặn người lạ dùng tài khoản**.

**Các bước:** Trang Mật khẩu & bảo mật liệt kê phiên: thiết bị / trình duyệt, IP, lần hoạt động cuối, đánh dấu "Thiết bị này" → bấm đăng xuất 1 thiết bị hoặc "Đăng xuất thiết bị khác".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-08.E1 | Đăng xuất chính thiết bị đang dùng | Hỏi xác nhận rồi đăng xuất như AUTH-03 |
| AUTH-08.E2 | Phiên đã hết hạn | Không hiện trong danh sách |

---

## AUTH-09 · Lịch sử đăng nhập

> Là **người dùng**, tôi muốn **xem các lần đăng nhập gần đây** để **phát hiện đăng nhập lạ**.

Lấy từ `audit_logs` (`category = auth`) của chính mình: thời điểm, thiết bị, IP, thành công / thất bại. 30 ngày gần nhất.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-09.E1 | Có lần đăng nhập thất bại liên tiếp | Hiện cảnh báo vàng + gợi ý đổi mật khẩu |

---

## AUTH-10 · Cấp tài khoản Cổng học viên / phụ huynh

> Là **giáo vụ**, tôi muốn **cấp tài khoản cổng cho học viên và phụ huynh** để **họ xem lịch học, chuyên cần, học phí**.

**Nguồn:** Học viên › Hồ sơ (tab Tài khoản) · Phụ huynh / Người giám hộ · **Quyền cần:** `students.profiles.update` / `students.contacts.update`

**Các bước:**
1. Mở hồ sơ HV / phụ huynh → "Cấp tài khoản".
2. Hệ thống gợi ý tên đăng nhập (mã HV, hoặc SĐT phụ huynh), email nếu có.
3. Sinh mật khẩu tạm → gửi qua Zalo / SMS / email theo kênh của phụ huynh; lần đăng nhập đầu **bắt buộc đổi mật khẩu**.
4. Ghi nhật ký.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-10.E1 | Đã có tài khoản | Chỉ cho "Đặt lại mật khẩu" / "Khóa" / "Mở khóa" |
| AUTH-10.E2 | Tên đăng nhập trùng | Báo trùng, gợi ý thêm hậu tố |
| AUTH-10.E3 | HV nhỏ không có email, quên mật khẩu | Giáo vụ đặt lại mật khẩu tạm hoặc phụ huynh đặt lại cho con từ Cổng PH |
| AUTH-10.E4 | HV nghỉ học (không còn ghi danh đang học / bảo lưu) | Tài khoản cổng tự chuyển `inactive` |
| AUTH-10.E5 | Phụ huynh có nhiều con | **Một** tài khoản PH thấy tất cả các con — không cấp tài khoản PH theo từng con |
| AUTH-10.E6 | Phụ huynh bị gỡ liên kết với con | Không còn thấy dữ liệu của con đó ngay lập tức |

---

## AUTH-11 · Khóa tạm khi nhập sai mật khẩu nhiều lần 🆕

> Là **quản trị**, tôi muốn **hệ thống chặn dò mật khẩu** để **tài khoản không bị đoán mật khẩu**.

**Quy tắc:** Sai **5 lần liên tiếp** trong 15 phút → khóa tạm **15 phút** (khác với khóa bởi quản trị). Đăng nhập đúng → về 0.
Giới hạn thêm theo IP: 20 lần sai / 15 phút / IP.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| AUTH-11.E1 | Đăng nhập trong lúc bị khóa tạm | "Bạn đã nhập sai quá nhiều lần, thử lại sau N phút." (`auth.too_many_attempts`) — không kiểm tra mật khẩu |
| AUTH-11.E2 | Bị khóa tạm 3 lần trong ngày | Gửi email cảnh báo cho chủ tài khoản + nhật ký `critical` |
| AUTH-11.E3 | Đặt lại mật khẩu thành công (AUTH-04) | Xóa khóa tạm |

---

## Dữ liệu bổ sung cho nhóm này

✅ Đã thêm vào entity + [database.md](../database.md) ngày 2026-11-17 (bảng 4, 7, 13a, 13b):

| Nhu cầu | Đề xuất | Story |
|---|---|---|
| Phiên / refresh token, danh sách thiết bị | Bảng mới `system.user_sessions` (account_type, account_id, refresh_token_hash, device, ip, trusted_until, created_at, last_seen_at, revoked_at) | AUTH-02, 03, 07, 08 |
| Khóa tạm khi nhập sai | Thêm `failed_login_count`, `locked_until` vào `user_accounts` + `portal_accounts` | AUTH-11 |
| Bắt đổi mật khẩu lần đầu | Thêm `must_change_password` vào `portal_accounts` (+ `user_accounts`) | AUTH-10 |
| Bắt buộc 2FA | Thêm `two_factor_required` vào `user_accounts` | AUTH-07.E1 |
| Link mời / OTP | Bảng `system.one_time_tokens` (purpose: invite / reset / 2fa, token_hash, expires_at, attempts, used_at) — hoặc cache | AUTH-04, 05, 07 |
