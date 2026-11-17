# 03 · Quản lý chi nhánh

Module: **System** · Bảng: `system.branches`, `audit_logs` ([database.md](../database.md))
Frontend: `pages/staff/system/branches/BranchesPage.tsx`, `features/system/useBranches.ts` (danh sách chi nhánh cho ô chọn),
`features/system/systemStore.ts › saveBranch`, `features/system/constants.ts › BRANCH_STATUS`

## Mô hình (đọc trước)

```
Branch ── code (CG, Q1… — 2–4 chữ/số, viết hoa, KHÔNG đổi sau khi tạo)
       ── thông tin liên hệ: name, address, phone, email, open_hours
       ── manager_id → hr.employees (quản lý chi nhánh)
       ── bank_name / bank_account / bank_holder  → sinh VietQR trên hóa đơn
       ── invoice_prefix (mặc định = code)       → số hóa đơn HD-<prefix>-0001
       ── status:  preparing ──Khai trương──▶ active ──Đóng──▶ closed
                                                 ▲                  │
                                                 └──── Mở lại ──────┘
```

**Quy tắc vàng:**
1. Chi nhánh là **gốc của phân quyền theo chi nhánh** (PQ-05, PQ-10) — Id chi nhánh dùng ở mọi module. **Không xóa** chi nhánh, chỉ **đóng**.
2. Chỉ chi nhánh **đang hoạt động** được chọn khi tạo dữ liệu mới (lead, lớp, ghi danh, hồ sơ nhân viên…). Chi nhánh đã đóng vẫn hiện khi **tra cứu** dữ liệu cũ.
3. Mã chi nhánh và tiền tố hóa đơn là một phần của mã chứng từ → **không đổi** khi đã phát sinh chứng từ.
4. Mọi thay đổi ghi `audit_logs` (`category = setting`), có danh sách trước → sau; số tài khoản ngân hàng chỉ ghi 4 số cuối.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| CN-01 | Xem danh sách chi nhánh & số liệu tổng quan | Quản trị, quản lý | Cao |
| CN-02 | Thêm chi nhánh | Quản trị | Cao |
| CN-03 | Sửa thông tin chi nhánh | Quản trị | Cao |
| CN-04 | Khai báo tài khoản nhận học phí (VietQR) | Quản trị, kế toán trưởng | Cao |
| CN-05 | Khai trương chi nhánh | Quản trị | TB |
| CN-06 | Đóng chi nhánh | Quản trị | TB |
| CN-07 | Mở lại chi nhánh đã đóng | Quản trị | Thấp |
| CN-08 | Chọn chi nhánh làm việc & danh sách chi nhánh cho ô chọn | Mọi nhân viên | Cao |

**Quyền:** `system.branches.view` (xem) · `.create` (thêm) · `.update` (sửa, khai trương, đóng, mở lại) · `.export`.
Module `system` chỉ có phạm vi toàn hệ thống (PQ-05.E3). Dữ liệu mẫu: 4 chi nhánh CG · Q1 · HC · TD (TD đang chuẩn bị).

---

## CN-01 · Xem danh sách chi nhánh & số liệu tổng quan

> Là **quản trị / quản lý**, tôi muốn **xem mọi chi nhánh cùng số liệu chính** để **nắm tình hình từng nơi và đi nhanh tới dữ liệu của chi nhánh đó**.

**Nguồn:** Hệ thống › Chi nhánh · **Quyền cần:** `system.branches.view`

**Các bước:**
1. Đầu trang: thẻ tổng — chi nhánh hoạt động (+ số đang chuẩn bị), học viên đang học, lớp đang chạy, phòng học, nhân sự, giáo viên, đã thu, còn phải thu.
2. Lưới thẻ chi nhánh: mã, tên, trạng thái, ngày khai trương (hoặc "dự kiến khai trương"), 5 chỉ số (học viên · lớp · phòng · giáo viên · nhân sự),
   đã thu / còn phải thu, địa chỉ, SĐT, email, giờ mở cửa, quản lý, tài khoản nhận học phí + tiền tố hóa đơn, ghi chú.
3. Tìm theo tên / mã / địa chỉ / SĐT / email (không dấu); lọc theo trạng thái.
4. Lối tắt trên thẻ (đặt chi nhánh làm việc = chi nhánh đó rồi chuyển trang): Học viên · Giáo viên · Phòng học · Nhân sự · Công nợ —
   chỉ hiện khi có quyền trang đích; chi nhánh đã đóng ẩn lối tắt.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-01.E1 | Chưa có quản lý / quản lý đã nghỉ việc | Hiện "Quản lý: Chưa có" (màu cảnh báo) |
| CN-01.E2 | Chưa khai báo tài khoản ngân hàng | Hiện "Chưa khai báo tài khoản nhận học phí — không sinh được VietQR" |
| CN-01.E3 | Chi nhánh đã đóng | Thẻ mờ, viền đứt; vẫn xem được |
| CN-01.E4 | Không có chi nhánh khớp tìm kiếm | "Không tìm thấy chi nhánh phù hợp" |
| CN-01.E5 | Người xem chỉ có `view` | Ẩn nút Thêm / Sửa / Khai trương / Đóng / Mở lại |

**Số liệu theo chi nhánh** (đếm, không lưu — [database.md › Những gì KHÔNG lưu](../database.md)):

| Chỉ số | Cách tính | Module nguồn |
|---|---|---|
| Nhân sự | Nhân viên + GV + trợ giảng có `branch_id` = chi nhánh, trừ `quit` | Hr ✅ |
| Giáo viên | Như trên, `kind = teacher` | Hr ✅ |
| Phòng | Phòng thuộc chi nhánh | Academic ⏳ |
| Lớp đang chạy | Lớp `ongoing` + `enrolling` | Academic ⏳ |
| Học viên đang học | Học viên khác nhau có ghi danh `studying` ở lớp của chi nhánh | Students ⏳ |
| Đã thu / Còn phải thu | Tổng đã trả / còn lại của hóa đơn thuộc ghi danh ở chi nhánh | Finance ⏳ |

🆕 **Đề xuất backend:** API danh sách trả thông tin chi nhánh + 2 chỉ số Hr ngay; các chỉ số ⏳ thêm dần khi module tương ứng xong
(đọc qua interface trong `<Module>.Contracts`, không join bảng module khác). Trước đó frontend hiển thị "—".

---

## CN-02 · Thêm chi nhánh

> Là **quản trị**, tôi muốn **khai báo chi nhánh mới** để **chuẩn bị nhân sự, phòng, lớp trước ngày khai trương**.

**Quyền cần:** `system.branches.create`

**Các bước:**
1. Bấm "Thêm chi nhánh" → nhập: tên, mã (2–4 chữ / số), địa chỉ, điện thoại, email, quản lý (tùy chọn), giờ mở cửa
   (mặc định "T2–T7 08:00–21:30"), ngày khai trương (mặc định +30 ngày), tiền tố hóa đơn (bỏ trống = theo mã), tài khoản ngân hàng (tùy chọn — CN-04), ghi chú.
2. Hệ thống chuẩn hóa mã → **chữ hoa**; tạo chi nhánh trạng thái **`preparing`**.
3. Ghi nhật ký "Thêm chi nhánh".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-02.E1 | Thiếu mã / tên | `system.branch.code_required` / `system.branch.name_required` |
| CN-02.E2 | Mã sai định dạng (khác 2–4 chữ / số) | Lỗi theo trường "2–4 chữ / số" |
| CN-02.E3 | Trùng mã (không phân biệt hoa thường) | "Mã CG đã dùng cho chi nhánh khác" (`system.branch.duplicate_code`) |
| CN-02.E4 | Thiếu địa chỉ / SĐT / giờ mở cửa / ngày khai trương, email sai | Lỗi theo trường |
| CN-02.E5 | Tiền tố hóa đơn sai định dạng | "2–4 chữ / số" |
| CN-02.E6 | Quản lý được chọn đã nghỉ việc / không tồn tại 🆕 | `system.branch.manager_invalid` |
| CN-02.E7 | Tiền tố hóa đơn trùng chi nhánh khác 🆕 | `system.branch.duplicate_invoice_prefix` (2 chi nhánh cùng tiền tố → số hóa đơn trùng) |

**Ghi chú:** Ô "Quản lý" gợi ý nhân viên chưa nghỉ việc thuộc Ban giám đốc hoặc có vị trí chứa "quản lý" (lấy từ Hr). Frontend mock đặt Id = mã viết thường;
backend dùng GUID, `code` chỉ là mã hiển thị (CLAUDE.md mục 5).

---

## CN-03 · Sửa thông tin chi nhánh

> Là **quản trị**, tôi muốn **cập nhật thông tin chi nhánh** để **hóa đơn, thông báo gửi phụ huynh luôn đúng địa chỉ / liên hệ**.

**Quyền cần:** `system.branches.update`

**Các bước:** Bấm "Sửa" trên thẻ → form như CN-02 (**mã bị khóa**) → Lưu → nhật ký "Sửa chi nhánh" kèm từng trường trước → sau.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-03.E1 | Đổi mã | Không cho — ô mã khóa; API bỏ qua / từ chối (`system.branch.code_locked`) |
| CN-03.E2 | Đổi tiền tố hóa đơn khi chi nhánh đã có hóa đơn 🆕 | Không cho (`system.branch.invoice_prefix_locked`) — số hóa đơn đã phát hành phải liên tục |
| CN-03.E3 | Hai quản trị sửa cùng lúc | Người lưu sau nhận "Dữ liệu vừa được người khác sửa" (`system.concurrency`, `xmin`) |
| CN-03.E4 | Đổi quản lý chi nhánh | Chỉ đổi thông tin hiển thị — **không** tự gán vai trò "Quản lý chi nhánh"; phân quyền làm ở PQ-05 |
| CN-03.E5 | Đổi trạng thái qua form | Không cho — trạng thái chỉ đổi qua CN-05 / CN-06 / CN-07 |

---

## CN-04 · Khai báo tài khoản nhận học phí (VietQR)

> Là **quản trị / kế toán trưởng**, tôi muốn **khai báo tài khoản ngân hàng của từng chi nhánh** để **hóa đơn có mã QR chuyển khoản đúng tài khoản**.

**Quyền cần:** `system.branches.update` · **Nằm trong:** form CN-02 / CN-03

**Các bước:** Nhập ngân hàng, số tài khoản (6–20 chữ số), chủ tài khoản (tự viết hoa) → Lưu → nhật ký ghi "Tài khoản nhận học phí: Vietcombank · ••••1234 → BIDV · ••••5678".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-04.E1 | Nhập thiếu 1 trong 3 ô | "Cần nhập đủ ngân hàng, số tài khoản và chủ tài khoản" (`system.branch.bank_incomplete`) |
| CN-04.E2 | Số tài khoản không phải 6–20 chữ số | Lỗi theo trường |
| CN-04.E3 | Xóa trắng cả 3 ô | Cho phép → chi nhánh không sinh được VietQR (CN-01.E2) |
| CN-04.E4 | Đổi số tài khoản 🆕 | Nhật ký mức **`warning`** + báo cho người có quyền `finance.invoices` — tránh bị đổi tài khoản nhận tiền mà không ai biết |

---

## CN-05 · Khai trương chi nhánh

> Là **quản trị**, tôi muốn **chuyển chi nhánh từ "đang chuẩn bị" sang "đang hoạt động"** để **bắt đầu tuyển sinh, xếp lớp ở đó**.

**Quyền cần:** `system.branches.update`

**Các bước:** Thẻ chi nhánh `preparing` → "Khai trương" → trạng thái `active` → nhật ký → chi nhánh xuất hiện trong mọi ô chọn chi nhánh (CN-08).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-05.E1 | Chi nhánh đã `active` | `system.branch.invalid_status` |
| CN-05.E2 | Khai trương sớm hơn ngày dự kiến | Cho phép; 🆕 cập nhật `opened_at` = hôm nay |
| CN-05.E3 | Chưa có tài khoản ngân hàng / quản lý 🆕 | Vẫn cho, nhưng cảnh báo "Chưa khai báo … — hóa đơn chưa có VietQR" |

---

## CN-06 · Đóng chi nhánh

> Là **quản trị**, tôi muốn **đóng chi nhánh ngừng hoạt động** để **không ai tạo lead, ghi danh, xếp lớp mới ở đó nữa nhưng vẫn tra cứu được dữ liệu cũ**.

**Quyền cần:** `system.branches.update`

**Các bước:**
1. Thẻ chi nhánh `active` → "Đóng chi nhánh".
2. Hệ thống kiểm tra điều kiện (bảng dưới). Đạt → hộp xác nhận nêu hậu quả, **gõ mã chi nhánh** để xác nhận.
3. Trạng thái `closed` → nhật ký mức **`critical`**.
4. Hậu quả: không nhận lead / ghi danh / xếp lớp mới; dữ liệu cũ (học viên, nhân sự, phòng, hóa đơn) giữ nguyên để tra cứu; chi nhánh biến khỏi ô chọn khi tạo mới.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-06.E1 | Còn lớp đang học / đang tuyển sinh | Không cho: "Còn N lớp đang học / tuyển sinh. Kết thúc hoặc chuyển lớp trước" (`system.branch.has_active_classes`) — kiểm khi có module Đào tạo |
| CN-06.E2 | Còn nhân sự chưa nghỉ việc có chi nhánh chính là chi nhánh này | Không cho: "Còn N nhân sự chưa chuyển chi nhánh hoặc chấm dứt công tác" (`system.branch.has_staff`) |
| CN-06.E3 | Gõ sai mã xác nhận | Nút xác nhận khóa |
| CN-06.E4 | Chi nhánh đang `preparing` (bỏ dự án mở) 🆕 | Cho đóng thẳng (không có lớp / nhân sự thì không vướng E1, E2) |
| CN-06.E5 | Người dùng đang chọn chi nhánh này làm việc | Lần tải sau tự về "Toàn hệ thống" / chi nhánh đầu tiên được gán |
| CN-06.E6 | Vai trò của người dùng chỉ gán ở chi nhánh đã đóng 🆕 | Vẫn giữ gán (không tự gỡ); trang Phân quyền đánh dấu "chi nhánh đã đóng" để quản trị xử lý |

---

## CN-07 · Mở lại chi nhánh đã đóng

> Là **quản trị**, tôi muốn **mở lại chi nhánh đã đóng** để **hoạt động trở lại mà không phải tạo chi nhánh mới (giữ mã, lịch sử)**.

**Quyền cần:** `system.branches.update`

**Các bước:** Thẻ chi nhánh `closed` → "Mở lại" → trạng thái `active` → nhật ký mức `warning`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-07.E1 | Chi nhánh không ở trạng thái `closed` | `system.branch.invalid_status` |

---

## CN-08 · Chọn chi nhánh làm việc & danh sách chi nhánh cho ô chọn

> Là **nhân viên**, tôi muốn **chọn chi nhánh đang làm việc ở thanh trên cùng** để **các trang chỉ hiện dữ liệu của chi nhánh đó**.

**Quyền cần:** đăng nhập (mọi nhân viên) · **Liên quan:** PQ-10.E7

**Các bước:**
1. Sau đăng nhập: tài khoản phạm vi toàn hệ thống mặc định "Toàn hệ thống"; tài khoản theo chi nhánh mặc định chi nhánh được gán đầu tiên.
2. Ô chọn ở thanh trên cùng chỉ liệt kê chi nhánh **được phép** (chi nhánh trong các vai trò được gán; "mọi chi nhánh" → tất cả).
3. Các ô "Chi nhánh" khi **tạo mới** dữ liệu (hồ sơ nhân viên, lớp, lead…) chỉ hiện chi nhánh `active`; bộ lọc **tra cứu** hiện cả chi nhánh đã đóng.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CN-08.E1 | Chọn chi nhánh ngoài phạm vi (sửa request) | Không mở rộng quyền — server vẫn lọc theo phạm vi (PQ-10.E7) |
| CN-08.E2 | Danh sách chi nhánh cho ô chọn | API nhẹ `GET /system/branches/options` cho **mọi người đã đăng nhập** (id, mã, tên, trạng thái) — không cần `system.branches.view` |

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/system/...`) | Quyền |
|---|---|---|
| CN-01 | `GET branches?status=&q=` → `BranchProfile[]` + `stats` | `system.branches.view` |
| CN-02 | `POST branches` | `system.branches.create` |
| CN-03, CN-04 | `PUT branches/{id}` `{ …, version }` | `system.branches.update` |
| CN-05 / 06 / 07 | `POST branches/{id}/open` · `…/close` `{ confirmCode }` · `…/reopen` | `system.branches.update` |
| CN-08 | `GET branches/options` | Đăng nhập |

- Trường JSON khớp `BranchProfile` ở `frontend/src/features/system/types.ts` (`bank: { bank, account, holder }`, `openedAt` dạng `YYYY-MM-DD`).
- Module khác đọc chi nhánh qua 🆕 `IBranchDirectory` trong `System.Contracts` (tên, mã, trạng thái, tiền tố hóa đơn, tài khoản ngân hàng cho VietQR).
- Đóng / mở chi nhánh phát 🆕 integration event `BranchStatusChanged` để CRM / Đào tạo / Học viên chặn tạo mới ở chi nhánh đóng.

## Dữ liệu cần bổ sung

| Nhu cầu | Đề xuất | Story |
|---|---|---|
| Trùng mã | UQ `branches.code` (đã có) | CN-02.E3 |
| Trùng tiền tố hóa đơn | UQ `branches.invoice_prefix` | CN-02.E7 |
| Chống sửa đè | `xmin` (quy ước chung) | CN-03.E3 |
| Không cần cột mới cho số liệu | Tính khi đọc từ module nguồn | CN-01 |

## Đã làm (backend, 2026-11-17)

Code: `src/Modules/System/JPCenter.Modules.System/Application/Branches/` · Test: `tests/JPCenter.IntegrationTests/System/BranchFlowTests.cs`,
`tests/Modules/System/JPCenter.Modules.System.Tests/Domain/RoleAndBranchTests.cs`.

| Story | API (`/api/v1/system/...`) | Quyền |
|---|---|---|
| CN-01 | `GET branches?status=&q=` → `BranchDto[]` (`stats.staff`, `stats.teachers`; các chỉ số ⏳ không trả) | `system.branches.view` |
| CN-02 | `POST branches` `{ code, name, address, phone, email, managerId?, openHours, openedAt, invoicePrefix?, bank?, note? }` | `system.branches.create` |
| CN-03, CN-04 | `PUT branches/{id}` `{ …như trên trừ code, version }` | `system.branches.update` |
| CN-05 | `POST branches/{id}/open` → `{ branch, warnings }` | `system.branches.update` |
| CN-06 | `POST branches/{id}/close` `{ confirmCode }` | `system.branches.update` |
| CN-07 | `POST branches/{id}/reopen` | `system.branches.update` |
| CN-08 | `GET branches/options` → `[{ id, code, name, status }]` | Đăng nhập |

**Cho module khác** (`System.Contracts`): `IBranchDirectory` (FindAsync, ListAsync), `IStaffAccessDirectory.WithPermissionAsync(permission, branchId)`
(ai đang có 1 quyền tại 1 chi nhánh — dùng gửi email đúng người), integration event `BranchStatusChanged` (khai trương / đóng / mở lại).
`Hr.Contracts › IEmployeeDirectory.HeadcountByBranchAsync` cho số nhân sự / giáo viên.

**Chốt khi code:**
- Mã chi nhánh và tiền tố hóa đơn: 2–4 chữ / số, tự viết hoa. Sửa chi nhánh không nhận `code` (gửi lên → 400 trường lạ).
- CN-04.E4: đổi số tài khoản → nhật ký mức `warning` + email "Tài khoản nhận học phí đã thay đổi" tới người có `finance.invoices.view` ở chi nhánh đó
  (trừ người sửa). Nhật ký chỉ ghi 4 số cuối (`••••1234`) — áp chung cho mọi thuộc tính tên chứa `BankAccount` (DataChangeAuditInterceptor).
- CN-05.E2: khai trương sớm → `opened_at` = hôm nay. CN-05.E3: trả `warnings` (chưa có tài khoản ngân hàng / quản lý).
- CN-06.E2 đếm nhân sự chưa nghỉ có chi nhánh chính là chi nhánh này (mọi loại).
- ⏳ chờ module khác: CN-01 số phòng / lớp / học viên / đã thu (Đào tạo, Học viên, Tài chính); CN-03.E2 khóa tiền tố khi đã có hóa đơn (Tài chính);
  CN-06.E1 còn lớp đang chạy (Đào tạo); CN-06.E5, E6 xử lý ở giao diện / trang Phân quyền (giai đoạn nối frontend).

