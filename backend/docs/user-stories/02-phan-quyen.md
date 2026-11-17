# 02 · Vai trò & phân quyền

Module: **System** · Bảng: `system.roles`, `role_grants`, `user_accounts`, `user_role_assignments`, `permission_overrides`,
`approval_requests`, `audit_logs` ([database.md](../database.md))
Frontend: `pages/staff/system/roles/RolesPage.tsx`, `pages/staff/system/user-permissions/UserPermissionsPage.tsx`,
`features/system/UserAccessDrawer.tsx`, `features/system/access.ts` (logic quyền hiệu lực — backend tính **y hệt**), `features/system/approvals.ts`

## Mô hình quyền (đọc trước)

```
Mã quyền  = <module>.<trang>.<hành động>        vd  crm.leads.update
            hành động: view · create · update · delete · export · approve
Phạm vi   = own (của mình) < branch (chi nhánh được gán) < all (toàn hệ thống)

Role          ── grants { mã quyền → phạm vi }            (Super Admin = mọi quyền, all)
Người dùng    ── roles [{ role, chi nhánh áp dụng }]       (rỗng = mọi chi nhánh)
              └─ overrides [{ cấp / chặn 1 quyền, phạm vi, hạn, lý do }]

Quyền hiệu lực = HỢP các role (lấy phạm vi RỘNG nhất)
               + cấp lẻ còn hạn
               − chặn lẻ còn hạn        (CHẶN luôn thắng)
```

**Quy tắc vàng:**
1. Chỉ cấp được quyền **mình đang có**, với phạm vi **không rộng hơn** của mình (Super Admin cấp được mọi thứ).
2. **Không tự sửa** quyền của chính mình.
3. Role gán cho **một số chi nhánh** → quyền phạm vi `all` thu về `branch` (trừ module `system` — chỉ có ý nghĩa toàn hệ thống).
4. Cấp quyền **tài chính** (`finance.*`) → phải qua **duyệt 2 người**.
5. Mọi thay đổi quyền ghi `audit_logs` (`category = permission`).
6. Kiểm tra quyền theo **mã quyền**, không theo tên vai trò.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| PQ-01 | Xem danh sách vai trò & ma trận quyền | Quản trị | Cao |
| PQ-02 | Tạo vai trò mới | Quản trị | Cao |
| PQ-03 | Sửa ma trận quyền của vai trò | Quản trị | Cao |
| PQ-04 | Xóa vai trò | Quản trị | TB |
| PQ-05 | Gán vai trò theo chi nhánh cho người dùng | Quản trị | Cao |
| PQ-06 | Cấp / chặn quyền lẻ có thời hạn | Quản trị | TB |
| PQ-07 | Xem quyền hiệu lực của người dùng | Quản trị | Cao |
| PQ-08 | Cấp tài khoản cho nhân viên, khóa / mở khóa | Quản trị | Cao |
| PQ-09 | Duyệt cấp quyền tài chính (2 người) | Người duyệt | Cao |
| PQ-10 | Hệ thống kiểm tra quyền khi dùng chức năng | Hệ thống | Cao |
| PQ-11 | Dữ liệu theo cổng (GV / HV / PH) chỉ thấy của mình | Hệ thống | Cao |
| PQ-12 | Quyền lẻ hết hạn tự mất hiệu lực | Hệ thống | TB |

**Vai trò mặc định** (`is_system = true`, không xóa được): Super Admin · Admin · Quản lý chi nhánh · Giáo vụ · Kế toán · Sale · Marketing · Giáo viên.
Ví dụ vai trò tự tạo: Lễ tân, Trưởng nhóm Sale.

---

## PQ-01 · Xem danh sách vai trò & ma trận quyền

> Là **quản trị**, tôi muốn **xem các vai trò và quyền của từng vai trò** để **biết ai được làm gì**.

**Nguồn:** Hệ thống › Vai trò & quyền · **Quyền cần:** `system.roles.view`

**Các bước:**
1. Mở trang → danh sách role bên trái: tên, mô tả, số người đang dùng (không tính tài khoản bị khóa), nhãn "Mặc định".
2. Chọn 1 role → ma trận bên phải: **Module → Nhóm (trang) → Hành động**, mỗi ô chọn phạm vi (trống / Của mình / Chi nhánh / Toàn hệ thống).
3. Lọc nhanh theo module, tìm theo tên quyền.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-01.E1 | Role Super Admin | Hiện "Toàn quyền" — ma trận khóa, không sửa |
| PQ-01.E2 | Người xem chỉ có quyền `view` | Ma trận chỉ đọc |

---

## PQ-02 · Tạo vai trò mới

> Là **quản trị**, tôi muốn **tạo vai trò mới** (vd Lễ tân) để **gán bộ quyền phù hợp công việc**.

**Quyền cần:** `system.roles.create`

**Các bước:**
1. Bấm "Thêm vai trò" → nhập tên, mô tả; tùy chọn **sao chép quyền từ role có sẵn**.
2. Chỉnh ma trận (như PQ-03) → Lưu.
3. Ghi nhật ký "Tạo vai trò".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-02.E1 | Trùng tên role | "Tên vai trò đã tồn tại" (`system.role.duplicate_name`) |
| PQ-02.E2 | Sao chép từ role có quyền vượt quá quyền mình | Các quyền vượt bị bỏ, báo "N quyền vượt quá quyền của bạn — không sao chép" |
| PQ-02.E3 | Tên trống | `system.role.name_required` |

---

## PQ-03 · Sửa ma trận quyền của vai trò

> Là **quản trị**, tôi muốn **bật / tắt quyền và chọn phạm vi cho vai trò** để **điều chỉnh khi quy trình thay đổi**.

**Quyền cần:** `system.roles.update`

**Các bước:**
1. Chọn role → đổi phạm vi các ô (có thể bật cả hàng / cả nhóm một lần).
2. Hệ thống đánh dấu "Có thay đổi chưa lưu"; bấm **Lưu**.
3. Hệ thống kiểm tra từng ô theo quy tắc vàng 1 → lưu các ô hợp lệ.
4. Ghi nhật ký với danh sách thay đổi trước → sau.
5. Quyền mới áp dụng cho **mọi người đang dùng role** từ lần làm mới token kế tiếp (≤ 15 phút) — xem AUTH-02.E3.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-03.E1 | Sửa Super Admin | Không cho (`system.role.super_admin_locked`) |
| PQ-03.E2 | Có ô vượt quá quyền của người sửa | Ô đó **giữ nguyên giá trị cũ**, lưu phần còn lại, báo "N quyền vượt quá quyền của bạn — giữ nguyên" |
| PQ-03.E3 | Rời trang / đổi role khi chưa lưu | Hỏi "Bỏ các thay đổi chưa lưu?" |
| PQ-03.E4 | Bật quyền `update/delete/approve` mà chưa có `view` cùng trang | Tự bật `view` cùng phạm vi (không có quyền xem thì không mở được trang) |
| PQ-03.E5 | Thêm quyền `finance.*` vào role đang có người dùng | Coi như cấp quyền tài chính cho những người đó → tạo yêu cầu **duyệt 2 người** (PQ-09) thay vì lưu thẳng |
| PQ-03.E6 | Hai quản trị sửa cùng role cùng lúc | Người lưu sau nhận "Vai trò vừa được người khác sửa, tải lại" (`xmin` concurrency) |
| PQ-03.E7 | Role mặc định (`is_system`) | Sửa được quyền (trừ Super Admin), không đổi tên / xóa được |

---

## PQ-04 · Xóa vai trò

> Là **quản trị**, tôi muốn **xóa vai trò không còn dùng** để **danh sách gọn gàng**.

**Quyền cần:** `system.roles.delete`

**Các bước:** Chọn role → Xóa → xác nhận → role vào **Thùng rác** (khôi phục trong 30 ngày) → ghi nhật ký.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-04.E1 | Role mặc định | Không xóa được (`system.role.system_role`) |
| PQ-04.E2 | Role đang được gán cho người dùng (kể cả tài khoản bị khóa) | "Không xóa được role đang dùng" + danh sách người đang dùng (`system.role.in_use`) |
| PQ-04.E3 | Khôi phục từ thùng rác khi đã có role khác trùng tên | Khôi phục với tên thêm hậu tố "(khôi phục)" |

---

## PQ-05 · Gán vai trò theo chi nhánh cho người dùng

> Là **quản trị**, tôi muốn **gán vai trò cho nhân viên theo từng chi nhánh** (vd Giáo vụ ở Cầu Giấy, Sale ở Quận 1) để **họ chỉ thao tác đúng nơi làm việc**.

**Nguồn:** Hệ thống › Phân quyền người dùng → mở 1 người · **Quyền cần:** `system.user-permissions.update`

**Các bước:**
1. Danh sách người dùng: vai trò @ chi nhánh, số quyền, quyền lẻ (biểu tượng đồng hồ nếu sắp hết hạn), trạng thái tài khoản, đăng nhập gần nhất. Lọc theo role, chi nhánh, trạng thái.
2. Mở 1 người → tab Vai trò → "Thêm vai trò": chọn role + chi nhánh (bỏ trống = mọi chi nhánh).
3. Hệ thống xem trước **quyền hiệu lực sau khi đổi** (PQ-07) → Lưu.
4. Ghi nhật ký "Gán vai trò".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-05.E1 | Mở tài khoản của chính mình | Chỉ xem, báo "Đây là tài khoản của bạn — không tự sửa quyền của mình được" (`system.permission.self_edit`) |
| PQ-05.E2 | Role có quyền vượt quá người gán | Role đó bị khóa chọn trong danh sách |
| PQ-05.E3 | Role gán ở 1 vài chi nhánh mà role có quyền phạm vi `all` | Quyền thu về `branch` (trừ module `system`); giao diện ghi chú rõ |
| PQ-05.E4 | Cùng role gán 2 lần | Gộp chi nhánh vào 1 dòng |
| PQ-05.E5 | Người gán chỉ có phạm vi `branch` | Chỉ chọn được chi nhánh mình được gán; không chọn "mọi chi nhánh" |
| PQ-05.E6 | Việc gán làm người dùng **có thêm** quyền `finance.*` | Không lưu thẳng → tạo yêu cầu duyệt (PQ-09), báo "Đã gửi yêu cầu duyệt" |
| PQ-05.E7 | Gỡ hết role | Cho phép; người dùng vẫn đăng nhập được nhưng không thấy mục nào ngoài **Cá nhân** (AUTH-01.E7) |
| PQ-05.E8 | Gỡ role Super Admin của Super Admin cuối cùng 🆕 | Từ chối — hệ thống luôn phải còn ≥ 1 Super Admin đang hoạt động (`system.permission.last_super_admin`) |

---

## PQ-06 · Cấp / chặn quyền lẻ có thời hạn

> Là **quản trị**, tôi muốn **cấp thêm hoặc chặn 1 quyền cho 1 người trong thời gian nhất định** (vd cho Sale xuất Excel lead 7 ngày) để **không phải tạo role mới cho trường hợp tạm thời**.

**Quyền cần:** `system.user-permissions.update`

**Các bước:**
1. Mở người dùng → tab Quyền lẻ → "Thêm quyền lẻ".
2. Chọn **Cấp** hoặc **Chặn** → tìm quyền ("công nợ", "xuất file"...) → phạm vi (khi cấp) → hạn (bỏ trống = vô thời hạn) → **lý do (bắt buộc)**.
3. Lưu → ghi nhật ký.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-06.E1 | Cấp quyền mình không có / phạm vi rộng hơn của mình | "Bạn chỉ cấp được quyền mình đang có, với phạm vi không rộng hơn của mình" (`system.permission.exceeds_granter`); danh sách chọn chỉ hiện quyền cấp được |
| PQ-06.E2 | Chặn quyền | Được chặn **bất kỳ** quyền nào (không cần mình có); chặn thắng mọi role và cấp lẻ |
| PQ-06.E3 | Cấp quyền `finance.*` | Thành yêu cầu duyệt 2 người (PQ-09) |
| PQ-06.E4 | Hạn ở quá khứ | `system.permission.invalid_until` |
| PQ-06.E5 | Thiếu lý do | `system.permission.reason_required` |
| PQ-06.E6 | Cấp lẻ quyền người dùng đã có qua role với phạm vi hẹp hơn | Lấy phạm vi rộng nhất; cảnh báo "quyền đã có từ role X" |
| PQ-06.E7 | Gỡ quyền lẻ trước hạn | Cho phép, ghi nhật ký |

---

## PQ-07 · Xem quyền hiệu lực của người dùng

> Là **quản trị**, tôi muốn **xem chính xác người này đang được làm gì và quyền đến từ đâu** để **trả lời "sao tôi không vào được trang X"**.

**Các bước:** Mở người dùng → tab "Quyền hiệu lực": từng mã quyền + phạm vi + **nguồn** (tên role / "Cấp lẻ") + trạng thái **Bị chặn** (lý do, hạn). Đếm tổng số quyền dùng được.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-07.E1 | Quyền đến từ 2 role khác phạm vi | Hiện phạm vi rộng nhất, liệt kê cả 2 nguồn |
| PQ-07.E2 | Quyền bị chặn | Gạch ngang, hiện lý do + hạn chặn |
| PQ-07.E3 | Quyền lẻ đã hết hạn | Không tính |

**Thuật toán** (giống `features/system/access.ts › effectivePerms`): duyệt role → áp phạm vi (thu `all` → `branch` nếu role gán theo chi nhánh, trừ `system`) → lấy rộng nhất; cộng cấp lẻ còn hạn; đánh dấu chặn lẻ còn hạn.
Menu nhân viên = các trang có quyền `.view` dùng được (không bị chặn).

---

## PQ-08 · Cấp tài khoản cho nhân viên, khóa / mở khóa

> Là **quản trị**, tôi muốn **cấp tài khoản đăng nhập cho nhân viên và khóa khi cần** để **kiểm soát ai được vào hệ thống**.

**Quyền cần:** `system.user-permissions.create` / `.update`

**Các bước — cấp tài khoản:**
1. Trang Phân quyền hiện "N nhân viên chưa có tài khoản" → "Cấp tài khoản" → chọn nhân viên + **vai trò ban đầu** + chi nhánh.
2. Tạo tài khoản `invited` với email của nhân viên → gửi email mời ([AUTH-05](01-dang-nhap-bao-mat.md#auth-05--kích-hoạt-tài-khoản-được-mời)).
3. Ghi nhật ký.

**Các bước — khóa / mở khóa:**
1. Mở người dùng → "Khóa tài khoản" → nhập **lý do (bắt buộc)** → xác nhận.
2. Hệ thống đặt `locked`, thu hồi **mọi phiên** ngay, ghi nhật ký mức `critical`.
3. "Mở khóa" → `active`, xóa lý do, ghi nhật ký `warning`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-08.E1 | Chọn vai trò ban đầu vượt quá quyền người cấp | Khóa chọn (như PQ-05.E2) |
| PQ-08.E2 | Nhân viên chưa có email | Không cấp được, nhắc cập nhật hồ sơ Nhân sự |
| PQ-08.E3 | Email đã dùng cho tài khoản khác | `system.user.duplicate_email` |
| PQ-08.E4 | Tự khóa chính mình | Không cho (`system.permission.self_edit`) |
| PQ-08.E5 | Khóa Super Admin cuối cùng 🆕 | Không cho (`system.permission.last_super_admin`) |
| PQ-08.E6 | Nhân viên nghỉ việc (Hr chuyển `quit`) | Hệ thống tự khóa, lý do "Đã nghỉ việc" (nghe event từ Hr) |
| PQ-08.E7 | Tài khoản `invited` | Có nút "Gửi lại lời mời" |
| PQ-08.E8 | Khóa tài khoản giáo viên đang có lớp | Cho khóa nhưng cảnh báo "GV đang dạy N lớp — xếp GV thay" |

---

## PQ-09 · Duyệt cấp quyền tài chính (2 người)

> Là **người duyệt thứ hai**, tôi muốn **xem và duyệt / từ chối yêu cầu cấp quyền tài chính** để **không ai một mình tự mở quyền tiền bạc**.

**Nguồn:** Hệ thống › Duyệt thao tác · **Quyền cần:** `system.approvals` + (`system.audit` hoặc `system.user-permissions`)

**Các bước:**
1. Người A thay đổi quyền khiến ai đó **có thêm** quyền `finance.*` (PQ-03.E5, PQ-05.E6, PQ-06.E3) → hệ thống **không áp dụng ngay** mà tạo `approval_requests` (`kind = finance_permission`, trạng thái `pending`), lưu thay đổi dự kiến trong `payload` (trước / sau).
2. Người duyệt mở danh sách "Chờ duyệt" → xem: ai đề nghị, cho ai, quyền gì, trước → sau, lý do.
3. **Duyệt** → hệ thống áp dụng đúng thay đổi trong payload → ghi nhật ký. **Từ chối** → nhập lý do → không áp dụng.
4. Người đề nghị nhận thông báo kết quả.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-09.E1 | Người đề nghị tự duyệt | Không cho (`system.approval.self_approval`) |
| PQ-09.E2 | Người đề nghị rút lại khi chưa ai duyệt | `withdrawn` (`system.approval.only_requester` nếu người khác rút) |
| PQ-09.E3 | Đã duyệt / từ chối rồi mà bấm lại | `system.approval.not_pending` |
| PQ-09.E4 | Lúc duyệt, quyền của người đề nghị đã bị thu hẹp (không còn cấp được) | Từ chối áp dụng, báo "Người đề nghị không còn đủ quyền để cấp" |
| PQ-09.E5 | Người được cấp đã bị khóa / nghỉ việc trước khi duyệt | Vẫn lưu quyền nhưng cảnh báo; không mở khóa tài khoản |
| PQ-09.E6 | Thay đổi KHÔNG thêm quyền tài chính (gỡ bớt, chặn) | Áp dụng ngay, không cần duyệt |

---

## PQ-10 · Hệ thống kiểm tra quyền khi dùng chức năng

> Là **chủ trung tâm**, tôi muốn **mọi thao tác đều được kiểm tra quyền ở server** để **người dùng không vượt quyền dù sửa giao diện / gọi API trực tiếp**.

**Các bước (mỗi request):**
1. Xác thực token → lấy `ICurrentUser` (Id, cổng, chi nhánh, quyền hiệu lực).
2. Endpoint khai báo `.RequirePermission("crm.leads", PermissionAction.Update)` → thiếu quyền → **403** (`system.permission.forbidden`).
3. Có quyền → lọc dữ liệu theo **phạm vi**:
   - `own`: chỉ bản ghi mình phụ trách / tạo (vd lead có `assignee_id` = mình).
   - `branch`: bản ghi thuộc chi nhánh được gán cho role chứa quyền đó.
   - `all`: không lọc chi nhánh.
4. Lọc bằng **global query filter** trong DbContext, không viết tay `Where` rải rác.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-10.E1 | Có quyền `view` nhưng không có `update` | Xem được, nút sửa ẩn; gọi API sửa → 403 |
| PQ-10.E2 | Phạm vi `branch`, mở bản ghi chi nhánh khác bằng link trực tiếp | Trả **404** (không lộ bản ghi tồn tại) |
| PQ-10.E3 | Phạm vi `own`, lead được chuyển sang Sale khác | Mất quyền thấy lead đó ngay |
| PQ-10.E4 | Xuất Excel | Cần riêng quyền `export`; dữ liệu xuất cũng lọc theo phạm vi; ghi nhật ký `category = export` |
| PQ-10.E5 | Trang **Cá nhân** (chấm công của tôi, việc của tôi...) | Không cần mã quyền — luôn chỉ dữ liệu của chính người đăng nhập |
| PQ-10.E6 | "Giao việc cấp dưới" | Không cần mã quyền; chỉ giao được cho người dưới mình theo cây `manager_id` |
| PQ-10.E7 | Người dùng chọn chi nhánh trên thanh trên cùng | Chỉ thu hẹp hiển thị trong các chi nhánh được phép, không mở rộng quyền |

---

## PQ-11 · Dữ liệu theo cổng chỉ thấy của mình

> Là **trung tâm**, tôi muốn **giáo viên, học viên, phụ huynh chỉ thấy dữ liệu của họ** để **bảo mật thông tin học viên khác và thông tin nội bộ**.

| Cổng | Thấy | KHÔNG thấy |
|---|---|---|
| Giáo viên | Lớp mình dạy (GV lớp, dạy thay, trợ giảng), HV trong các lớp đó | Học phí, lịch sử chăm sóc, ghi chú nội bộ, lớp khác |
| Học viên | Lớp / buổi / bài tập / điểm / học phí của mình | Nhận xét cuối khóa của GV, HV khác, đáp án trước khi nộp |
| Phụ huynh | Mọi thông tin học tập + học phí của **các con có liên kết** | Con của người khác, nhận xét cuối khóa |

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-11.E1 | GV dạy thay 1 buổi | Chỉ thấy lớp đó trong buổi được dạy thay (điểm danh, tài liệu buổi), không thấy toàn bộ lịch sử lớp |
| PQ-11.E2 | GV bị gỡ khỏi lớp | Mất quyền xem lớp ngay |
| PQ-11.E3 | Phụ huynh bỏ cờ liên kết với con | Mất quyền xem con đó ngay |
| PQ-11.E4 | Endpoint cổng gọi nhầm DTO nội bộ | Cổng dùng DTO riêng không có trường nội bộ (kiểm tra bằng test) |

---

## PQ-12 · Quyền lẻ hết hạn tự mất hiệu lực

> Là **quản trị**, tôi muốn **quyền cấp tạm tự hết khi tới hạn** để **không phải nhớ đi thu hồi**.

**Các bước:** Khi tính quyền hiệu lực, bỏ qua override có `until` ≤ hiện tại. Job hằng ngày: báo người cấp các quyền lẻ **sắp hết hạn trong 3 ngày**; ghi nhật ký khi hết hạn.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| PQ-12.E1 | Đang dùng thì hết hạn | Mất quyền từ lần làm mới token kế tiếp (≤ 15 phút) |
| PQ-12.E2 | Chặn lẻ hết hạn | Quyền từ role được dùng lại |

---

## Mã quyền của module System (để gán)

| Trang | Mã | Ghi chú |
|---|---|---|
| Chi nhánh | `system.branches` | |
| Vai trò & quyền | `system.roles` | |
| Phân quyền người dùng | `system.user-permissions` | |
| Tích hợp | `system.integrations` | Chỉ Super Admin mặc định |
| Nhật ký hệ thống | `system.audit` | |
| Duyệt thao tác | `system.approvals` | Kết hợp quyền theo loại yêu cầu |
| Thùng rác | `system.trash` | |
| Cài đặt chung | `system.settings` | |

Module `system` chỉ có ý nghĩa **toàn hệ thống** — gán theo chi nhánh không thu hẹp phạm vi.

## Dữ liệu cần bổ sung

| Nhu cầu | Đề xuất | Story |
|---|---|---|
| Trùng tên role | UQ `roles.name` (trong số chưa xóa) — đã ghi UQ trong database.md | PQ-02.E1 |
| Sao chép role | Không cần cột mới (copy grants khi tạo) | PQ-02 |
| Đảm bảo ≥ 1 Super Admin | Kiểm tra ở Application khi gỡ role / khóa | PQ-05.E8, PQ-08.E5 |
