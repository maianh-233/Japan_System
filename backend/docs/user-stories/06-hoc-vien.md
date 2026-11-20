# 06 · Học viên & ghi danh

Module: **Students** · Bảng: `students.students`, `enrollments`, `certificates`, `care_cases`, `care_logs`, `jlpt_sessions`, `jlpt_registrations`,
`abroad_cases`, `abroad_documents`, `abroad_notes` ([database.md](../database.md)) · Liên quan: `crm.lead_student_links` (phụ huynh), `system.portal_accounts`
Frontend: `pages/staff/students/**`, `features/student/{studentStore,types}.ts`, `features/student/components/**`, `features/student/useHideStudent.tsx`,
`features/academic/classes/detail/{ClassStudentsTab,StudentActions}.tsx`, `features/student/{atRisk,jlpt,abroad}/**`

## Mô hình (đọc trước)

```
Student (hồ sơ — tạo từ Lead ở CRM-07, hoặc nhập Excel)
 ├─ Enrollment[]  ghi danh vào 1 Lớp (Academic) ── 1 Invoice (Finance)
 │     studying ──Bảo lưu──▶ reserved ──Học lại──▶ studying
 │        │  └──Chuyển lớp──▶ transferred  (sinh Enrollment mới ở lớp đích, nối transferred_to / _from)
 │        ├──Cho nghỉ──▶ dropped           (hủy phần học phí còn lại)
 │        └──Lớp kết thúc──▶ completed     (──▶ Certificate nếu đủ điều kiện)
 ├─ PortalAccount (0..1)   tài khoản Cổng học viên
 ├─ LeadStudentLink[] ──▶ Lead = phụ huynh / người liên hệ (Crm)
 ├─ CareCase / CareLog     cảnh báo bỏ học & chăm sóc (theo từng lớp)
 ├─ JlptRegistration[]     đăng ký thi JLPT qua trung tâm
 └─ AbroadCase[]           hồ sơ du học / XKLĐ
```

**Quy tắc vàng:**
1. **Ghi danh đang có hiệu lực** = `studying` + `reserved` — dùng để tính sĩ số lớp, chặn ẩn HV, chặn ghi danh trùng.
2. Mỗi ghi danh sinh **đúng 1 hóa đơn** (Finance). Ghi danh kết thúc kiểu `dropped` / `transferred` → hóa đơn **hủy phần còn lại** (`cancelled`), tiền đã thu giữ nguyên để hoàn hoặc khấu trừ.
3. Trạng thái học tổng hợp của HV **suy ra** từ ghi danh (đang học › bảo lưu › đã học xong › đã nghỉ › chưa xếp lớp) — không lưu cột riêng.
4. Không xóa HV — chỉ **ẩn**, và chỉ khi không còn ghi danh có hiệu lực.
5. Ghi chú nội bộ, học phí, lịch sử chăm sóc, nhận xét cuối khóa **không** trả về cổng GV; cổng HV / PH chỉ thấy dữ liệu của mình / con mình (PQ-11).
6. HV dưới 18 tuổi (theo `dob`) → người quyết định là phụ huynh liên hệ chính (CRM-08).

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| HV-01 | Xem danh sách học viên | Giáo vụ, Sale, kế toán | Cao |
| HV-02 | Xem hồ sơ học viên | Giáo vụ, Sale, kế toán | Cao |
| HV-03 | Sửa thông tin học viên | Giáo vụ, Sale | Cao |
| HV-04 | Nhập học viên từ Excel | Giáo vụ | Thấp |
| HV-05 | Ẩn / bỏ ẩn học viên | Giáo vụ | TB |
| HV-06 | Ghi danh học viên có sẵn vào lớp | Giáo vụ, Sale | Cao |
| HV-07 | Bảo lưu | Giáo vụ | Cao |
| HV-08 | Học lại sau bảo lưu | Giáo vụ | Cao |
| HV-09 | Chuyển lớp (1 hoặc nhiều HV) | Giáo vụ | Cao |
| HV-10 | Cho nghỉ học | Giáo vụ | Cao |
| HV-11 | Hoàn thành khóa | Hệ thống (khi lớp kết thúc) | Cao |
| HV-12 | Quản lý phụ huynh / người giám hộ | Giáo vụ, Sale | Cao |
| HV-13 | Cấp & quản lý tài khoản cổng HV / PH | Giáo vụ | Cao |
| HV-14 | Cảnh báo bỏ học & chăm sóc | Giáo vụ, Sale | TB |
| HV-15 | Cấp chứng nhận hoàn thành | Giáo vụ | TB |
| HV-16 | Đăng ký & theo dõi thi JLPT | Giáo vụ | TB |
| HV-17 | Hồ sơ du học / XKLĐ | Tư vấn du học | Thấp |

**Quyền** (theo trang): `students.profiles` · `students.contacts` · `students.at-risk` · `students.jlpt` · `students.certificates` · `students.study-abroad`.
Thao tác ghi danh (HV-06 → HV-10) đứng ở trang Lớp học → kiểm `academic.classes.update`; đứng ở hồ sơ HV → `students.profiles.update`.
Phạm vi `own` = HV có lead liên kết do mình phụ trách (Sale) 🆕 · `branch` = HV thuộc chi nhánh.

---

## HV-01 · Xem danh sách học viên

> Là **giáo vụ**, tôi muốn **tìm nhanh học viên và thấy ngay lớp đang học, trạng thái, nợ học phí** để **trả lời phụ huynh trong vài giây**.

**Nguồn:** Học viên › Hồ sơ học viên (`StudentsPage.tsx`) · **Quyền cần:** `students.profiles.view`

**Các bước:**
1. Cột: HV (mã, tên), điện thoại, ngày sinh, chi nhánh, lớp đang học, trạng thái học, học phí (tình trạng xấu nhất), còn nợ, tài khoản cổng.
2. Tìm theo tên / mã / SĐT / email (không dấu). Lọc: chi nhánh, trạng thái học, tình trạng học phí, lớp, "hiện học viên đã ẩn".
3. Chọn nhiều → ẩn hàng loạt (bỏ qua HV bị chặn, nêu lý do).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-01.E1 | Người xem không có `finance.invoices.view` 🆕 | Ẩn cột học phí / còn nợ (API không trả) |
| HV-01.E2 | Chưa có học viên | "Hồ sơ học viên được tạo từ Lead khi khách đăng ký học" + link Tuyển sinh |
| HV-01.E3 | Tình trạng học phí | Theo thứ tự xấu nhất: quá hạn › chưa đóng › đóng một phần › đã đóng › đã hoàn › đã hủy |
| HV-01.E4 | Xuất Excel | `students.profiles.export`; nhật ký `export` |

---

## HV-02 · Xem hồ sơ học viên

> Là **nhân viên**, tôi muốn **xem mọi thông tin của 1 học viên ở một chỗ** để **tư vấn, chăm sóc, thu tiền đúng**.

**Nguồn:** `StudentDetailDrawer.tsx` · **Quyền cần:** `students.profiles.view`

**Các tab:** Thông tin (HV-03) · Lớp học (ghi danh hiện tại + lịch sử, chuyên cần, điểm, hóa đơn từng ghi danh) · Phụ huynh (HV-12) ·
Tài khoản cổng (HV-13) · Nhận xét cuối khóa của GV (**chỉ nhân viên**) · Chăm sóc (HV-14).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-02.E1 | Ghi danh đã chuyển lớp | Hiện chuỗi "Chuyển từ GVV_N4-K10 → GVV_N4-K11" (nối `transferred_from/to`) |
| HV-02.E2 | Lớp ở chi nhánh khác chi nhánh HV | Vẫn hiện; phạm vi `branch` theo chi nhánh **của HV** 🆕 |
| HV-02.E3 | HV đã ẩn | Mở được qua link trực tiếp, nhãn "Đã ẩn" |

---

## HV-03 · Sửa thông tin học viên

> Là **giáo vụ / Sale**, tôi muốn **cập nhật SĐT, email, ngày sinh, địa chỉ** để **liên lạc và xác định người quyết định đúng**.

**Nguồn:** tab Thông tin (`StudentInfoTab.tsx`) · **Quyền cần:** `students.profiles.update`

**Các bước:** Sửa họ tên*, giới tính, ngày sinh, SĐT, email, địa chỉ, chi nhánh, ghi chú nội bộ → Lưu (nút chỉ bật khi có thay đổi) → nhật ký trước → sau.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-03.E1 | Thiếu họ tên | `students.student.name_required` |
| HV-03.E2 | SĐT / email sai định dạng | "Gồm 10 số, bắt đầu bằng 0" / "Email không hợp lệ" |
| HV-03.E3 | Ngày sinh ở tương lai 🆕 | `students.student.invalid_dob` |
| HV-03.E4 | Đổi email khi đã có tài khoản cổng 🆕 | Không tự đổi tên đăng nhập; hỏi "Cập nhật email nhận thông báo của tài khoản?" |
| HV-03.E5 | Sửa đè | `xmin` → "Dữ liệu vừa được người khác sửa" |

---

## HV-04 · Nhập học viên từ Excel

> Là **giáo vụ**, tôi muốn **nhập danh sách HV cũ khi chuyển hệ thống** để **không phải gõ tay**.

**Nguồn:** `StudentImportDialog.tsx` · **Quyền cần:** `students.profiles.create`

**Các bước:** Tải mẫu → chọn file → xem trước dòng hợp lệ / lỗi → nhập (1 request) → hồ sơ ở trạng thái **chưa xếp lớp**.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-04.E1 | Dòng thiếu tên / SĐT sai | Báo lỗi theo dòng, không nhập dòng đó |
| HV-04.E2 | Trùng SĐT + họ tên với HV có sẵn 🆕 | Cảnh báo theo dòng, người dùng chọn bỏ / vẫn nhập |
| HV-04.E3 | Có cột phụ huynh 🆕 | Tạo / tìm lead theo SĐT phụ huynh và liên kết (CRM-08) |

---

## HV-05 · Ẩn / bỏ ẩn học viên

> Là **giáo vụ**, tôi muốn **ẩn HV đã nghỉ lâu** để **danh sách gọn, dữ liệu vẫn tra cứu được**.

**Nguồn:** `useHideStudent.tsx` · **Quyền cần:** `students.profiles.update`

**Các bước:** "Ẩn" → kiểm tra điều kiện → hộp xác nhận nêu: còn nợ bao nhiêu (công nợ vẫn theo dõi ở Tài chính), tài khoản cổng còn hoạt động không → `hidden = true`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-05.E1 | Còn ghi danh `studying` / `reserved` | Không cho: "Đang trong lớp GVV_N3-K12 (bảo lưu) — chỉ ẩn được khi HV không còn học lớp nào" (`students.student.still_studying`) |
| HV-05.E2 | Còn nợ | Cho ẩn, cảnh báo đỏ trong hộp xác nhận |
| HV-05.E3 | Tài khoản cổng đang hoạt động | Không tự khóa — khóa riêng ở HV-13 |
| HV-05.E4 | Ẩn hàng loạt | 1 request; trả danh sách HV bị bỏ qua + lý do |

---

## HV-06 · Ghi danh học viên có sẵn vào lớp

> Là **giáo vụ**, tôi muốn **thêm HV cũ vào lớp mới** để **HV học tiếp mà không phải qua lead**.

**Nguồn:** Lớp › tab Học viên › "Thêm học viên" · Hồ sơ HV · CRM-07 (HV mới) · **Quyền cần:** `academic.classes.update` hoặc `students.profiles.update`

**Các bước:**
1. Chọn HV (loại HV đang học lớp này), giảm giá / mã ưu đãi, hạn đóng.
2. Hệ thống: ghi danh `studying`, `enrolled_at` = hôm nay → phát **`StudentEnrolled`** → Finance tạo hóa đơn
   (giá = học phí lớp; mã `HD-{CN}-{YY}{MM}-{SO:4}` theo tiền tố chi nhánh — TC-04).
3. HV xuất hiện ở danh sách điểm danh từ buổi kế tiếp; cổng HV / PH thấy lớp mới.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-06.E1 | Lớp đầy (ghi danh hiệu lực ≥ sĩ số tối đa) | `academic.class.full` |
| HV-06.E2 | Lớp `paused` / `finished` / `cancelled` | `academic.class.closed` |
| HV-06.E3 | HV đã có ghi danh hiệu lực ở lớp này | 🆕 `students.enrollment.duplicate` |
| HV-06.E4 | Lớp đang học dở | Cho; gợi ý học phí theo **số buổi còn lại** (người dùng sửa được) |
| HV-06.E5 | HV đã ẩn | Tự bỏ ẩn khi ghi danh |
| HV-06.E6 | Giữ chỗ lên lớp (DT-17) | Chỗ giữ tạm **không** tính sĩ số — không chặn ghi danh mới |

---

## HV-07 · Bảo lưu

> Là **giáo vụ**, tôi muốn **cho HV tạm nghỉ có thời hạn mà vẫn giữ chỗ và học phí đã đóng** để **HV quay lại học tiếp**.

**Nguồn:** `StudentActions.tsx › ReserveModal` · **Quyền cần:** `academic.classes.update`

**Các bước:** Chọn "Bảo lưu đến ngày"* (mặc định +1 tháng, **tối đa 6 tháng**) + lý do* → `reserved`, `reserved_until`, ghi chú "Bảo lưu đến dd/MM/yyyy — lý do".
Hậu quả: HV vẫn chiếm chỗ; điểm danh các buổi trong thời gian bảo lưu tự là **vắng có phép**; hóa đơn giữ nguyên.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-07.E1 | Ghi danh không ở `studying` | `students.enrollment.not_studying` |
| HV-07.E2 | Ngày trong quá khứ / quá 6 tháng | 🆕 `students.enrollment.invalid_reserve_until` |
| HV-07.E3 | Hết hạn bảo lưu (job hằng ngày) | **Không** tự đổi trạng thái — báo giáo vụ "N HV hết hạn bảo lưu, cần liên hệ" |
| HV-07.E4 | Lớp kết thúc khi HV còn bảo lưu | Giữ `reserved`; giáo vụ chuyển lớp (HV-09) hoặc cho nghỉ (HV-10) |
| HV-07.E5 | Cảnh báo bỏ học | Bỏ qua HV đang bảo lưu khi tính chuyên cần |

---

## HV-08 · Học lại sau bảo lưu

**Quyền cần:** `academic.classes.update` · "Học lại (hết bảo lưu)" → xác nhận → `studying`, xóa `reserved_until`, ghi chú "Học lại từ dd/MM/yyyy".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-08.E1 | Ghi danh không ở `reserved` | `students.enrollment.not_reserved` |
| HV-08.E2 | Lớp đã kết thúc / hủy | `academic.class.closed` — phải chuyển lớp |

---

## HV-09 · Chuyển lớp

> Là **giáo vụ**, tôi muốn **chuyển HV sang lớp khác và tự tính khoản khấu trừ từ tiền đã đóng** để **không phải tính tay, không mất tiền của HV**.

**Nguồn:** `StudentActions.tsx › TransferModal`, `BulkTransferModal` · **Quyền cần:** `academic.classes.update`

**Các bước:**
1. Chọn lớp đích* (lớp `enrolling` / `ongoing` còn chỗ; ưu tiên "cùng khóa"; hiện "khai giảng dd/MM" hoặc "đã học x/y buổi" và số chỗ còn).
2. Hệ thống gợi ý: **đã đóng** − **đã dùng** (học phí lớp cũ ÷ tổng buổi × số buổi đã dạy, làm tròn 10.000đ) = **khấu trừ**.
   Học phí lớp mới gợi ý theo số buổi còn lại nếu lớp đang học. Cả hai sửa được (chính sách riêng).
3. Hạn đóng* (nếu phải đóng thêm), lý do*.
4. Trong 1 transaction: ghi danh cũ → `transferred`, `ended_at`, `transferred_to` · ghi danh mới `studying`, `transferred_from` ·
   hóa đơn cũ hủy phần còn lại · hóa đơn mới: `amount` = học phí lớp mới, `discount` = min(học phí, khấu trừ) → còn phải thu = học phí − khấu trừ.
5. **Chuyển hàng loạt**: chọn nhiều HV → 1 lớp đích, khấu trừ tính riêng từng HV, xem bảng trước khi xác nhận.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-09.E1 | Ghi danh không ở `studying` / `reserved` | `students.enrollment.already_ended` |
| HV-09.E2 | Lớp đích không đủ chỗ cho số HV chuyển | "Lớp X chỉ còn N chỗ" — `academic.class.full` |
| HV-09.E3 | Lớp đích = lớp hiện tại / lớp đã đóng | 🆕 `students.enrollment.same_class` / `academic.class.closed` |
| HV-09.E4 | Khấu trừ > học phí lớp mới | Hóa đơn mới = 0đ (đã đóng đủ); phần dư **không** tự hoàn — kế toán hoàn tay (TC-07) 🆕 hiện cảnh báo |
| HV-09.E5 | Hóa đơn cũ đang chờ duyệt hoàn tiền / hủy | Chặn chuyển: 🆕 `finance.invoice.pending_approval` |
| HV-09.E6 | Doanh số Sale | Hóa đơn mới giữ Sale của hóa đơn cũ (TC-12) |

**Giữa module:** Students phát `EnrollmentTransferred { oldEnrollmentId, newEnrollmentId, credit, newAmount, dueDate }` → Finance hủy hóa đơn cũ + lập hóa đơn mới (cùng Outbox, không gọi chéo bảng).

---

## HV-10 · Cho nghỉ học

> Là **giáo vụ**, tôi muốn **cho HV nghỉ hẳn một lớp** để **dừng công nợ và giải phóng chỗ**.

**Nguồn:** `ClassStudentsTab.tsx › drop` · **Quyền cần:** `academic.classes.update`

**Các bước:**
1. "Cho nghỉ học" → hộp xác nhận nêu: trạng thái → "Đã nghỉ"; hủy công nợ còn lại X; HV đã đóng tiền → có thể hoàn ngay sau bước này. **Lý do bắt buộc.**
2. `dropped`, `ended_at` = hôm nay, `note` = lý do → phát `EnrollmentDropped` → Finance hủy phần còn lại của hóa đơn.
3. Đã đóng tiền → mở luôn form hoàn tiền (TC-07).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-10.E1 | Ghi danh đã kết thúc | `students.enrollment.already_ended` |
| HV-10.E2 | Lớp bị hủy (DT-05) | Mọi ghi danh hiệu lực → `dropped` với lý do "Lớp bị hủy: …" |
| HV-10.E3 | Lần nghỉ cuối cùng của HV | Gợi ý ẩn HV (HV-05); không tự ẩn |
| HV-10.E4 | Đề xuất lên lớp đang mở của HV | 🆕 tự chuyển `declined` lý do "Đã nghỉ học" |

---

## HV-11 · Hoàn thành khóa

**Kích hoạt:** giáo vụ bấm "Kết thúc lớp" (DT-04) → mọi ghi danh `studying` → `completed`, `ended_at` = ngày kết thúc thực tế. Ghi danh `reserved` giữ nguyên (HV-07.E4).
Phát `EnrollmentCompleted` → cổng HV hiện "Đã học xong", trang Chứng nhận liệt kê HV đủ điều kiện (HV-15).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-11.E1 | HV còn nợ khi hoàn thành | Vẫn `completed`; công nợ giữ nguyên, chặn cấp chứng nhận (trừ ngoại lệ) |

---

## HV-12 · Quản lý phụ huynh / người giám hộ

> Là **giáo vụ**, tôi muốn **xem mỗi phụ huynh có những con nào, ai nhận hóa đơn, ai nhận thông báo** để **liên lạc đúng người**.

**Nguồn:** Học viên › Phụ huynh / Người giám hộ (`GuardiansPage.tsx`, `GuardianDetailDrawer.tsx`, `GuardianFormFields.tsx`), tab Phụ huynh trong hồ sơ HV ·
**Quyền cần:** `students.contacts.view / create / update`

**Các bước:**
1. Danh sách = lead có ≥ 1 liên kết HV: tên, SĐT, các con (quan hệ), cờ liên hệ chính / nhận hóa đơn, kênh nhận (Zalo / SMS / email), tài khoản cổng.
2. Thẻ cảnh báo: "N học viên chưa có người liên hệ chính".
3. Thêm phụ huynh: họ tên*, SĐT* (10 số), email, chi nhánh*, là … của HV*, chọn ≥ 1 HV* → tạo lead (nguồn `referral`, trạng thái `won`) + liên kết.
4. Sửa cờ liên kết như CRM-08; chọn kênh nhận thông báo (`notify_channels`).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-12.E1 | SĐT đã thuộc người liên hệ khác | "SĐT đã thuộc người liên hệ khác" — gợi ý **liên kết** lead có sẵn thay vì tạo mới |
| HV-12.E2 | Chưa chọn HV | "Chọn ít nhất 1 học viên" |
| HV-12.E3 | Đặt liên hệ chính | Mỗi HV tối đa 1 (CRM-08.E2) |
| HV-12.E4 | Dữ liệu thuộc Crm | Students **đọc** qua `Crm.Contracts › IGuardianDirectory`; ghi đi qua API Crm (không ghi bảng `crm.*`) |

---

## HV-13 · Cấp & quản lý tài khoản cổng học viên / phụ huynh

> Là **giáo vụ**, tôi muốn **cấp tài khoản Cổng học viên / phụ huynh, khóa hoặc đặt lại mật khẩu** để **HV / PH tự xem lịch, điểm, học phí**.

**Nguồn:** `AccountCard.tsx`, `StudentGuardiansTab.tsx` · **Quyền cần:** `students.profiles.update` (HV) / `students.contacts.update` (PH) · **Chi tiết:** AUTH-10

**Các bước:**
1. "Cấp tài khoản" → tên đăng nhập gợi ý (HV: mã HV; PH: `ph.{SĐT}`) → System tạo `portal_accounts` + gửi lời mời kích hoạt (AUTH-05).
2. Khóa / mở khóa (xác nhận "X sẽ không đăng nhập được Cổng … cho đến khi mở khóa") — khóa thì thu hồi mọi phiên.
3. "Đặt lại mật khẩu" → mật khẩu tạm qua SMS / email, buộc đổi ở lần đăng nhập sau (nút khóa khi tài khoản bị khóa).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-13.E1 | Đã có tài khoản | `system.portal_account.exists` (mỗi HV / PH tối đa 1) |
| HV-13.E2 | Không có SĐT lẫn email | Không cấp được — "Cần SĐT hoặc email để gửi thông tin đăng nhập" |
| HV-13.E3 | PH không còn liên kết HV nào | Tài khoản còn nhưng không thấy dữ liệu; gợi ý khóa |
| HV-13.E4 | Dữ liệu cổng | HV chỉ thấy của mình; PH thấy tất cả các con **đang liên kết** (PQ-11) |

---

## HV-14 · Cảnh báo bỏ học & chăm sóc

> Là **giáo vụ / Sale**, tôi muốn **danh sách HV có dấu hiệu bỏ học, mức rủi ro, người phụ trách gọi** để **can thiệp trước khi HV nghỉ**.

**Nguồn:** Học viên › Cảnh báo bỏ học (`AtRiskPage.tsx`, `AtRiskDrawer.tsx`, `atRisk.ts`, `studentInsights.ts`) · **Quyền cần:** `students.at-risk.view / update`

**Các bước:**
1. Hệ thống tính cho mỗi HV **đang học** (×lớp) theo ngưỡng ở Cài đặt chung (mặc định): vắng không phép ≥ 3 · vắng liên tiếp ≥ 2 ·
   chuyên cần < 80% (đã học ≥ 4 buổi) · nộp bài < 50% · điểm thi gần nhất < 60% · nợ quá hạn.
2. Điểm rủi ro → mức: **cao** (≥ 5 hoặc vắng liên tiếp ≥ 3 — liên hệ trong ngày) · **TB** (≥ 3 — trong 2–3 ngày) · **thấp** (theo dõi).
3. Mỗi trường hợp (`care_cases`, duy nhất theo HV × lớp): người chăm sóc, trạng thái (chưa liên hệ → đang liên hệ → đang theo dõi → đã ổn định), hẹn liên hệ lại.
4. "Ghi nhận liên hệ": kênh (gọi / Zalo / gặp) + nội dung* → `care_logs` (cũng hiện ở tab Học viên của lớp).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-14.E1 | Nội dung liên hệ trống | `students.care_log.note_required` |
| HV-14.E2 | Hết dấu hiệu nhưng case đang mở | Giữ case, nhãn "Không còn dấu hiệu" — người chăm sóc đóng (`resolved`) |
| HV-14.E3 | Case `resolved` rồi có dấu hiệu mới | Mở lại `new`, ghi `detected_at` mới |
| HV-14.E4 | Cách tính | **Job hằng đêm** 🆕 tính lại `risk_level`, `flags` (đọc chuyên cần / bài / điểm qua `Academic.Contracts`, nợ qua `Finance.Contracts`) |
| HV-14.E5 | Cổng GV | **Không** thấy care log / lịch sử chăm sóc (teacher-scope-limits) |
| HV-14.E6 | Vắng 2 buổi liên tiếp lúc điểm danh | Thông báo ngay cho giáo vụ (DT-09.E5), không đợi job |

---

## HV-15 · Cấp chứng nhận hoàn thành

> Là **giáo vụ**, tôi muốn **cấp và gửi chứng nhận hoàn thành khóa có mã tra cứu + QR** để **HV có giấy xác nhận và bên thứ ba kiểm tra được**.

**Nguồn:** Học viên › Chứng nhận hoàn thành (`CertificatesPage.tsx`), `StudentActions.tsx › CertificateModal` · **Quyền cần:** `students.certificates.create / update`

**Các bước:**
1. Danh sách HV lớp đã kết thúc kèm checklist điều kiện: lớp kết thúc / HV hoàn thành · chuyên cần ≥ 80% · bài thi cuối ≥ 60% (nếu có) · hết nợ học phí.
2. "Cấp & gửi" → email* → sinh mã `CN-{năm}-{mã lớp rút gọn}-{số:4}`, PDF theo mẫu, gửi email, `send_count = 1`.
3. "Gửi lại" → email (có thể khác) → `send_count + 1`. Cấp hàng loạt: bỏ qua HV không có email, nêu số lượng.
4. Trang tra cứu công khai `/verify/{code}` (QR trên PDF) 🆕 hiện họ tên, khóa, lớp, ngày cấp.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-15.E1 | Chưa đủ điều kiện | Không cho, trừ khi nhập **lý do cấp ngoại lệ** → lưu `exception`, nhật ký `warning` |
| HV-15.E2 | Ghi danh chưa `completed` | `students.certificate.not_completed` |
| HV-15.E3 | Cấp lần 2 | `students.certificate.already_issued` — chỉ "Gửi lại" |
| HV-15.E4 | Gửi email lỗi | Giữ chứng nhận; báo lỗi gửi, cho gửi lại |
| HV-15.E5 | Cổng HV | Xem / tải PDF chứng nhận của mình |

---

## HV-16 · Đăng ký & theo dõi thi JLPT

> Là **giáo vụ**, tôi muốn **gom hồ sơ, lệ phí, phiếu dự thi, kết quả và chứng chỉ gốc JLPT của HV** để **không sót hạn nộp và biết tỉ lệ đỗ theo lớp / GV**.

**Nguồn:** Học viên › Chứng chỉ JLPT (`JlptPage.tsx`, `jlpt.ts`) · **Quyền cần:** `students.jlpt.*`

**Các bước:**
1. Kỳ thi (tháng 7 & 12): ngày thi, hạn nhận hồ sơ, ngày có kết quả, lệ phí theo cấp độ, địa điểm.
2. Đăng ký (chọn nhiều HV): cấp độ, địa điểm → `registered` → checklist hồ sơ (ảnh 3×4, CCCD, phiếu ĐK đã ký) → đóng phí `paid` → nộp hội đồng `submitted` →
   nhập số báo danh `admit` → nhập điểm từng phần `done` (tự tính đỗ / trượt theo quy chế: điểm liệt từng phần + tổng tối thiểu) hoặc `absent`; hủy `cancelled`.
3. Chứng chỉ gốc: chờ về → đã về (số chứng chỉ, ngày, người nhận) → đã phát (cho HV / phụ huynh, ngày, người phát).
4. Thống kê tỉ lệ đỗ theo cấp độ / lớp / GV.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-16.E1 | Ngày kỳ thi không hợp lệ (hạn nộp sau ngày thi…) | `students.jlpt_session.invalid_dates` |
| HV-16.E2 | Nộp hội đồng khi thiếu giấy tờ | `students.jlpt.docs_missing` |
| HV-16.E3 | Chuyển trạng thái sai thứ tự | `students.jlpt.invalid_status` |
| HV-16.E4 | Nhận chứng chỉ khi chưa đỗ | `students.jlpt.not_passed` |
| HV-16.E5 | Phát chứng chỉ chưa về | `students.jlpt.cert_not_received` |
| HV-16.E6 | Đăng ký sau hạn nhận hồ sơ 🆕 | Cảnh báo, vẫn cho (trung tâm có thể nộp bổ sung) |
| HV-16.E7 | Lệ phí 🆕 | Thu ngoài hóa đơn học phí ⏳ — tạm ghi `fee`, `paid_at`; khi Finance có "phiếu thu khác" thì nối |

---

## HV-17 · Hồ sơ du học / XKLĐ

> Là **tư vấn du học**, tôi muốn **theo dõi từng hồ sơ qua các giai đoạn và giấy tờ còn thiếu** để **không lỡ kỳ nhập học / chuyến bay**.

**Nguồn:** Học viên › Du học / XKLĐ (`AbroadPage.tsx`, `AbroadDrawer.tsx`) · **Quyền cần:** `students.study-abroad.*`

**Các bước:**
1. Tạo hồ sơ cho HV: chương trình (du học / Tokutei / TTS / kỹ sư), đối tác, nơi đến, ngành nghề, kỳ `YYYY-MM`, yêu cầu tiếng Nhật, tổng phí, người phụ trách.
2. Giai đoạn: tư vấn → giấy tờ → phỏng vấn (ngày, đạt / trượt) → COE (nộp, nhận) → visa (nộp, nhận) → xuất cảnh (ngày bay).
3. Checklist giấy tờ: chưa có → đã nhận → đã kiểm tra. Ghi chú tiến trình. Phí đã đóng / tổng.
4. Hủy hồ sơ (lý do bắt buộc).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| HV-17.E1 | Thao tác trên hồ sơ đã hủy | `students.abroad.cancelled` |
| HV-17.E2 | Phỏng vấn trượt | Ở lại giai đoạn phỏng vấn, cho đặt lịch mới hoặc hủy |
| HV-17.E3 | Sang COE khi giấy tờ chưa kiểm tra hết 🆕 | Cảnh báo, vẫn cho |

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/students/...`) | Quyền |
|---|---|---|
| HV-01 | `GET students?branchId=&studyStatus=&tuitionStatus=&classId=&hidden=&q=&page=` | `students.profiles.view` |
| HV-02 | `GET students/{id}` (kèm `enrollments[]`, `guardians[]`, `account`, `evaluations[]`) | `students.profiles.view` |
| HV-03 | `PUT students/{id}` `{ …, version }` | `students.profiles.update` |
| HV-04 | `POST students/import` | `students.profiles.create` |
| HV-05 | `POST students/{id}/hide` · `…/unhide` · `POST students/hide` `{ ids }` | `students.profiles.update` |
| HV-06 | `POST enrollments` `{ studentId, classId, discount?, discountCode?, dueDate }` + `Idempotency-Key` | `academic.classes.update` \| `students.profiles.update` |
| HV-07, 08 | `POST enrollments/{id}/reserve` `{ until, reason }` · `…/resume` | như trên |
| HV-09 | `GET enrollments/{id}/transfer-quote?targetClassId=` (gợi ý khấu trừ) · `POST enrollments/{id}/transfer` · `POST enrollments/transfer` (hàng loạt) | như trên |
| HV-10 | `POST enrollments/{id}/drop` `{ reason }` | như trên |
| HV-12 | `GET guardians?q=&noPrimary=` · `POST guardians` (gọi Crm) | `students.contacts.*` |
| HV-13 | `POST students/{id}/account` · `POST guardians/{leadId}/account` (gọi System — AUTH-10) | `.update` |
| HV-14 | `GET at-risk?level=&status=&assignee=` · `PUT care-cases/{id}` · `POST care-cases/{id}/logs` | `students.at-risk.*` |
| HV-15 | `GET certificates?classId=&eligible=` · `POST enrollments/{id}/certificate` `{ email, exception? }` · `POST certificates/{id}/resend` · công khai `GET public/certificates/{code}` | `students.certificates.*` |
| HV-16 | `GET/POST jlpt-sessions` · `GET/POST jlpt-registrations` · `POST jlpt-registrations/{id}/{pay|submit|admit|result|absent|cancel|cert-received|cert-handed}` | `students.jlpt.*` |
| HV-17 | `GET/POST/PUT abroad-cases` · `POST abroad-cases/{id}/stage|documents|notes|cancel` | `students.study-abroad.*` |

- JSON khớp `frontend/src/features/student/types.ts` (`Enrollment`, `Certificate`, `PortalAccount`); trạng thái học tổng hợp `StudyStatus` tính ở server.
- **Integration event (Students.Contracts)** 🆕: `StudentEnrolled`, `EnrollmentTransferred`, `EnrollmentDropped`, `EnrollmentReserved`, `EnrollmentResumed`,
  `EnrollmentCompleted` — Finance (hóa đơn), Academic (danh sách điểm danh), Notifications nghe.
- **Interface cho module khác** 🆕: `IStudentLookup` (tên, mã, dob, chi nhánh), `IEnrollmentQuery` (HV đang học của 1 lớp; số ghi danh hiệu lực — tính sĩ số).
- **Nghe event:** `ClassFinished` (Academic) → HV-11 · `ClassCancelled` → HV-10.E2 · `InvoicePaid` (Finance) → cập nhật cờ nợ cho HV-14.
- **Job:** nhắc hết hạn bảo lưu (hằng ngày, HV-07.E3) · tính cảnh báo bỏ học (hằng đêm, HV-14.E4).
- **Mã lỗi cần thêm vào Domain** (chưa có): `students.student.invalid_dob`, `students.enrollment.duplicate`, `students.enrollment.invalid_reserve_until`,
  `students.enrollment.same_class`.

## Phạm vi giai đoạn 14 (chốt 2026-11-20)

- **Làm:** HV-01 → HV-11 (danh sách, hồ sơ, sửa, nhập Excel, ẩn, ghi danh, bảo lưu, học lại, chuyển lớp, cho nghỉ, hoàn thành khi lớp kết thúc),
  sĩ số lớp cho Academic (`IEnrollmentLookup`), HV-13 dùng lại API tài khoản cổng đã có ở System.
- **Chờ module khác:** HV-12 phụ huynh (dữ liệu ở Crm — giai đoạn 16), HV-14 cảnh báo bỏ học (cần điểm danh — giai đoạn 15),
  hóa đơn của ghi danh (Finance — giai đoạn 17; Students phát event, chưa ai nghe).
- **Làm sau (nhóm riêng):** HV-15 chứng nhận, HV-16 JLPT, HV-17 du học / XKLĐ.
- **Domain (phase 1):** `Student.Create / Update` kiểm họ tên, ngày sinh không ở tương lai, SĐT 10 số bắt đầu bằng 0 (+84 → 0), email; `Hide` nêu lớp còn học.
  `Enrollment`: bảo lưu cần lý do + hạn sau hôm nay ≤ 6 tháng, cho nghỉ / chuyển lớp cần lý do, hoàn thành chỉ từ "đang học"; ghi chú giống frontend
  ("Bảo lưu đến dd/MM/yyyy — …", "Học lại từ …", "Chuyển sang lớp … — …").

## Đã làm — backend (giai đoạn 14 phase 3, 2026-11-20)

Code: `src/Modules/Students/JPCenter.Modules.Students/Application/{Students,Enrollments}/` · Test: `tests/JPCenter.IntegrationTests/Students/`.

| Story | API (`/api/v1/students/...`) | Quyền |
|---|---|---|
| HV-01, 02 | `GET ?includeHidden=&branchId=` (kèm `studyStatus`, `activeClassIds`) · `GET {id}` | `students.profiles.view` |
| HV-03 | `PUT {id}` `{ …, rowVersion }` | `.update` |
| HV-04 | `POST import` `{ rows, importDuplicates }` → `{ created, errors[row], duplicates[row, existingCode] }` (≤ 1000 dòng, giới hạn nhập file) | `.create` |
| HV-05 | `POST hide` `{ ids }` → `{ hidden, skipped[reason] }` · `POST {id}/unhide` | `.update` |
| Ghi danh | `GET enrollments?classId=&studentId=` | `students.profiles.view` **hoặc** `academic.classes.view` |
| HV-06 | `POST enrollments` `{ studentId, classId, amount?, discount?, dueDate? }` | `academic.classes.update` **hoặc** `students.profiles.update` |
| HV-07, 08, 10 | `POST enrollments/{id}/reserve` `{ until, reason }` · `…/resume` · `…/drop` `{ reason }` | như trên |
| HV-09 | `POST enrollments/transfer` `{ targetClassId, items[{ enrollmentId, amount, credit }], dueDate, reason }` (1 hoặc nhiều HV) | như trên |
| HV-11 | Tự động: Academic `ClassFinished` → ghi danh đang học thành hoàn thành; `ClassCancelled` → cho nghỉ "Lớp bị hủy: …" | — |

**Chốt khi code:**
- Quyền "1 trong 2" dùng `.RequireAnyPermission(...)` (BuildingBlocks); phạm vi: HV theo chi nhánh của HV, ghi danh còn theo chi nhánh của lớp.
- Phát `StudentEnrolled` (học phí, giảm trừ, hạn đóng, ghi danh gốc khi chuyển lớp) và `EnrollmentEnded` cho Finance — ⏳ chưa ai nghe (giai đoạn 17).
- Mã HV mới = số lớn nhất + 1 (HV1036…); nhập Excel bỏ qua dòng lỗi, dòng trùng SĐT + họ tên trả về để người dùng chọn.
- ⏳ Phạm vi `own` (HV có lead do mình phụ trách) tạm tính như `branch` cho tới khi có Crm.

### Frontend (giai đoạn 14 phase 4)
- `features/student/studentApi.ts`: tải lúc đăng nhập (hồ sơ HV kể cả đã ẩn → `useCrmStore.students`, ghi danh → `useStudentStore.enrollments`);
  chi tiết lớp tải ghi danh của lớp.
- Đã nối: sửa hồ sơ, nhập Excel (server tạo hồ sơ; lead phụ huynh + liên kết vẫn ghi ở Crm giả), ẩn / bỏ ẩn, thêm HV vào lớp, bảo lưu, học lại,
  cho nghỉ, chuyển lớp (đơn + hàng loạt = 1 request). Hủy / kết thúc lớp: server cập nhật ghi danh (chạy nền) → trang tải lại sau ~2,5 giây.
- ⏳ Hóa đơn còn giả: ghi danh / chuyển lớp tạo hóa đơn giả ở trình duyệt, cho nghỉ / chuyển / hủy lớp hủy công nợ hóa đơn giả (mất khi tải lại trang).
- ⏳ Còn chạy dữ liệu giả: tạo HV từ lead (CRM-07), ghi danh từ lead, lên lớp (giai đoạn 15 / 16), chứng nhận, JLPT, du học.

## Đã làm — HV-14 (giai đoạn 15 phase 3, 2026-11-20)

| Story | API (`/api/v1/students/...`) | Quyền |
|---|---|---|
| HV-14 | `GET at-risk?branchId=&classId=&studentId=&level=&status=&includeResolved=` · `PUT at-risk/{id}` `{ assigneeId, status, nextAt }` · `POST at-risk/{id}/logs` `{ channel, note }` · `POST at-risk/recalculate` · `GET care-logs?classId=&studentId=` | `students.at-risk.*` (nhật ký: hoặc `academic.classes.view`) |

- Tính nguy cơ: job hằng đêm 02:00 + ngay khi Academic phát `AttendanceRecorded`; chuyên cần qua `Academic.Contracts.IAttendanceStats`.
- ⏳ Bài tập / điểm thi / nợ quá hạn tạm tính 0 (chưa có API); ngưỡng dùng mặc định tới khi Cài đặt chung lưu `atRisk`.
