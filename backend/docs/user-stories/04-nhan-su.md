# 04 · Quản lý nhân viên & giáo viên

Module: **Hr** · Bảng: `hr.employees`, `hr.teacher_profiles` ([database.md](../database.md))
Frontend: `pages/staff/hr/employees/PeoplePage.tsx` (dùng chung cho Nhân viên và Giáo viên), `pages/staff/hr/teachers/TeachersPage.tsx`,
`features/hr/EmployeeDrawer.tsx` (hồ sơ), `features/hr/EmployeeFormFields.tsx` (form + quy tắc), `features/hr/hrStore.ts`, `features/hr/types.ts`

**Không thuộc file này** (viết story riêng sau): Chấm công (`hr.timesheets`), Đơn từ & phê duyệt (`hr.requests`), Tuyển dụng (`hr.recruitment`),
Onboarding (`hr.onboarding`), Xếp ca (`work.*`), Lương (`finance.*-payroll`). Ở đây chỉ nêu chỗ các phần đó **đọc** hồ sơ nhân viên.

## Mô hình (đọc trước)

```
Employee (1 bảng cho cả 3 loại — kind)
 ├─ kind:  staff (nhân viên) · teacher (giáo viên) · assistant (trợ giảng)
 ├─ code:  NV0001 / GV0001 / TG0001 — tự sinh, không đổi
 ├─ branch_id (chi nhánh chính) · department · position · manager_id (cây quản lý)
 ├─ shift_based (chỉ staff: làm theo ca → có trong Xếp ca)
 ├─ contract: type · no · start · end? · base_salary? · allowance?   (hợp đồng HIỆN TẠI)
 ├─ leave_balance (phép năm còn)
 ├─ status:  probation ──▶ active ◀──▶ leave (nghỉ dài hạn)
 │                └──────────┴──────────▶ quit (đã nghỉ — không quay lại)
 └─ teacher (chỉ teacher / assistant): nationality vn|jp · levels · certificates · rate offline/online
                                       · availability (thứ × ca) · extra_branch_ids · bio
```

**Quy tắc vàng:**
1. **Không xóa** hồ sơ nhân viên — nghỉ việc thì chuyển `quit`, giữ để tra cứu lương, chấm công, lớp đã dạy.
2. `kind` và `code` **không đổi** sau khi tạo (nhân viên chuyển sang dạy → tạo hồ sơ GV riêng hoặc ghi chú; không đổi loại).
3. Hồ sơ nhân viên ≠ tài khoản đăng nhập: tài khoản cấp ở **Phân quyền người dùng** (PQ-08). Nghỉ việc → tài khoản **tự khóa** (PQ-08.E6).
4. Lương, phụ cấp, đơn giá là **thông tin nhạy cảm** — chỉ người có quyền sửa hồ sơ hoặc quyền lương mới thấy (NS-05.E3 🆕).
5. Mọi thay đổi ghi `audit_logs` (`category = data`; lương / hợp đồng / nghỉ việc mức `warning`).

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| NS-01 | Xem danh sách nhân viên | HR, quản lý | Cao |
| NS-02 | Xem danh sách giáo viên & trợ giảng | HR, giáo vụ | Cao |
| NS-03 | Thêm hồ sơ nhân viên / giáo viên / trợ giảng | HR | Cao |
| NS-04 | Sửa hồ sơ | HR | Cao |
| NS-05 | Xem chi tiết hồ sơ | HR, quản lý, giáo vụ | Cao |
| NS-06 | Gia hạn / ký hợp đồng mới | HR | Cao |
| NS-07 | Cho nghỉ việc | HR, quản lý | Cao |
| NS-08 | Cập nhật lịch rảnh giáo viên | HR, giáo vụ | TB |
| NS-09 | Theo dõi hợp đồng sắp hết hạn | HR | TB |
| NS-10 | Nghỉ dài hạn / quay lại làm việc | HR | Thấp |

**Quyền** (theo trang): nhân viên `hr.employees.*` · giáo viên & trợ giảng `hr.teachers.*` — `view` xem · `create` thêm · `update` sửa, gia hạn,
nghỉ việc, lịch rảnh · `export` xuất Excel. Phạm vi (PQ-10): `own` = mình + cấp dưới theo cây `manager_id` 🆕 · `branch` = nhân sự có chi nhánh chính
thuộc chi nhánh được gán · `all` = toàn hệ thống.

---

## NS-01 · Xem danh sách nhân viên

> Là **HR / quản lý**, tôi muốn **xem danh sách nhân viên theo phòng ban, trạng thái** để **nắm nhân sự và việc cần xử lý (hợp đồng sắp hết, chưa có tài khoản)**.

**Nguồn:** Nhân sự › Nhân viên · **Quyền cần:** `hr.employees.view`

**Các bước:**
1. Danh sách nhân viên (`kind = staff`) của chi nhánh đang chọn (thanh trên cùng; "Toàn hệ thống" = tất cả trong phạm vi).
2. Thẻ số liệu (không tính người đã nghỉ): đang làm việc (+ số thử việc) · hợp đồng hết hạn ≤ 45 ngày · chưa có tài khoản · thử việc.
   Bấm thẻ → lọc theo thẻ đó.
3. Nhóm theo **phòng ban** (chip có số lượng): Ban giám đốc, Giáo vụ, Tuyển sinh, Marketing, Kế toán, Lễ tân, IT / Cơ sở vật chất.
4. Cột: nhân viên (mã · vị trí) · chi nhánh · phòng ban · hợp đồng (loại, đến ngày, còn N ngày) · giờ làm (theo ca / hành chính) · phép còn · tài khoản · trạng thái.
5. Tìm theo tên, mã, SĐT, email, vị trí; lọc trạng thái; ô "Hiện người đã nghỉ" (mặc định ẩn); sắp xếp theo tên, ngày hết hợp đồng.
6. Bấm 1 dòng → hồ sơ (NS-05).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-01.E1 | Người xem chỉ có `view` | Ẩn nút Thêm / Sửa |
| NS-01.E2 | Mở link `?open=<id>` tới người đã nghỉ | Tự bật "Hiện người đã nghỉ" rồi mở hồ sơ |
| NS-01.E3 | Hồ sơ ngoài phạm vi (link trực tiếp) | 404 (PQ-10.E2) |
| NS-01.E4 | "Chưa có tài khoản" | Lấy theo tài khoản thật ở System (PQ-08), không chỉ dựa cờ `has_account` — xem "Dữ liệu cần bổ sung" |
| NS-01.E5 | Xuất Excel 🆕 | Cần `hr.employees.export`; cột lương chỉ có khi được xem lương (NS-05.E3); ghi nhật ký `category = export` (PQ-10.E4) |

---

## NS-02 · Xem danh sách giáo viên & trợ giảng

> Là **HR / giáo vụ**, tôi muốn **xem giáo viên theo cấp độ dạy, giờ dạy, đánh giá** để **xếp lớp, tìm người dạy thay và theo dõi chất lượng**.

**Nguồn:** Nhân sự › Giáo viên · **Quyền cần:** `hr.teachers.view`

**Các bước:**
1. Danh sách `kind = teacher | assistant` của chi nhánh đang chọn; nhóm: GV Việt · GV người Nhật · Trợ giảng.
2. Thẻ: GV & trợ giảng đang làm (+ thử việc) · hợp đồng hết hạn ≤ 45 ngày · tổng giờ dạy tháng này · thử việc.
3. Cột: giáo viên (mã · loại) · chi nhánh · dạy cấp (N5…N1) · chứng chỉ · đơn giá / giờ (offline, online) · lớp đang dạy · giờ dạy tháng · đánh giá TB · hợp đồng · trạng thái.
4. Lọc thêm theo **cấp độ dạy**.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-02.E1 | Lớp đang dạy, giờ dạy, đánh giá | Đọc từ module Đào tạo (GV lớp / trợ giảng, chấm công theo buổi, đánh giá của học viên) — hiện "—" tới khi có module này ⏳ |
| NS-02.E2 | GV dạy thêm ở chi nhánh khác (`extra_branch_ids`) 🆕 | Hiện cả ở danh sách chi nhánh dạy thêm (nhãn "dạy thêm") — dùng cho xếp lớp / dạy thay |
| NS-02.E3 | Người xem không được xem lương | Ẩn cột đơn giá (NS-05.E3) |

---

## NS-03 · Thêm hồ sơ nhân viên / giáo viên / trợ giảng

> Là **HR**, tôi muốn **tạo hồ sơ cho người mới** để **xếp ca, xếp lớp, chấm công và cấp tài khoản cho họ**.

**Quyền cần:** `hr.employees.create` (nhân viên) · `hr.teachers.create` (giáo viên, trợ giảng)

**Các bước:**
1. Bấm "Thêm nhân viên" / "Thêm giáo viên" / "Thêm trợ giảng". Gợi ý: nhân viên mới nên tạo qua **Tuyển dụng → Onboarding** để có đủ checklist.
2. Nhập:
   - **Cá nhân:** họ tên, giới tính, SĐT (10 số, bắt đầu 0), email, ngày sinh, chi nhánh chính (chỉ chi nhánh `active`).
   - **Công việc:** phòng ban (chỉ nhân viên; GV / trợ giảng luôn "Giảng dạy"), vị trí, quản lý trực tiếp, trạng thái (mặc định Thử việc), làm theo ca (chỉ nhân viên).
   - **Giảng dạy** (GV / trợ giảng): GV Việt / Nhật, cấp độ dạy (≥ 1), chi nhánh dạy thêm (khác chi nhánh chính), bằng cấp, đơn giá offline & online / giờ, giới thiệu (hiện cho học viên, ≤ 500 ký tự).
   - **Hợp đồng:** loại (mặc định Thử việc), từ ngày, đến ngày (bắt buộc trừ "Không xác định thời hạn"; mặc định +2 tháng), lương cơ bản, phụ cấp, ghi chú.
3. Hệ thống sinh **mã** theo loại (NV / GV / TG + 4 số), số hợp đồng, `joined_at` = ngày bắt đầu hợp đồng, phép năm mặc định **12 ngày**, lịch rảnh trống.
4. Ghi nhật ký "Thêm nhân viên"; thông báo "Đã tạo NV0123 — nhớ cấp tài khoản ở Hệ thống › Phân quyền người dùng".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-03.E1 | Thiếu họ tên / email | `hr.employee.name_required` / `hr.employee.email_required` |
| NS-03.E2 | SĐT sai định dạng | "SĐT 10 số" (`hr.employee.invalid_phone`) |
| NS-03.E3 | Email trùng người khác 🆕 | `hr.employee.duplicate_email` — email dùng làm email đăng nhập (PQ-08) |
| NS-03.E4 | Hợp đồng có thời hạn mà thiếu "đến ngày" | `hr.employee.contract_end_required` |
| NS-03.E5 | "Đến ngày" trước "từ ngày" 🆕 | `hr.employee.contract_dates` |
| NS-03.E6 | GV / trợ giảng chưa chọn cấp độ dạy / thiếu đơn giá | `hr.employee.levels_required` / lỗi theo trường; đơn giá ≥ 0 |
| NS-03.E7 | Chi nhánh chính đã đóng / chưa khai trương 🆕 | `hr.employee.branch_inactive` (chi nhánh `preparing` được phép — tuyển trước khai trương) |
| NS-03.E8 | Chi nhánh dạy thêm trùng chi nhánh chính | Tự bỏ khỏi danh sách dạy thêm |
| NS-03.E9 | Quản lý trực tiếp là người đã nghỉ / không tồn tại 🆕 | `hr.employee.manager_invalid` |
| NS-03.E10 | Trạng thái chọn "Đã nghỉ việc" khi tạo | Không cho — danh sách trạng thái khi tạo chỉ có Thử việc / Đang làm |

**Ghi chú:** Ô "Quản lý trực tiếp" gợi ý nhân viên thuộc Ban giám đốc (frontend); backend chấp nhận **bất kỳ** nhân viên chưa nghỉ (cây quản lý có trưởng nhóm).
Tạo từ Onboarding: Onboarding gọi cùng use case này rồi đánh dấu checklist "Tạo hồ sơ" (story Onboarding).

---

## NS-04 · Sửa hồ sơ

> Là **HR**, tôi muốn **cập nhật hồ sơ** khi **nhân viên đổi vị trí, chi nhánh, quản lý, thông tin liên hệ hoặc hồ sơ giảng dạy**.

**Quyền cần:** `hr.employees.update` / `hr.teachers.update`

**Các bước:** Nút "Sửa" (dòng danh sách hoặc hồ sơ) → form như NS-03 → Lưu → nhật ký "Sửa hồ sơ" kèm trường trước → sau (lương ghi mức `warning`).
Giữ nguyên: mã, loại, số hợp đồng, ngày vào làm, phép còn, lịch rảnh.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-04.E1 | Chọn chính mình làm quản lý | `hr.employee.self_manager` |
| NS-04.E2 | Tạo vòng quản lý (A quản lý B, B quản lý A) 🆕 | `hr.employee.manager_cycle` |
| NS-04.E3 | Đổi email khi đã có tài khoản 🆕 | Cảnh báo "Email đăng nhập vẫn là email cũ" — đổi email đăng nhập làm ở tài khoản (System) |
| NS-04.E4 | Đổi chi nhánh chính | Cho phép; cảnh báo "Vai trò & chi nhánh trong Phân quyền không tự đổi" (PQ-05). GV đang có lớp ở chi nhánh cũ → cảnh báo ⏳ |
| NS-04.E5 | Đổi trạng thái sang "Đã nghỉ việc" qua form | Không cho — dùng NS-07 (có kiểm tra, khóa tài khoản) |
| NS-04.E6 | Sửa hồ sơ người đã nghỉ 🆕 | Chỉ sửa được ghi chú / thông tin liên hệ |
| NS-04.E7 | Hai người sửa cùng lúc | `system.concurrency` (`xmin`) |
| NS-04.E8 | Bỏ cấp độ dạy khi GV đang dạy lớp cấp đó 🆕 | Cảnh báo, vẫn cho lưu ⏳ (cần Đào tạo) |

---

## NS-05 · Xem chi tiết hồ sơ

> Là **HR / quản lý / giáo vụ**, tôi muốn **xem đầy đủ hồ sơ một người** để **trả lời nhanh về hợp đồng, phép, chấm công, lớp đang dạy**.

**Quyền cần:** `hr.employees.view` / `hr.teachers.view`

**Các tab:**
| Tab | Nội dung | Ai thấy |
|---|---|---|
| Hồ sơ | Mã, vị trí · phòng ban, chi nhánh, quản lý trực tiếp, SĐT, email, ngày sinh, ngày vào làm (số tháng), làm theo ca, phép còn, ghi chú · hợp đồng hiện tại (loại, hạn, còn N ngày, lương, phụ cấp) · tình trạng tài khoản đăng nhập (nút "Cấp tài khoản" / "Phân quyền" → PQ-05/08) | Mọi tab: có quyền xem trang |
| Chấm công tháng này | Ngày công, đi muộn, về sớm, quên check-out, vắng, nghỉ phép + chi tiết từng ngày | Chỉ nhân viên (`staff`); dữ liệu từ story Chấm công |
| Lịch rảnh | Bảng thứ × ca (NS-08) | GV / trợ giảng |
| Lớp & giờ dạy | Giờ dạy tháng, số buổi, lương dạy tạm tính, danh sách lớp | GV / trợ giảng ⏳ Đào tạo |
| Đánh giá | Điểm TB, nhận xét của học viên | Chỉ GV ⏳ Đào tạo |

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-05.E1 | Hợp đồng còn ≤ 45 ngày / đã hết hạn | Khung hợp đồng màu cảnh báo, ghi "còn N ngày" / "đã hết hạn" |
| NS-05.E2 | Người đã nghỉ | Ẩn nút Gia hạn, Cho nghỉ việc, Cấp tài khoản |
| NS-05.E3 | Thông tin lương 🆕 | Lương cơ bản, phụ cấp, đơn giá, "lương dạy tạm tính" **chỉ trả về** khi người xem có `update` trang đó hoặc `finance.staff-payroll` / `finance.teacher-payroll` `view`; người khác thấy "Ẩn" |
| NS-05.E4 | Xem hồ sơ của chính mình | Làm ở trang **Cá nhân › Hồ sơ của tôi** (không cần mã quyền, PQ-10.E5) — không qua trang này |

---

## NS-06 · Gia hạn / ký hợp đồng mới

> Là **HR**, tôi muốn **gia hạn hoặc ký hợp đồng mới** khi **hợp đồng sắp hết hoặc nhân viên qua thử việc**.

**Quyền cần:** `hr.employees.update` / `hr.teachers.update`

**Các bước:**
1. Hồ sơ → "Gia hạn / ký mới" → nhập ngày kết thúc mới (mặc định +12 tháng), lương cơ bản mới.
2. Hệ thống tạo hợp đồng mới: số hợp đồng mới, bắt đầu = ngày sau khi hợp đồng cũ kết thúc.
3. Đang **thử việc** → chuyển **Đang làm**, loại hợp đồng Thử việc → Xác định thời hạn.
4. Nhật ký mức `warning` (hợp đồng cũ → mới, lương cũ → mới).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-06.E1 | Ngày kết thúc mới ≤ ngày bắt đầu mới | `hr.employee.contract_dates` |
| NS-06.E2 | Ký hợp đồng không thời hạn 🆕 | Cho chọn loại "Không xác định thời hạn" → không cần ngày kết thúc |
| NS-06.E3 | Hợp đồng cũ không có ngày kết thúc | Bắt đầu = hôm nay |
| NS-06.E4 | Người đã nghỉ | Không cho (`hr.employee.already_quit`) |
| NS-06.E5 | Cần xem lại các hợp đồng trước 🆕 | Lưu **lịch sử hợp đồng** (xem "Dữ liệu cần bổ sung") |

---

## NS-07 · Cho nghỉ việc

> Là **HR / quản lý**, tôi muốn **ghi nhận nhân viên nghỉ việc** để **khóa tài khoản, gỡ khỏi xếp ca / xếp lớp và giữ hồ sơ để tra cứu**.

**Quyền cần:** `hr.employees.update` / `hr.teachers.update`

**Các bước:**
1. Hồ sơ → "Cho nghỉ việc" → hộp xác nhận nêu hậu quả, **lý do bắt buộc**; 🆕 chọn ngày nghỉ (mặc định hôm nay).
2. Hệ thống: `status = quit`, `quit_at`, ghi lý do vào ghi chú → nhật ký mức `warning`.
3. Phát integration event **`EmployeeQuit`** → System **khóa tài khoản** + thu hồi mọi phiên (PQ-08.E6); Xếp ca gỡ khỏi lịch từ ngày nghỉ;
   Đào tạo không cho chọn khi xếp lớp / dạy thay.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-07.E1 | Đã nghỉ rồi | `hr.employee.already_quit` |
| NS-07.E2 | GV đang dạy lớp chưa kết thúc | Cho nghỉ nhưng cảnh báo "Đang dạy N lớp — xếp GV thay" ⏳ |
| NS-07.E3 | Đang là quản lý trực tiếp của người khác 🆕 | Cảnh báo "N người đang báo cáo cho … — đổi quản lý" (không tự đổi) |
| NS-07.E4 | Đang là quản lý chi nhánh 🆕 | Cảnh báo; thẻ chi nhánh hiện "Chưa có" quản lý (CN-01.E1) |
| NS-07.E5 | Còn lead / task đang giữ ⏳ | Cảnh báo "Bàn giao N lead, M task" (CRM / Công việc) |
| NS-07.E6 | Ngày nghỉ trong tương lai 🆕 | Hồ sơ vẫn `active` tới ngày đó; job hằng ngày chuyển `quit` + phát event đúng ngày |
| NS-07.E7 | Nghỉ việc là Super Admin cuối cùng | System từ chối khóa (PQ-08.E5) → HR được báo phải cấp Super Admin cho người khác trước |

---

## NS-08 · Cập nhật lịch rảnh giáo viên

> Là **HR / giáo vụ**, tôi muốn **đánh dấu ca giáo viên có thể nhận dạy** để **xếp lớp mới và mời dạy thay đúng người**.

**Quyền cần:** `hr.teachers.update`

**Các bước:** Hồ sơ GV → tab Lịch rảnh → bảng T2…CN × ca (sáng 08:00–12:00 · chiều 13:00–17:30 · tối) → bấm ô để bật / tắt "Rảnh" → lưu ngay.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-08.E1 | Người chỉ có `view` | Bảng chỉ đọc |
| NS-08.E2 | Tắt ca mà GV đang có lớp cố định ca đó 🆕 | Cảnh báo "Đang dạy lớp … vào ca này" ⏳ — vẫn cho lưu |
| NS-08.E3 | Bấm liên tục nhiều ô 🆕 | Gom thành 1 lần lưu (debounce) hoặc nút "Lưu lịch rảnh" — tránh mỗi ô 1 request |
| NS-08.E4 | GV tự khai lịch rảnh 🆕 | Đề xuất cho sau: cổng GV tự khai, HR duyệt |

---

## NS-09 · Theo dõi hợp đồng sắp hết hạn

> Là **HR**, tôi muốn **biết hợp đồng nào sắp hết hạn** để **gia hạn kịp, không để nhân viên làm việc khi hợp đồng đã hết**.

**Quyền cần:** `hr.employees.view` / `hr.teachers.view`

**Các bước:** Thẻ "HĐ hết hạn ≤ 45 ngày" trên NS-01 / NS-02 → bấm để lọc; cột hợp đồng ghi "còn N ngày" / "hết hạn"; sắp xếp theo ngày hết hạn.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-09.E1 | Nhắc tự động 🆕 | Job hằng ngày: còn **45 / 15 / 7 ngày** → thông báo người có `hr.employees.update` của chi nhánh đó + quản lý trực tiếp |
| NS-09.E2 | Hợp đồng đã quá hạn mà chưa gia hạn | Đánh dấu đỏ "đã hết hạn"; vẫn đếm trong thẻ |
| NS-09.E3 | Hợp đồng không thời hạn / người đã nghỉ | Không tính |

---

## NS-10 · Nghỉ dài hạn / quay lại làm việc

> Là **HR**, tôi muốn **đánh dấu nhân viên nghỉ dài hạn (thai sản, ốm dài ngày)** để **họ không bị xếp ca / xếp lớp nhưng vẫn giữ hồ sơ và tài khoản**.

**Quyền cần:** `hr.employees.update` / `hr.teachers.update`

**Các bước:** Sửa hồ sơ → trạng thái "Nghỉ dài hạn" (hoặc nút riêng 🆕) → hồ sơ `leave`; quay lại → "Đang làm".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NS-10.E1 | Đang nghỉ dài hạn | Không xuất hiện khi xếp ca / chọn GV dạy thay; tài khoản **không** bị khóa |
| NS-10.E2 | Chuyển từ `quit` sang `leave` / `active` | Không cho — người đã nghỉ quay lại thì tạo hồ sơ mới (giữ lịch sử cũ) |

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/hr/...`) | Quyền |
|---|---|---|
| NS-01, NS-02 | `GET employees?kind=staff|teaching&branchId=&status=&department=&level=&includeQuit=&q=&page=` | `hr.employees.view` / `hr.teachers.view` (theo `kind`) |
| NS-05 | `GET employees/{id}` | như trên |
| NS-03 | `POST employees` | `.create` theo `kind` |
| NS-04 | `PUT employees/{id}` `{ …, version }` | `.update` |
| NS-06 | `POST employees/{id}/contracts` | `.update` |
| NS-07 | `POST employees/{id}/quit` `{ reason, quitAt }` | `.update` |
| NS-08 | `PUT employees/{id}/availability` | `hr.teachers.update` |
| NS-10 | `POST employees/{id}/long-leave` · `…/return` | `.update` |

- JSON khớp `Employee` ở `frontend/src/features/hr/types.ts` (`contract`, `teacher.rate`, `teacher.availability` = thứ 1–7 → ca).
- Quyền theo `kind`: thao tác trên GV / trợ giảng kiểm `hr.teachers.*`, trên nhân viên kiểm `hr.employees.*` (endpoint kiểm trong handler vì phụ thuộc dữ liệu).
- Module khác đọc qua `IEmployeeDirectory` (Hr.Contracts — đã có `FindAsync`, `FindManyAsync`, `ListActiveAsync`); 🆕 thêm `ListTeachersAsync(branchId, level, shift)` cho xếp lớp / dạy thay.
- Integration event 🆕: `EmployeeQuit` (System khóa tài khoản, Work gỡ ca), `EmployeeBranchChanged`.

## Dữ liệu cần bổ sung

| Nhu cầu | Đề xuất | Story |
|---|---|---|
| Lịch sử hợp đồng | Bảng 🆕 `hr.employment_contracts` (employee_id, no, type, start, end, base_salary, allowance, created_by) — `employees.contract_*` giữ hợp đồng hiện tại | NS-06.E5 |
| Email không trùng | UQ `employees.email` (đã có) | NS-03.E3 |
| Mã tự sinh theo loại | Sequence riêng NV / GV / TG (hoặc đếm max + 1 trong transaction) | NS-03 |
| Ngày nghỉ tương lai | `quit_at` > hôm nay + job hằng ngày | NS-07.E6 |
| "Đã có tài khoản" | Bỏ dần cờ `has_account` (dễ lệch): danh sách hỏi System qua 🆕 `System.Contracts › IAccountDirectory.HasAccountAsync(employeeIds)`; hoặc giữ cờ và cập nhật bằng event `StaffAccountCreated` | NS-01.E4 |
| Lý do nghỉ việc | 🆕 cột `quit_reason` (thay vì ghi vào `note`) | NS-07 |

## Đã làm (backend, 2026-11-17)

Code: `src/Modules/Hr/JPCenter.Modules.Hr/Application/Employees/`, `Api/Endpoints/EmployeeEndpoints.cs` ·
Test: `tests/JPCenter.IntegrationTests/Hr/HrFlowTests.cs`, `tests/Modules/Hr/JPCenter.Modules.Hr.Tests/Domain/EmployeeTests.cs`.

2 nhóm đường dẫn trên cùng 1 bảng, mỗi nhóm 1 mã quyền (khớp 2 trang frontend): `/hr/employees` → `hr.employees.*` (chỉ `kind = staff`),
`/hr/teachers` → `hr.teachers.*` (`teacher`, `assistant`). Hồ sơ khác nhóm / ngoài phạm vi → **404**.

| Story | API (`/api/v1/hr/employees` hoặc `/api/v1/hr/teachers`) | Quyền |
|---|---|---|
| NS-01, NS-02, NS-09 | `GET ?branchId=&group=&status=&level=&special=ending\|no_account&includeQuit=&q=&page=&pageSize=` → `{ items, total, page, pageSize, stats, groups }` | `.view` |
| NS-05 | `GET {id}` (kèm `contractHistory`) | `.view` |
| NS-03 | `POST` `{ kind?, fullName, gender, dob, phone, email, branchId, department, position, managerId, shiftBased, status, contract, teacher?, note }` | `.create` |
| NS-04 | `PUT {id}` `{ …như trên trừ kind / contract, version }` → `{ employee, warnings }` | `.update` |
| NS-06 | `POST {id}/contracts` `{ type?, end?, baseSalary?, allowance? }` (bỏ trống = giữ như cũ) | `.update` |
| NS-07 | `POST {id}/quit` `{ reason, quitAt? }` → `{ employee, warnings }` | `.update` |
| NS-08 | `PUT /hr/teachers/{id}/availability` `{ availability: { "1": ["morning"], … } }` | `hr.teachers.update` |
| NS-10 | `POST {id}/long-leave` · `POST {id}/return` | `.update` |

Job hằng ngày `hr-daily` (01:00): nhắc hợp đồng còn **đúng** 45 / 15 / 7 ngày (email gom theo người nhận: người có `<trang>.update` ở chi nhánh chính
+ quản lý trực tiếp) và chuyển "đã nghỉ" cho người hẹn ngày nghỉ đã tới.

**Giữa module:** Hr phát `EmployeeQuit` → System khóa tài khoản + thu hồi phiên (bỏ qua Super Admin cuối cùng, ghi nhật ký critical).
System phát `StaffAccountCreated` khi mời tài khoản → Hr bật `hasAccount`. Hr đọc `IBranchDirectory`, `IStaffAccessDirectory`
(`WithPermissionAsync`, `IsLastActiveSuperAdminAsync`) từ System.Contracts.

**Chốt khi code:**
- Lọc phạm vi (`all` / `branch` / `own`) làm **trong bộ nhớ** ở `EmployeeAccess`, không dùng global query filter: 1 bảng phục vụ 2 trang khác mã quyền,
  số nhân sự 1 trung tâm nhỏ. `branch` tính cả chi nhánh dạy thêm của GV; `own` = chính mình + mọi cấp dưới theo cây quản lý.
- Ẩn lương (NS-05.E3): không có `<trang>.update` và không có `finance.<staff|teacher>-payroll.view` → lương, phụ cấp, đơn giá trả `null`, `salaryHidden = true`.
- Trạng thái "nghỉ việc" chỉ qua `quit` (thêm / sửa gửi `quit` → `hr.employee.status_not_allowed`). Người đã nghỉ: sửa chỉ đổi SĐT + ghi chú (kèm cảnh báo).
- Thêm / chuyển nhân sự chỉ vào chi nhánh mình được gán (`hr.employee.branch_not_allowed`, 403). Chi nhánh đã đóng → `hr.employee.branch_inactive`.
- Số hợp đồng tự sinh `HĐ-{năm}/{mã}-{số thứ tự}`; hợp đồng cũ vào `hr.employment_contracts`. Đang thử việc + hợp đồng mới khác thử việc → "đang làm".
- Ngày nghỉ ở tương lai: chỉ hẹn (`quitScheduled = true`), tài khoản bị khóa khi job chuyển trạng thái.
- ⏳ chờ module khác: NS-07.E2 lớp đang dạy (Đào tạo), NS-07.E5 lead / task đang giữ (CRM, Công việc); `EmployeeBranchChanged`,
  `IEmployeeDirectory.ListTeachersAsync` (làm khi có module Đào tạo cần); xuất Excel (`.export`).

**Frontend (giai đoạn 11):** `frontend/src/features/hr/hrApi.ts` — trang Nhân viên / Giáo viên đọc hồ sơ trong phạm vi quyền
(`useHrVisibility`), thêm / sửa, gia hạn hợp đồng (+ lịch sử), cho nghỉ việc, nghỉ dài hạn / quay lại, lịch rảnh GV qua API; ẩn đơn giá khi
`salaryHidden`; form bỏ trạng thái "Đã nghỉ". ⏳ Chưa nối: chọn ngày hẹn nghỉ trên giao diện (API đã có `quitAt`), tạo hồ sơ từ Onboarding,
trang Hồ sơ cá nhân (vẫn dữ liệu giả).
