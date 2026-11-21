# 05 · Tuyển sinh (lead, chia lead, test xếp lớp)

Module: **Crm** · Bảng: `crm.leads`, `consultations`, `lead_student_links`, `lead_rules`, `lead_assignment_logs`, `placement_tests`,
`placement_attempts`, `appointments`, `campaigns`, `message_templates`, `message_logs` ([database.md](../database.md))
Frontend: `pages/staff/crm/**`, `features/crm/crmStore.ts`, `features/crm/admission/{routing,slaWatch,placement}.ts`,
`features/crm/appointments/appointments.ts`, `features/crm/components/{LeadFormFields,EnrollLeadModal,LinkedStudentsTab}.tsx`

## Mô hình (đọc trước)

```
Lead (người LIÊN HỆ — chính người học hoặc phụ huynh)
 ├─ status:  new ──(ghi tư vấn đầu tiên)──▶ consulting ──▶ test_scheduled ──▶ won
 │            └──────────────────────────────────────────────────────────▶ lost (bắt buộc lý do)
 ├─ assignee_id ← chia tự động theo lead_rules (hoặc chọn tay)  ── lead_assignment_logs (vì sao chọn Sale)
 ├─ Consultation[]      lịch sử tư vấn (gồm dòng hệ thống)
 ├─ PlacementAttempt[]  lượt làm test online qua link có hạn
 ├─ Appointment[]       hẹn đến trung tâm tư vấn / làm test
 └─ LeadStudentLink[] ──▶ Student (Students)   nhiều–nhiều: 1 phụ huynh nhiều con, bố + mẹ cùng 1 con
        cờ: is_primary (người quyết định, mỗi HV ≤ 1) · billing (nhận hóa đơn) · notify (nhận thông báo)
```

**Quy tắc vàng:**
1. Lead là **người liên hệ**, không phải người học. Học viên tạo ra từ lead (CRM-07) — chính người đó hoặc người khác (con, anh chị em…).
2. Trùng SĐT / email chỉ **cảnh báo**, vẫn cho lưu (bố và mẹ có thể dùng chung số; khách quay lại sau 1 năm).
3. Không xóa lead — chỉ **ẩn** (cờ `hidden`). Ẩn lead không ảnh hưởng học viên liên kết, lịch sử tư vấn giữ nguyên.
4. Lịch sử tư vấn **chỉ thêm**, không sửa / xóa. Mọi thao tác tự động (chia lead, ghi danh, kết quả test) sinh dòng `is_system = true`.
5. Phạm vi `own` của `crm.*` = lead có `assignee_id` = mình (PQ-10).

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| CRM-01 | Xem danh sách / Kanban lead | Sale, quản lý | Cao |
| CRM-02 | Thêm lead (tay / form web / Facebook) | Sale, lễ tân, hệ thống | Cao |
| CRM-03 | Nhập lead từ Excel | Sale, marketing | TB |
| CRM-04 | Sửa lead, chuyển giai đoạn, đánh dấu mất | Sale | Cao |
| CRM-05 | Ghi lịch sử tư vấn & hẹn gọi lại | Sale | Cao |
| CRM-06 | Ẩn / bỏ ẩn lead | Sale, quản lý | TB |
| CRM-07 | Chốt lead → tạo học viên + ghi danh + hóa đơn | Sale | Cao |
| CRM-08 | Liên kết lead ↔ học viên (phụ huynh) | Sale, giáo vụ | Cao |
| CRM-09 | Cấu hình quy tắc chia lead & SLA | Quản lý Sale | Cao |
| CRM-10 | Tự chia lead mới | Hệ thống | Cao |
| CRM-11 | Theo dõi SLA, cảnh báo & tự chuyển Sale | Hệ thống, quản lý | Cao |
| CRM-12 | Soạn bài test xếp lớp | Giáo vụ | TB |
| CRM-13 | Gửi link test & lead làm bài online | Sale, lead | TB |
| CRM-14 | Đặt lịch hẹn tư vấn / test tại trung tâm | Sale, lễ tân | TB |
| CRM-15 | Ghi kết quả lịch hẹn (đến / không đến / hủy) | Sale, lễ tân | TB |
| CRM-16 | Chiến dịch marketing | Marketing | TB |
| CRM-17 | Mẫu tin nhắn & gửi tin | Sale, giáo vụ, kế toán | TB |
| CRM-18 | Phễu chuyển đổi | Quản lý | Thấp |
| CRM-19 | Tư vấn lên lớp (phía Sale) | Sale | TB |

**Quyền** (theo trang): `crm.leads` · `crm.lead-rules` · `crm.placement-tests` · `crm.appointments` · `crm.promotions` · `crm.funnel` ·
`crm.message-templates` · `crm.campaigns` — hành động `view / create / update / delete / export`. Phạm vi `own / branch / all`.

---

## CRM-01 · Xem danh sách / Kanban lead

> Là **Sale**, tôi muốn **xem lead của mình theo giai đoạn** để **biết hôm nay cần gọi ai trước**.

**Nguồn:** Tuyển sinh › Lead (`LeadsPage.tsx`) · **Quyền cần:** `crm.leads.view` · **Ưu tiên:** Cao

**Các bước:**
1. Hai chế độ: **bảng** (phân trang) và **Kanban** 5 cột Mới · Đang tư vấn · Hẹn test · Đã chốt · Mất, có số đếm từng cột.
2. Tìm theo tên / SĐT / mã / email (không dấu). Lọc: giai đoạn, nguồn, chi nhánh, Sale, chiến dịch, "quá SLA", "quá hẹn gọi lại", "hiện lead đã ẩn".
3. Mỗi dòng: mã, tên, SĐT, nguồn, khóa quan tâm, Sale, lần liên hệ cuối, hẹn gọi lại, nhãn **Quá SLA** / **Quá hẹn**.
4. Bấm lead → ngăn chi tiết: Thông tin · Lịch sử tư vấn · Học viên liên kết · Test · Lịch hẹn · Tin đã gửi.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-01.E1 | Phạm vi `own` | Chỉ trả lead `assignee_id` = mình; Kanban đếm theo phạm vi |
| CRM-01.E2 | Lead quá SLA | Lead `new`, chưa có tư vấn nào, `now − created_at > SLA` (SLA theo quy tắc khớp, không có → Cài đặt `crm.defaultSlaMinutes`, mặc định 15 phút) |
| CRM-01.E3 | Quá hẹn gọi lại | `next_follow_up_at < now` và lead chưa `won` / `lost` |
| CRM-01.E4 | Lead đã ẩn | Không hiện mặc định; bật bộ lọc mới thấy (nhãn "Đã ẩn") |
| CRM-01.E5 | Xuất Excel | Cần `crm.leads.export`; ghi nhật ký `export`; xuất đúng bộ lọc đang chọn |

---

## CRM-02 · Thêm lead

> Là **Sale / lễ tân**, tôi muốn **ghi nhanh khách vừa gọi hoặc đến** để **không bỏ sót khách và lead tự về đúng Sale**.

**Nguồn:** Lead › "Thêm lead" (`LeadFormFields.tsx`) · **Quyền cần:** `crm.leads.create`

**Các bước:**
1. Nhập: tên*, SĐT*, email, nguồn*, trạng thái* (mặc định Mới), chi nhánh* (chỉ chi nhánh `active` — CN-08), khóa quan tâm, chiến dịch
   (chỉ chiến dịch đúng kênh của nguồn), Sale phụ trách (bỏ trống = tự chia), hẹn gọi lại (không trước hôm nay), ghi chú.
2. Gõ SĐT → kiểm tra trùng ngay, hiện **cảnh báo** "Trùng với lead LD0012 — Nguyễn Văn A" (vẫn cho lưu).
3. Lưu → sinh mã `LD0001` → không chọn Sale thì chạy **CRM-10** → Sale được giao nhận thông báo.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-02.E1 | Thiếu tên / SĐT | `crm.lead.name_required` / `crm.lead.phone_required` |
| CRM-02.E2 | SĐT sai định dạng | Chuẩn hóa `+84…` → `0…`; khác 10 số bắt đầu bằng 0 → lỗi theo trường |
| CRM-02.E3 | Trùng SĐT với lead khác | **Không chặn** — API trả `warnings: [{ code: "crm.lead.duplicate_phone", leadId }]`, UI hiện link tới lead cũ |
| CRM-02.E4 | Trạng thái `lost` lúc tạo | Bắt buộc lý do mất — `crm.lead.lost_reason_required` |
| CRM-02.E5 | Lead từ form web / Facebook 🆕 | Webhook tích hợp tạo lead, `created_by` = hệ thống; có `utm` khớp chiến dịch → gắn `campaign_id` (CRM-16) |
| CRM-02.E6 | Chi nhánh đã đóng | `system.branch.inactive` 🆕 — chỉ tạo lead ở chi nhánh `active` |
| CRM-02.E7 | Chọn Sale ngoài phạm vi / đã nghỉ việc | Từ chối `crm.lead.invalid_assignee` 🆕 (Sale phải còn làm, thuộc chi nhánh của lead) |

---

## CRM-03 · Nhập lead từ Excel

> Là **marketing**, tôi muốn **nhập danh sách khách từ sự kiện / file quảng cáo** để **không phải gõ tay từng người**.

**Nguồn:** Lead › "Nhập Excel" (`LeadImportDialog.tsx`) · **Quyền cần:** `crm.leads.create`

**Các bước:**
1. Tải file mẫu (Họ tên*, SĐT*, Email, Nguồn, Khóa quan tâm, Chi nhánh, Sale phụ trách, Ghi chú) — tên cột nhận cả tên gần đúng (SĐT, Phone…).
2. Chọn file → xem trước: dòng hợp lệ / lỗi / trùng; sửa hoặc bỏ dòng lỗi.
3. Xác nhận → **1 request** cho cả lô (quy ước bulk = 1 request) → lead mới trạng thái Mới, dòng không có Sale chạy CRM-10 lần lượt.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-03.E1 | Dòng thiếu tên / SĐT, SĐT sai | Đánh dấu lỗi theo dòng, không nhập dòng đó |
| CRM-03.E2 | Nguồn / chi nhánh / Sale không nhận ra | Nguồn → `event`; chi nhánh trống → chi nhánh đang làm việc; Sale không khớp → tự chia |
| CRM-03.E3 | Trùng SĐT với lead có sẵn hoặc trong cùng file | Cảnh báo theo dòng, người dùng chọn nhập / bỏ |
| CRM-03.E4 | File > 1.000 dòng 🆕 | Từ chối `crm.import.too_many_rows` — chia nhỏ file |
| CRM-03.E5 | Lỗi giữa chừng | Cả lô trong 1 transaction — lỗi thì không nhập dòng nào |

---

## CRM-04 · Sửa lead, chuyển giai đoạn, đánh dấu mất

> Là **Sale**, tôi muốn **cập nhật giai đoạn của lead** để **quản lý thấy đúng tiến độ tư vấn**.

**Nguồn:** Ngăn chi tiết lead, kéo thả Kanban · **Quyền cần:** `crm.leads.update`

**Các bước:** Sửa thông tin như CRM-02 hoặc kéo thẻ sang cột khác → lưu → nhật ký dữ liệu (trước → sau).
Đổi Sale phụ trách → ghi `lead_assignment_logs` "Chuyển tay" + báo Sale mới.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-04.E1 | Chuyển sang `lost` | Hộp nhập **lý do mất** bắt buộc — `crm.lead.lost_reason_required` |
| CRM-04.E2 | Chuyển sang `won` bằng kéo thả | Không cho — `won` chỉ qua CRM-07 (có ghi danh). 🆕 `crm.lead.won_requires_enrollment` |
| CRM-04.E3 | Mở lại lead `lost` | Cho về `consulting`, xóa `lost_reason`, thêm dòng hệ thống "Mở lại lead" |
| CRM-04.E4 | Sửa lead đang ẩn | Cho sửa; vẫn ẩn |
| CRM-04.E5 | Hai người sửa cùng lúc | `xmin` → "Dữ liệu vừa được người khác sửa" |
| CRM-04.E6 🆕 | Đổi giai đoạn lead đã `won` | Không cho — `crm.lead.won_locked` (đã có ghi danh; HV bỏ học xử lý ở HV-14) |

---

## CRM-05 · Ghi lịch sử tư vấn & hẹn gọi lại

> Là **Sale**, tôi muốn **ghi lại mỗi lần gọi / nhắn / gặp** để **người sau đọc là biết khách đang ở đâu**.

**Nguồn:** Lead › tab Lịch sử tư vấn (`ConsultationTab.tsx`) · **Quyền cần:** `crm.leads.update`

**Các bước:**
1. Chọn kênh (gọi, Zalo, gặp, nhắn tin, email), kết quả (quan tâm, suy nghĩ, hẹn test, không nghe máy, không quan tâm, đã đăng ký), nội dung*, hẹn gọi lại.
2. Lưu → `last_contact_at = now`; có hẹn gọi lại → cập nhật `next_follow_up_at`; lead đang `new` → tự sang **`consulting`**.
3. Danh sách xếp mới nhất trước; dòng hệ thống có nhãn "Hệ thống".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-05.E1 | Nội dung trống | `crm.lead.consult_content_required` |
| CRM-05.E2 | Kết quả "Hẹn test" | Gợi ý mở CRM-13 (gửi link) hoặc CRM-14 (hẹn đến trung tâm); lead → `test_scheduled` |
| CRM-05.E3 | Kết quả "Không quan tâm" | Gợi ý đánh dấu mất (CRM-04.E1), không tự chuyển |
| CRM-05.E4 | Sửa / xóa dòng đã ghi | Không cho — chỉ thêm dòng mới |
| CRM-05.E5 | Lần tư vấn đầu tiên | Dừng đồng hồ SLA (CRM-11) |
| CRM-05.E6 | Cổng GV / HV / PH | **Không** trả lịch sử tư vấn (quy tắc nội bộ — CLAUDE.md mục 6) |

---

## CRM-06 · Ẩn / bỏ ẩn lead

> Là **Sale / quản lý**, tôi muốn **ẩn lead rác / trùng** để **danh sách gọn mà không mất dữ liệu**.

**Nguồn:** `useHideLead.tsx` · **Quyền cần:** `crm.leads.update` (🆕 có thể tách `crm.leads.delete` nếu muốn chặt hơn)

**Các bước:** "Ẩn lead" → hộp xác nhận nêu: N học viên liên kết vẫn học bình thường, lịch sử tư vấn giữ nguyên → `hidden = true`, `hidden_at`.
"Bỏ ẩn" → không cần xác nhận.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-06.E1 | Lead đã ẩn mà ẩn tiếp | `crm.lead.already_hidden` |
| CRM-06.E2 | Lead là phụ huynh có tài khoản cổng | Vẫn ẩn được; tài khoản Cổng phụ huynh **không** tự khóa (khóa riêng — AUTH-10) |
| CRM-06.E3 | Lead ẩn trong chia lead / SLA | Bỏ qua khi đếm tải (`least_load`) và khi quét SLA |

---

## CRM-07 · Chốt lead → tạo học viên + ghi danh + hóa đơn

> Là **Sale**, tôi muốn **chốt khách ngay trong ngăn lead (người học → lớp → học phí)** để **không phải nhập lại ở 3 trang khác nhau**.

**Nguồn:** Lead › "Ghi danh" (`EnrollLeadModal.tsx`, 3 bước) · **Quyền cần:** `crm.leads.update` + `students.profiles.create`
(+ `finance.invoices.create` nếu thu tiền ngay) · **Ưu tiên:** Cao

**Các bước:**
1. **Người học** — chọn 1 trong 3: *chính người liên hệ* (quan hệ `self`) · *học viên đã liên kết* · *người khác* (họ tên*, SĐT, ngày sinh, quan hệ: con / anh chị em / vợ chồng / khác).
2. **Chọn lớp** — lớp của chi nhánh lead, trạng thái `enrolling` / `ongoing`; xếp ưu tiên khớp khóa quan tâm / gợi ý test, rồi lớp sắp khai giảng, rồi còn nhiều chỗ.
   Mỗi lớp hiện chỗ còn (đỏ khi hết, cam khi ≤ 3). **Bỏ qua bước này** được (chỉ tạo hồ sơ HV).
3. **Học phí** — học phí lớp, giảm giá (hoặc mã ưu đãi — TC-03), hạn đóng*, tùy chọn **thu ngay** (số tiền, hình thức).
4. Hệ thống trong **1 transaction**: tạo HV (Students) + liên kết lead ↔ HV → ghi danh `studying` → hóa đơn (Finance) → thu tiền nếu có →
   dòng tư vấn hệ thống "Ghi danh X vào lớp Y — học phí…, đã thu…" → lead **`won`**.
5. Màn kết quả: nút "Xem hồ sơ học viên", "Xem hóa đơn".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-07.E1 | Chọn "chính người liên hệ" khi đã có liên kết `self` | Lựa chọn bị khóa (1 lead chỉ có 1 hồ sơ `self`) |
| CRM-07.E2 | Người khác: thiếu tên / SĐT sai | "Nhập họ tên học viên" / "SĐT gồm 10 số, bắt đầu bằng 0" (`students.student.name_required`) |
| CRM-07.E3 | HV đã có ghi danh đang học / bảo lưu ở lớp đó | "Học viên đã học lớp này" — `students.enrollment.duplicate` 🆕 |
| CRM-07.E4 | Lớp hết chỗ (đang học + bảo lưu ≥ sĩ số tối đa) | `academic.class.full` |
| CRM-07.E5 | Lớp `paused` / `finished` / `cancelled` | `academic.class.closed` |
| CRM-07.E6 | Giảm giá < 0 hoặc > học phí | `finance.invoice.discount_too_large` |
| CRM-07.E7 | Thu ngay ≤ 0 hoặc > số phải thu | `finance.invoice.overpay` / `finance.invoice.invalid_amount` |
| CRM-07.E8 | Không chọn lớp | Chỉ tạo HV + liên kết, lead **vẫn** `won`; không sinh hóa đơn |
| CRM-07.E9 | HV < 18 tuổi 🆕 | Liên kết của lead tự bật `is_primary` + `billing` + `notify` (phụ huynh là người quyết định) |
| CRM-07.E10 | Bấm 2 lần / mạng chập chờn | Header `Idempotency-Key` (bảng `system.idempotency_keys`) — không tạo 2 HV / 2 hóa đơn |

**Luồng giữa module:** Crm gọi `Students.Contracts › IStudentEnrollmentService.EnrollFromLead(...)` 🆕 (Students tạo HV + ghi danh,
gọi Finance tạo hóa đơn qua `Finance.Contracts`). Không ghi trực tiếp bảng module khác.

---

## CRM-08 · Liên kết lead ↔ học viên (phụ huynh)

> Là **Sale / giáo vụ**, tôi muốn **gắn mẹ và bố của 1 bé vào hồ sơ bé** để **gửi hóa đơn / thông báo đúng người và biết ai là người quyết định**.

**Nguồn:** Lead › tab Học viên (`LinkedStudentsTab.tsx`), Học viên › tab Phụ huynh · **Quyền cần:** `crm.leads.update` hoặc `students.contacts.update`

**Các bước:**
1. "Liên kết học viên" → chọn HV đã có* + quan hệ* → lưu → dòng hệ thống ở lịch sử tư vấn.
2. Bật / tắt cờ trên từng liên kết: **Liên hệ chính**, **Nhận hóa đơn**, **Nhận thông báo**.
3. Hiện "người liên hệ khác của cùng HV" (vd bố cũng là lead).
4. "Gỡ liên kết" → xác nhận "Hồ sơ HV, lớp học, học phí giữ nguyên".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-08.E1 | Liên kết đã tồn tại | `crm.lead.already_linked` |
| CRM-08.E6 🆕 | Liên kết chỉ gắn HV, **không** đổi giai đoạn | Lead chỉ `won` qua CRM-07 (ghi danh); gỡ liên kết HV chưa có → `crm.lead.not_linked` |
| CRM-08.E2 | Bật "Liên hệ chính" khi HV đã có người chính khác | Tự **tắt** cờ ở liên kết cũ (mỗi HV tối đa 1) — báo "Đã đổi người liên hệ chính" |
| CRM-08.E3 | Gỡ liên kết người liên hệ chính duy nhất của HV nhỏ tuổi 🆕 | Cảnh báo "HV chưa có người quyết định" — vẫn cho |
| CRM-08.E4 | Không còn ai nhận hóa đơn | Cảnh báo; hóa đơn gửi tới SĐT / email của chính HV |
| CRM-08.E5 | Gỡ liên kết khi PH có tài khoản cổng | PH mất quyền xem HV đó ngay (PQ-11) |

---

## CRM-09 · Cấu hình quy tắc chia lead & SLA

> Là **quản lý Sale**, tôi muốn **khai báo lead nguồn nào, chi nhánh nào giao cho nhóm Sale nào, cách chia và thời hạn gọi** để **lead được xử lý nhanh, công bằng**.

**Nguồn:** Tuyển sinh › Chia lead & SLA (`LeadRulesPage.tsx`) · **Quyền cần:** `crm.lead-rules.view / create / update / delete`

**Các bước:**
1. Danh sách quy tắc theo **thứ tự ưu tiên** (nút lên / xuống), bật / tắt từng quy tắc.
2. Thêm / sửa: tên*, điều kiện nguồn / chi nhánh / khóa quan tâm (rỗng = mọi giá trị), cách chia (`round_robin` xoay vòng · `least_load` ít lead mở nhất ·
   `fixed` cố định người đầu), nhóm Sale* (thứ tự chọn = thứ tự xoay vòng), "chỉ chia cho Sale đang trong ca", SLA (phút)*, quá hạn báo cho ai, "quá 2×SLA tự chuyển".
3. Tab **Nhật ký chia lead**: thời điểm, lead, giao cho, lý do (vd `Quy tắc "Facebook CG" → Trần Minh Khoa (2/3 Sale đang trong ca)`).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-09.E1 | Không chọn Sale nào | `crm.lead_rule.no_sale` |
| CRM-09.E2 | SLA ≤ 0 hoặc > 7 ngày | `crm.lead_rule.invalid_sla` |
| CRM-09.E3 | Sale trong nhóm đã nghỉ việc / chuyển phòng | Không lỗi — bị **bỏ qua** khi chia; trang hiện cảnh báo trên quy tắc |
| CRM-09.E4 | Xóa quy tắc | Xóa mềm; nhật ký chia lead cũ vẫn giữ tên quy tắc |
| CRM-09.E5 | Đổi thứ tự | 1 request gửi cả danh sách thứ tự mới |

---

## CRM-10 · Tự chia lead mới

> Là **hệ thống**, tôi **giao lead mới chưa có Sale cho đúng người** để **khách được gọi trong vài phút**.

**Nguồn:** `features/crm/admission/routing.ts › routeLead` (backend chạy đúng logic này) · **Kích hoạt:** CRM-02, CRM-03, webhook

**Các bước:**
1. Dò quy tắc **đang bật** từ trên xuống, quy tắc đầu tiên khớp (nguồn ∧ chi nhánh ∧ khóa quan tâm) được dùng.
2. Lọc nhóm Sale còn làm việc. Bật "chỉ trong ca" → giữ người đang trong ca (làm theo ca → theo Xếp ca; không → giờ hành chính Cài đặt chung, CN nghỉ).
   Không ai trong ca → chia cho cả nhóm.
3. Chọn người: `round_robin` đi tiếp từ sau con trỏ, bỏ người ngoài pool, cập nhật `cursor` · `least_load` ít lead mở (`new`/`consulting`/`test_scheduled`, không ẩn) nhất ·
   `fixed` người đầu tiên.
4. Ghi `lead_assignment_logs` (sale, rule, reason) + thông báo Sale được giao.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-10.E1 | Không quy tắc nào khớp | Lead không có Sale, log "Không quy tắc nào khớp — chờ quản lý giao tay"; báo người có `crm.lead-rules.update` |
| CRM-10.E2 | Quy tắc không còn Sale đang làm việc | Log "Quy tắc X không còn Sale nào đang làm việc", lead chưa giao |
| CRM-10.E3 | 2 lead về cùng lúc (round_robin) | Cập nhật `cursor` có khóa lạc quan (`xmin`) — thử lại, không giao 2 lead cho cùng 1 người ngoài ý muốn |
| CRM-10.E4 | Lead tạo có chọn Sale | Không chạy chia; vẫn log "Chọn tay" |

---

## CRM-11 · Theo dõi SLA, cảnh báo & tự chuyển Sale

> Là **quản lý**, tôi muốn **được báo khi lead mới quá hạn chưa ai gọi, và lead tự chuyển người nếu quá lâu** để **không mất khách nóng**.

**Nguồn:** `slaWatch.ts › runSlaCheck` · **Chạy:** job Quartz **mỗi phút** (frontend đang giả lập khi mở trang)

**Các bước:**
1. Với lead không ẩn, quá SLA (CRM-01.E2): chưa cảnh báo → đặt `sla_escalated_at`, thông báo người trong "quá hạn báo cho" (không có → Quản lý chi nhánh).
2. Quy tắc bật tự chuyển + trễ thêm ≥ 1 SLA (tức quá **2×SLA**) + chưa tự chuyển lần nào → chọn Sale khác cùng nhóm (ưu tiên trong ca, ít lead mở nhất),
   đặt `sla_reassigned_at`, log "Tự chuyển do quá 2×SLA (30 phút) — trước: X".
3. Gom thông báo: > 3 lead cùng lượt → 1 thông báo tổng hợp.
4. Quản lý chuyển tay lead quá SLA ở trang Chia lead → log "Chuyển tay do quá SLA".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-11.E1 | Mỗi lead chỉ cảnh báo 1 lần, tự chuyển 1 lần | Kiểm `sla_escalated_at` / `sla_reassigned_at`; sau đó quản lý xử lý tay |
| CRM-11.E2 | Không có Sale thay thế | Bỏ qua bước chuyển, vẫn cảnh báo |
| CRM-11.E3 | Job chạy trễ / chạy 2 lần | An toàn khi chạy lại (idempotent nhờ 2 cột mốc) |
| CRM-11.E4 | Sale ghi tư vấn đầu tiên | Lead thôi quá SLA ngay lượt quét sau |

---

## CRM-12 · Soạn bài test xếp lớp

> Là **giáo vụ**, tôi muốn **tạo đề test bốc từ ngân hàng câu hỏi kèm thang gợi ý trình độ** để **Sale gửi cho khách mà không phải tự soạn đề**.

**Nguồn:** Tuyển sinh › Test xếp lớp › tab Bài test (`PlacementTestsPage.tsx`, `placement.ts`) · **Quyền cần:** `crm.placement-tests.create / update`

**Các bước:**
1. Nhập tên*, mô tả, thời gian làm (phút), các **phần**: cấp độ × kỹ năng × số câu.
2. Hệ thống bốc câu **đã duyệt** đúng cấp độ + kỹ năng (đọc qua `Academic.Contracts › IQuestionBank` 🆕); "Đổi đề" = bốc ngẫu nhiên lại. Danh sách câu được **cố định** khi lưu.
3. Thang gợi ý: các mốc `% điểm tối thiểu → nhận xét trình độ → khóa gợi ý`, bắt buộc có mốc 0%.
4. Trạng thái: nháp → đang dùng → lưu trữ.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-12.E1 | Không có phần nào | `crm.placement_test.no_section` |
| CRM-12.E2 | Ngân hàng không đủ câu cho 1 phần | `crm.placement_test.no_question` (hiện "N5 · Từ vựng: cần 10, chỉ có 6 câu đã duyệt") |
| CRM-12.E3 | Thời gian ≤ 0 hoặc > 180 phút | `crm.placement_test.invalid_duration` |
| CRM-12.E4 | Thang không có mốc 0% | "Thang gợi ý cần có mốc 0%" 🆕 `crm.placement_test.band_zero_required` |
| CRM-12.E5 | Câu trong đề bị chuyển "ngừng dùng" sau đó | Đề đã lưu vẫn dùng câu đó (cố định); chỉ khi "Đổi đề" mới bỏ |
| CRM-12.E6 | Sửa đề đã có người làm 🆕 | Chỉ cho sửa tên / mô tả / thang; đổi câu → tạo bài test mới |

---

## CRM-13 · Gửi link test & lead làm bài online

> Là **Sale**, tôi muốn **gửi link test qua Zalo / SMS / email và tự nhận kết quả gợi ý lớp** để **tư vấn đúng trình độ mà không cần khách đến trung tâm**.

**Nguồn:** Test xếp lớp › tab Lượt làm, `AttemptDrawer.tsx` · **Quyền cần:** `crm.placement-tests.update` (gửi) · lead làm bài **không cần đăng nhập**

**Các bước:**
1. Chọn bài test (đang dùng)*, lead*, kênh, số ngày hiệu lực → tạo lượt `sent` với `token` bí mật → gửi link `https://…/t/{token}` theo mẫu tin `test_reminder`.
2. Lead mở link → bắt đầu (`in_progress`, `started_at`) → đồng hồ đếm ngược theo thời gian bài → nộp (hoặc hết giờ tự nộp).
3. Hệ thống chấm: đúng / tổng, theo kỹ năng, % → mốc thang cao nhất ≤ điểm → trình độ + khóa gợi ý → `done`.
4. Sale mở lượt làm → "Ghi vào lịch sử tư vấn" → dòng hệ thống + cập nhật khóa quan tâm của lead (`logged = true`).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-13.E1 | Mở link sau hạn | `crm.placement_attempt.expired` — trang "Link đã hết hạn, liên hệ trung tâm" |
| CRM-13.E2 | Mở lại link đang làm | Tiếp tục bài, thời gian còn lại tính từ `started_at` |
| CRM-13.E3 | Bắt đầu lại lượt đã bắt đầu | `crm.placement_attempt.started` |
| CRM-13.E4 | Nộp khi không ở `in_progress` / đã nộp | `crm.placement_attempt.not_in_progress` |
| CRM-13.E5 | Đáp án đúng | **Không** trả về trang làm bài (chỉ đề + lựa chọn); kết quả chỉ hiện điểm tổng cho lead |
| CRM-13.E6 | Token đoán / dò 🆕 | Token ≥ 128 bit ngẫu nhiên; giới hạn tần suất theo IP |
| CRM-13.E8 🆕 | Đang làm thì mạng rớt | Đáp án lưu dần (`answers`) — hết hạn thì job chấm phần đã lưu |
| CRM-13.E7 | Job hằng giờ | Lượt `sent` / `in_progress` quá hạn → `expired` (`in_progress` thì chấm phần đã làm) |

---

## CRM-14 · Đặt lịch hẹn tư vấn / test tại trung tâm

> Là **Sale / lễ tân**, tôi muốn **hẹn khách đến trung tâm mà không trùng phòng, trùng người tiếp** để **khách đến là có chỗ, có người**.

**Nguồn:** Tuyển sinh › Lịch hẹn (`AppointmentsPage.tsx`, `BookAppointmentModal.tsx`, `appointments.ts › findConflicts`) · **Quyền cần:** `crm.appointments.create / update`

**Các bước:**
1. Chọn lead*, loại (tư vấn / làm test — thời lượng mặc định theo loại), ngày*, giờ*, chi nhánh, phòng (trống = quầy), người tiếp / coi test*, ghi chú.
2. Kiểm tra trùng ngay khi chọn (bảng dưới). Có trùng → không cho lưu "Lịch bị trùng — đổi giờ, phòng hoặc người tiếp".
3. Lưu → mã `LH0001` → lead `test_scheduled` nếu là hẹn test.
4. **Nhắc trước 60 phút** (job): thông báo người tiếp + gửi tin lead (mẫu `consult_reminder` / `test_reminder`), đặt `reminded_at`.
5. Xem: Sắp tới · Đã qua · Lịch tuần. "Đổi lịch" = sửa giờ (kiểm trùng lại, reset `reminded_at`).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-14.E1 | Giờ kết thúc ≤ giờ bắt đầu, hoặc hẹn trong quá khứ | `crm.appointment.invalid_time` |
| CRM-14.E2 | Phòng bảo trì / ngừng dùng | Trùng loại `room`: "P.201 đang bảo trì" |
| CRM-14.E3 | Phòng có hẹn khác hoặc **buổi học** cùng giờ | "P.201 đang có lớp N3K12 học 18:00–20:00" (đọc buổi học qua `Academic.Contracts`) |
| CRM-14.E4 | Người tiếp có hẹn khác / đang dạy (GV) / nghỉ dài ngày | Trùng loại `staff` |
| CRM-14.E5 | Lead đã có hẹn khác giao giờ | Trùng loại `lead` |
| CRM-14.E6 | Mã lỗi chung khi trùng | 🆕 `crm.appointment.conflict` kèm `conflicts: [{ kind, message }]` |

---

## CRM-15 · Ghi kết quả lịch hẹn

> Là **Sale / lễ tân**, tôi muốn **đánh dấu khách đã đến / không đến / hủy** để **theo dõi tỉ lệ đến hẹn và gọi lại khách vắng**.

**Quyền cần:** `crm.appointments.update`

| Thao tác | Điều kiện | Kết quả |
|---|---|---|
| **Đã đến** | Lịch `scheduled` | Bắt buộc nhập *kết quả buổi hẹn* → `done`, `outcome`; thêm dòng tư vấn kênh "gặp" |
| **Không đến** | `scheduled` và đã qua giờ bắt đầu | `no_show`; lead đặt hẹn gọi lại sau 1 ngày |
| **Hủy lịch** | `scheduled` | Bắt buộc lý do → `cancelled`, `cancel_reason` |

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-15.E1 | Thao tác trên lịch không còn `scheduled` | `crm.appointment.not_scheduled` |
| CRM-15.E2 | "Không đến" trước giờ hẹn | Nút khóa; API từ chối `crm.appointment.not_started` 🆕 |

---

## CRM-16 · Chiến dịch marketing

> Là **marketing**, tôi muốn **theo dõi ngân sách, số lead, tỷ lệ chốt, chi phí / lead của từng chiến dịch** để **biết kênh nào đáng tiền**.

**Nguồn:** Tuyển sinh › Chiến dịch (`CampaignsPage.tsx`) · **Quyền cần:** `crm.campaigns.*`

**Các bước:**
1. Tạo / sửa: tên*, mã*, kênh (= nguồn lead), chi nhánh*, bắt đầu* – kết thúc*, ngân sách*, đã chi, chỉ tiêu lead, người phụ trách*, mã UTM, khóa quảng bá, ghi chú.
2. Trạng thái: dự kiến → đang chạy ⇄ tạm dừng → kết thúc.
3. Bảng số liệu (tính khi đọc): số lead, % chỉ tiêu, số chốt, tỷ lệ chốt, chi phí / lead = đã chi ÷ số lead, chi phí / chốt.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-16.E1 | Ngày kết thúc trước ngày bắt đầu | `crm.campaign.invalid_dates` |
| CRM-16.E2 | Trùng mã / trùng UTM 🆕 | `crm.campaign.duplicate_code` / `crm.campaign.duplicate_utm` |
| CRM-16.E3 | Chưa có lead | Chi phí / lead hiện "—" (không chia 0) |
| CRM-16.E4 | Lead từ form có `utm` của chiến dịch đã kết thúc | Vẫn gắn chiến dịch (đo hiệu quả trễ) |

---

## CRM-17 · Mẫu tin nhắn & gửi tin

> Là **Sale / giáo vụ / kế toán**, tôi muốn **gửi tin Zalo / SMS theo mẫu, tự điền tên, lớp, số tiền** để **nhắn nhanh và thống nhất**.

**Nguồn:** Tuyển sinh › Mẫu tin nhắn (`MessageTemplatesPage.tsx`), `SendMessageModal.tsx` · **Quyền cần:** `crm.message-templates.*` (quản lý mẫu); gửi tin theo quyền trang đang đứng

**Các bước:**
1. Mẫu theo tình huống (chào lead, nhắc test, nhắc tư vấn, nhắc học phí, báo nghỉ, báo đến lớp, báo vắng, báo cáo tuần, tùy chỉnh), kênh gợi ý, nội dung có biến `{ten}`, `{lop}`, `{so_tien}`…
2. Gửi: chọn người nhận (lead / HV / PH) → chọn mẫu → xem trước đã điền biến → sửa nếu cần → gửi → `message_logs` (nội dung thực tế, người gửi).
3. Lead › tab Tin đã gửi xem lại.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-17.E1 | Nội dung mẫu trống | `crm.message_template.body_required` |
| CRM-17.E2 | Biến không có dữ liệu | Để nguyên `{lop}` + cảnh báo trước khi gửi |
| CRM-17.E3 | Người nhận không có SĐT | Ẩn kênh SMS / Zalo |
| CRM-17.E4 | Chưa cấu hình tích hợp Zalo OA / SMS ⏳ | Ghi log trạng thái "chưa gửi được" — gửi thật khi có module Tích hợp |
| CRM-17.E5 | Tin tự động (nhắc hẹn, báo vắng…) | `auto = true`, `sent_by = null` |
| CRM-17.E6 🆕 | Trạng thái gửi | `message_logs.status`: `pending` (chưa có tích hợp) · `sent` · `failed` |

---

## CRM-18 · Phễu chuyển đổi

> Là **quản lý**, tôi muốn **xem lead rơi rụng ở bước nào theo Sale / nguồn / chiến dịch** để **biết cần cải thiện khâu nào**.

**Nguồn:** Tuyển sinh › Phễu chuyển đổi (`FunnelPage.tsx`, `funnel.ts`) · **Quyền cần:** `crm.funnel.view` (`export` để xuất)

**Các bước:** Chọn khoảng thời gian (theo ngày tạo lead) → phễu Lead → Đã liên hệ → Đã test → Chốt, tỷ lệ từng bước, thời gian trung bình mỗi bước;
bảng so sánh theo Sale / nguồn / chiến dịch. **Tính khi đọc** — không lưu bảng riêng.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-18.E1 | Phạm vi `own` | Chỉ số liệu lead của mình |
| CRM-18.E2 | "Đã test" | Có lượt test `done` **hoặc** lịch hẹn test `done` |
| CRM-18.E3 | Dữ liệu lớn | Truy vấn SQL có tham số / view `reporting` (CLAUDE.md mục 7) |

---

## CRM-19 · Tư vấn lên lớp (phía Sale)

> Là **Sale**, tôi muốn **nhận danh sách HV lớp sắp kết thúc đã được giữ chỗ lớp kỳ sau và chốt với người quyết định** để **giữ chân học viên**.

**Nguồn:** Tuyển sinh › Tư vấn lên lớp (`PromotionsPage.tsx`) · **Quyền cần:** `crm.promotions.view / update` · **Liên quan:** DT-17 (giáo vụ giữ chỗ)

**Các bước:**
1. Danh sách đề xuất đã **giao Sale** (lớp cũ kết thúc): HV, lớp cũ → lớp đề xuất, hạn giữ chỗ, nhận xét cuối khóa GV (chỉ NV xem), "HV / PH đã bấm Quan tâm".
2. **Gom theo người quyết định**: HV nhỏ tuổi → phụ huynh liên hệ chính, gom các con của cùng 1 PH vào 1 dòng.
3. Ghi chú tư vấn (kênh, nội dung) · **Chốt** → ghi danh lớp mới + hóa đơn (như CRM-07 bước 3–4) · **Từ chối** (lý do bắt buộc) · **Không liên lạc được**.
4. Thẻ tổng: tỉ lệ lên lớp = chốt ÷ (chốt + từ chối).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CRM-19.E1 | Đề xuất đã chốt / từ chối | `academic.promotion.closed` |
| CRM-19.E2 | Chốt khi chưa chọn lớp đích | `academic.promotion.to_class_required` |
| CRM-19.E3 | Hết hạn giữ chỗ (job hằng ngày) | Tự trả chỗ (`seat_held = false`), đề xuất vẫn mở để Sale tư vấn tiếp; chốt lúc này kiểm sĩ số như ghi danh mới |
| CRM-19.E4 | Lớp đích đầy khi chốt | `academic.class.full` |

---

## Dữ liệu / API (đã làm — giai đoạn 18)

| Story | API (`/api/v1/crm/...`) | Quyền |
|---|---|---|
| CRM-01 | `GET leads?status=&source=&branchId=&assigneeId=&unassigned=&campaignId=&slaOverdue=&followUpOverdue=&hidden=&q=&page=&pageSize=&sort=` · `GET leads/board` (đếm theo cột) · `GET leads/{id}` (thông tin + lịch sử tư vấn + HV liên kết) | `crm.leads.view` |
| CRM-02 | `POST leads` → `{ lead, warnings }` · `GET leads/duplicates?phone=&exceptId=` | `crm.leads.create` (duplicates: create hoặc update) |
| CRM-03 | `POST leads/import` `{ rows }` (1 request ≤ 1.000 dòng) → `{ created, leads, errors, warnings }` | `crm.leads.create` |
| CRM-04 | `PUT leads/{id}` `{ …, version }` · `POST leads/{id}/status` `{ status, lostReason? }` · `POST leads/{id}/assign` `{ saleId, note? }` | `crm.leads.update` |
| CRM-05 | `POST leads/{id}/consultations` (lịch sử đọc trong `GET leads/{id}`) | `crm.leads.update` |
| CRM-06 | `POST leads/{id}/hide` · `…/unhide` | `crm.leads.update` |
| CRM-07 | `POST leads/{id}/enroll` `{ who: self·linked·other, studentId?, student?, classId?, amount?, discount, dueDate? }` + `Idempotency-Key` | `crm.leads.update` (+ `students.profiles.create` khi tạo người học mới) |
| CRM-08 | `POST leads/{id}/students` · `PUT leads/{id}/students/{studentId}` `{ relationship, primary, billing, notify }` · `DELETE …` → `{ students, warnings }` | `crm.leads.update` |
| CRM-09 | `GET/POST lead-rules` · `PUT/DELETE lead-rules/{id}` · `PUT lead-rules/order` `{ ids }` · `GET lead-assignment-logs?leadId=&page=` | `crm.lead-rules.*` |
| CRM-12 | `GET/POST placement-tests` · `PUT placement-tests/{id}` `{ …, redraw }` ("Đổi đề") · `POST placement-tests/{id}/status` · `GET placement-tests/{id}/questions` (có đáp án — nhân viên) | `crm.placement-tests.*` |
| CRM-13 | `GET/POST placement-attempts` · `POST placement-attempts/{id}/log` · **công khai:** `GET public/placement/{token}` · `POST …/start` · `PUT …/answers` (lưu dần) · `POST …/submit` | `.view` / `.create` hoặc `.update` / không cần đăng nhập (giới hạn theo IP) |
| CRM-14, 15 | `GET appointments?from=&to=&branchId=&leadId=&staffId=&status=` · `POST appointments/check` (trả `[{ kind, message }]`) · `POST appointments` · `PUT appointments/{id}` (đổi lịch) · `POST appointments/{id}/done\|no-show\|cancel` `{ text }` | `crm.appointments.*` |
| CRM-16 | `GET/POST campaigns` · `PUT campaigns/{id}?version=` · `POST campaigns/{id}/status` (kèm `stats`) | `crm.campaigns.*` |
| CRM-17 | `GET/POST message-templates` · `PUT/DELETE message-templates/{id}` · `POST messages` · `GET messages?leadId=\|studentId=` | `crm.message-templates.*`; gửi / xem tin theo quyền trang đang đứng |
| CRM-18 | `GET funnel?from=&to=&branchId=` → hành trình từng lead (frontend gom theo Sale / nguồn / chiến dịch) | `crm.funnel.view` |
| CRM-19 | **Academic** (dữ liệu lên lớp của Academic): `GET /api/v1/academic/promotions/sale` (kèm người quyết định) · `POST /api/v1/academic/promotions/{id}/notes\|unreachable\|decline\|confirm` | `crm.promotions.*` |

- Trùng lịch hẹn: `409 crm.appointment.conflict` + `errors: { room: […], staff: […], lead: […] }`.
- Vai trò mẫu Sale có thêm `students.profiles.create` phạm vi own (chốt lead tạo hồ sơ HV) — migration `GrantSaleCreateStudentFromLead`.
- ⏳ Thu tiền ngay khi chốt (CRM-07 bước 3) làm cùng Tài chính (giai đoạn 19); gửi Zalo / SMS / email thật làm cùng Tích hợp (giai đoạn 22) —
  tin ghi trạng thái `pending`. "Trong ca" khi chia lead = giờ hành chính tới khi có Xếp ca (giai đoạn 21). Xuất Excel lead (CRM-01.E5) làm ở frontend.
- Chốt lead = 2 giao dịch nối tiếp (Students tạo HV + ghi danh, rồi Crm liên kết + chốt); Crm lỗi sau đó thì HV vẫn còn — liên kết tay.
- JSON khớp `frontend/src/features/crm/types.ts` (`Lead`, `Consultation`, `LeadStudentLink`, `LeadRule`, `PlacementTest`, `PlacementAttempt`, `Campaign`).
- **Job (Quartz):** quét SLA mỗi phút (CRM-11) · nhắc lịch hẹn mỗi 5 phút (CRM-14) · hết hạn link test mỗi giờ (CRM-13.E7).
- **Cho module khác** (`Crm.Contracts`) 🆕: `IGuardianDirectory` (người liên hệ chính / nhận hóa đơn / nhận thông báo của 1 HV — Finance, Academic, Notifications dùng);
  integration event `LeadWon`, `LeadStudentLinked`, `LeadStudentUnlinked` (System cập nhật quyền Cổng phụ huynh).
- **Nghe event:** `EmployeeQuit` (Hr) → bỏ Sale khỏi pool chia lead, báo quản lý số lead đang giữ cần chuyển.
- **Mã lỗi cần thêm vào Domain** (chưa có): `crm.lead.duplicate_phone` (cảnh báo), `crm.lead.invalid_assignee`, `crm.lead.won_requires_enrollment`,
  `crm.import.too_many_rows`, `crm.placement_test.band_zero_required`, `crm.appointment.conflict`, `crm.appointment.not_started`,
  `crm.campaign.duplicate_code`, `crm.campaign.duplicate_utm`.
