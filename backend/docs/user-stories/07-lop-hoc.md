# 07 · Lớp học & buổi học

Module: **Academic** · Bảng: `academic.classes`, `class_sessions`, `attendance_records`, `teacher_attendances`, `session_support_entries`,
`session_cancel_requests`, `leave_requests`, `substitute_invites`, `student_evaluations`, `promotion_candidates`, `promotion_notes`, `rooms` ([database.md](../database.md))
Frontend: `pages/staff/academic/classes/{ClassesPage,ClassDetailPage}.tsx`, `pages/staff/academic/sessions/SessionsPage.tsx`,
`features/academic/classes/{ClassFormFields.tsx,sessionGenerator.ts,sessionUtils.ts,attendanceQr.ts}`, `features/academic/classes/detail/**`,
`features/academic/sessions/{sessionOps.ts,IncidentModal.tsx}`, `pages/staff/workflow/requests/RequestsPage.tsx`, `features/portal/**` (cổng GV / HV / PH),
`features/academic/promotion/**`

## Mô hình (đọc trước)

```
Course (sản phẩm) ──mở──▶ Class  (chốt curriculum_id lúc mở; mã GVV_ / GVN_ / GVVN_ theo loại GV của khóa)
                            status: enrolling ──Khai giảng──▶ ongoing ⇄ paused ──Kết thúc──▶ finished
                                       └───────────── Hủy (từ mọi trạng thái chưa kết thúc) ──▶ cancelled
                            └─ ClassSession[]  sinh từ lịch tuần (bỏ ngày lễ, gán bài / buổi thi theo lộ trình)
                                 kind: lesson | exam · origin: regular | makeup | extra
                                 status: scheduled ──(diễn ra)──▶ completed     scheduled ──Hủy──▶ cancelled ──▶ (xếp bù)
                                 ├─ AttendanceRecord[]   HV: present · late · excused · absent  (QR / GV / NV)
                                 ├─ TeacherAttendance[]  GV / trợ giảng / dạy thay — căn cứ lương
                                 ├─ báo cáo buổi của GV · sự cố giữa buổi · nhật ký hỗ trợ NV
                                 └─ confirmed_by/at      NV xác nhận "đã diễn ra" → tính tiến độ + lương GV
```

**Quy tắc vàng:**
1. **Sĩ số** = ghi danh hiệu lực (`studying` + `reserved`) — đọc từ Students; chỗ **giữ tạm lên lớp không tính**.
2. Buổi chỉ được tính lương / tiến độ khi **đã xác nhận** (DT-15). Buổi hủy không tính.
3. Trùng lịch (GV, phòng, ngày lễ) khi xếp buổi là **cảnh báo**; chỉ giờ sai mới chặn. Riêng **mở lớp** thì trùng GV / phòng là **chặn** (lịch lặp dài hạn).
4. Lộ trình đã chốt cho lớp không đổi theo phiên bản mới của lộ trình (lớp giữ `curriculum_id` cũ).
5. Cổng GV chỉ thấy lớp mình dạy (kể cả buổi dạy thay), **không** thấy học phí / lịch sử chăm sóc; cổng HV / PH chỉ thấy lớp của mình / con mình.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| DT-01 | Xem danh sách lớp | Giáo vụ, Sale | Cao |
| DT-02 | Mở lớp | Giáo vụ | Cao |
| DT-03 | Khai giảng lớp | Giáo vụ | Cao |
| DT-04 | Kết thúc lớp | Giáo vụ | Cao |
| DT-05 | Hủy lớp | Giáo vụ, quản lý | TB |
| DT-06 | Tạm dừng / học lại | Giáo vụ | TB |
| DT-07 | Sửa thông tin lớp | Giáo vụ | Cao |
| DT-08 | Sinh lịch buổi học tự động | Giáo vụ | Cao |
| DT-09 | Điểm danh học viên (QR / GV / nhân viên) | GV, giáo vụ, HV | Cao |
| DT-10 | Dời lịch, đổi GV / phòng cho 1 buổi; buổi học thêm, học bù | Giáo vụ | Cao |
| DT-11 | Hủy 1 buổi | Giáo vụ | Cao |
| DT-12 | Chấm công GV / trợ giảng theo buổi | GV, giáo vụ | Cao |
| DT-13 | Báo sự cố giữa buổi | Giáo vụ, lễ tân | TB |
| DT-14 | Kết thúc buổi & báo cáo buổi của GV | GV | Cao |
| DT-15 | Xác nhận buổi đã dạy | Giáo vụ | Cao |
| DT-16 | Đơn hủy buổi của GV & mời dạy thay | GV, giáo vụ | Cao |
| DT-17 | Lên lớp: giữ chỗ lớp kỳ sau & giao Sale | Giáo vụ | TB |
| DT-18 | Đơn xin nghỉ của HV / PH & học bù lớp khác | HV, PH, giáo vụ | Cao |
| DT-19 | Nhận xét cuối khóa của GV | GV | TB |
| DT-20 | Thời khóa biểu & bảng buổi học trong ngày | Giáo vụ, GV, HV, PH | Cao |

**Quyền:** `academic.classes` (lớp, HV trong lớp) · `academic.sessions` (buổi, điểm danh hộ, xác nhận — `approve` = xác nhận buổi) ·
`academic.requests` (đơn chờ xử lý — `approve` = duyệt). Cổng GV / HV / PH không dùng mã quyền mà lọc theo chủ tài khoản (PQ-11).

---

## DT-01 · Xem danh sách lớp

> Là **giáo vụ**, tôi muốn **xem mọi lớp theo trạng thái, sĩ số, tiến độ** để **biết lớp nào cần khai giảng, lớp nào sắp kết thúc**.

**Nguồn:** Đào tạo › Lớp học (`ClassesPage.tsx`) · **Quyền cần:** `academic.classes.view`

**Các bước:** Thẻ tổng theo trạng thái → bảng: mã, tên, khóa, GV, phòng / online, lịch (thứ + giờ), khai giảng – kết thúc, sĩ số `x/max` (đỏ khi < min),
tiến độ `buổi đã xác nhận / tổng`, trạng thái. Lọc: chi nhánh, khóa, GV, trạng thái, "sắp kết thúc (≤ 2 tuần)", "chưa đủ sĩ số". Bấm → trang chi tiết lớp
(tab Tổng quan · Buổi học · Học viên & học phí · Điểm danh · Bài tập · Bài thi · Tiến độ · Thông báo · Đánh giá GV · Lên lớp).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-01.E1 | Phạm vi `own` 🆕 | Lớp mình là GV / trợ giảng / người tạo |
| DT-01.E2 | Cột học phí / còn nợ | Chỉ trả khi có `finance.invoices.view` |

---

## DT-02 · Mở lớp

> Là **giáo vụ**, tôi muốn **mở lớp mới theo khóa với GV, phòng, lịch, sĩ số, học phí** để **Sale bắt đầu ghi danh**.

**Nguồn:** Lớp học › "Mở lớp" (`ClassFormFields.tsx`) · **Quyền cần:** `academic.classes.create`

**Các bước:**
1. Chọn **khóa*** (đang bán ở chi nhánh) → tự điền: lộ trình mặc định, tổng giờ (= số buổi × phút/buổi), sĩ số max, học phí (giá chi nhánh nếu có — TC-01), tiền tố mã.
2. Nhập: chi nhánh* (`active`), mã lớp* = tiền tố (`GVV_` GV Việt · `GVN_` GV Nhật · `GVVN_` kết hợp) + phần đuôi (chữ không dấu, số, `-`), tên*, GV* (1–2),
   trợ giảng, phòng **hoặc** online + link học, khai giảng dự kiến*, thứ học*, giờ bắt đầu* – kết thúc*, tổng giờ*, sĩ số min* – max*, học phí*, lớp trước (nếu lên lớp), ghi chú.
3. Hệ thống tính số buổi = tổng giờ ÷ giờ mỗi buổi, ngày kết thúc dự kiến theo lịch (bỏ ngày lễ) → hiện xem trước.
4. Lưu → `enrolling` → **DT-08** sinh lịch buổi ngay (tùy chọn, mặc định bật).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-02.E1 | Mã trùng (không phân biệt hoa thường) | "Mã GVV_N3-K12 đã tồn tại" 🆕 `academic.class.duplicate_code` |
| DT-02.E2 | Số GV ngoài 1–2; lớp Việt + Nhật thiếu 1 GV mỗi loại | `academic.class.teacher_count` / "Lớp Việt + Nhật cần 1 GV Việt và 1 GV Nhật" |
| DT-02.E3 | Offline không có phòng | `academic.class.room_required` |
| DT-02.E4 | Giờ kết thúc ≤ giờ bắt đầu | `academic.class.invalid_time` |
| DT-02.E5 | Không chọn thứ học | `academic.class.invalid_weekdays` |
| DT-02.E6 | Max < min, min ≤ 0, max > sức chứa phòng | `academic.class.invalid_size` |
| DT-02.E7 | Ngày kết thúc dự kiến trước khai giảng | `academic.class.invalid_dates` |
| DT-02.E8 | GV đã dạy lớp khác / phòng đã có lớp **trùng thứ + giờ** trong khoảng ngày | 🆕 `academic.class.schedule_conflict` kèm danh sách lớp trùng |
| DT-02.E9 | GV đã nghỉ việc / phòng bảo trì | Không có trong ô chọn; API từ chối 🆕 `academic.class.invalid_teacher` / `academic.room.unavailable` |
| DT-02.E10 | Khóa không bán ở chi nhánh / đang tạm ngừng | 🆕 `academic.course.not_available` |

---

## DT-03 · Khai giảng lớp

> Là **giáo vụ**, tôi muốn **chuyển lớp sang "đang học" với ngày khai giảng thực tế** để **lịch, điểm danh, lương GV bắt đầu chạy**.

**Nguồn:** Chi tiết lớp › "Khai giảng" · **Quyền cần:** `academic.classes.update`

**Các bước:** Chọn ngày khai giảng thực tế* (mặc định hôm nay); tùy chọn "Sinh lại các buổi chưa dạy từ ngày này (bỏ ngày lễ)" → `ongoing`, `actual_start_date`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-03.E1 | Lớp không ở `enrolling` | `academic.class.not_enrolling` |
| DT-03.E2 | Sĩ số < tối thiểu | **Chốt:** chỉ **cảnh báo** "Lớp mới có N HV, chưa đủ tối thiểu M" — vẫn cho khai giảng; API trả `{ class, warnings }` |
| DT-03.E3 | Lớp chưa có HV nào | **Chặn** `academic.class.not_enough_students` (lỗi duy nhất dùng mã này) |

---

## DT-04 · Kết thúc lớp

> Là **giáo vụ**, tôi muốn **đóng lớp khi học xong** để **HV chuyển "hoàn thành", Sale bắt đầu tư vấn lên lớp**.

**Quyền cần:** `academic.classes.update`

**Các bước:** "Kết thúc lớp" → ngày kết thúc thực tế* → hộp nêu: N buổi chưa dạy sẽ bị **hủy** (lý do "Lớp kết thúc sớm"), M HV đang học → "Hoàn thành" →
`finished`, `actual_end_date` → phát **`ClassFinished`** → Students (HV-11), Crm (giao Sale các đề xuất lên lớp — DT-17), Finance (khóa lương tháng tính đủ).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-04.E1 | Lớp không ở `ongoing` / `paused` | `academic.class.not_ongoing` |
| DT-04.E2 | Còn buổi đã diễn ra chưa xác nhận | Cảnh báo "N buổi chưa xác nhận — chưa tính lương GV"; vẫn cho |
| DT-04.E3 | HV đang bảo lưu | Giữ `reserved` (HV-07.E4) |

---

## DT-05 · Hủy lớp

> Là **giáo vụ / quản lý**, tôi muốn **hủy lớp không đủ HV** để **dừng lịch, công nợ và xử lý hoàn tiền**.

**Quyền cần:** `academic.classes.update` (🆕 đề xuất `academic.classes.delete` vì không hoàn tác)

**Các bước:** "Hủy lớp" → hộp nêu: N HV → "Đã nghỉ", hủy học phí còn lại; M HV đã đóng tiền → hoàn ở tab Học viên & học phí; K buổi chưa dạy bị hủy.
**Lý do bắt buộc** + **gõ mã lớp** để xác nhận → `cancelled`, `cancel_reason` → phát **`ClassCancelled`** (Students HV-10.E2; Notifications báo HV / PH / GV).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-05.E1 | Lớp đã `finished` / `cancelled` | `academic.class.closed` |
| DT-05.E2 | Thiếu lý do | `academic.class.cancel_reason` |
| DT-05.E3 | Gõ sai mã | Nút xác nhận khóa |
| DT-05.E4 | Đề xuất lên lớp có lớp đích là lớp này | Bỏ lớp đích, trả về `pending` + báo giáo vụ chọn lớp khác 🆕 |

---

## DT-06 · Tạm dừng / học lại

**Quyền cần:** `academic.classes.update`

- **Tạm dừng** (lý do bắt buộc) → `paused`, ghi chú "Tạm dừng: …". Buổi chưa dạy **giữ nguyên** trên lịch (không tự hủy).
- **Học lại** → `ongoing`, nhắc "Kiểm tra lịch các buổi còn lại" → giáo vụ dùng DT-08 "sinh lại phần còn lại".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-06.E1 | Tạm dừng lớp không `ongoing` | `academic.class.not_ongoing` |
| DT-06.E2 | Học lại lớp không `paused` | `academic.class.not_paused` |
| DT-06.E3 | Ghi danh mới khi lớp `paused` | `academic.class.closed` |
| DT-06.E4 | Điểm danh / QR buổi của lớp đang tạm dừng 🆕 | Chặn — báo "Lớp đang tạm dừng" |

---

## DT-07 · Sửa thông tin lớp

**Quyền cần:** `academic.classes.update` · Form như DT-02.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-07.E1 | Lớp đã `ongoing`: đổi khóa, lộ trình, mã | Khóa (không cho đổi) |
| DT-07.E2 | Đổi học phí khi đã có ghi danh | Chỉ áp cho ghi danh **mới**; hóa đơn cũ giữ nguyên |
| DT-07.E3 | Giảm sĩ số max dưới số HV hiện có | `academic.class.invalid_size` |
| DT-07.E4 | Đổi GV / phòng / lịch | Hỏi "Áp cho các buổi chưa dạy?" → cập nhật buổi `scheduled` chưa bị ghi đè riêng; kiểm trùng như DT-02.E8 |
| DT-07.E5 | Lớp đã `finished` / `cancelled` | `academic.class.closed` (chỉ sửa ghi chú) |

---

## DT-08 · Sinh lịch buổi học tự động

> Là **giáo vụ**, tôi muốn **sinh đủ buổi theo lịch tuần, bỏ ngày lễ, tự gán bài và buổi thi theo lộ trình** để **không phải tạo từng buổi**.

**Nguồn:** tab Buổi học › "Tạo lịch tự động" (`sessionGenerator.ts`) · **Quyền cần:** `academic.sessions.create`

**Các bước:**
1. Nhập: bắt đầu từ ngày*, thứ học*, giờ, số buổi (mặc định = tổng giờ ÷ giờ/buổi − số buổi đã dạy), "bỏ qua ngày lễ".
2. Chia nội dung: mỗi bài ~ (số buổi học ÷ số bài) buổi; **buổi thi** chèn ngay sau bài của mốc thi (giữa kỳ / cuối kỳ). Không có lộ trình → buổi học chưa gán bài.
3. Xem trước: danh sách buổi, ngày bị bỏ vì lễ (tên lễ), ngày kết thúc mới. Xác nhận → **thay toàn bộ buổi `scheduled` chưa diễn ra** bằng lịch mới,
   đánh số tiếp từ buổi đã dạy; cập nhật `planned_end_date`.
4. Mỗi buổi có `attendance_qr_token` riêng (cố định).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-08.E1 | Không chọn thứ / số buổi ≤ 0 | `academic.class.invalid_weekdays` / lỗi theo trường |
| DT-08.E2 | Buổi đã diễn ra / đã điểm danh / đã xác nhận | **Không** bị thay |
| DT-08.E3 | Buổi đã ghi đè GV / phòng riêng (dạy thay, đổi phòng) | Bị thay — hộp xác nhận liệt kê "N buổi có thay đổi riêng sẽ mất" |
| DT-08.E4 | Buổi học bù / học thêm chưa diễn ra | Giữ nguyên (không thuộc chuỗi thường) |
| DT-08.E5 | Danh sách ngày lễ | 🆕 lấy từ Cài đặt chung (bảng `settings`), không cứng trong code |

---

## DT-09 · Điểm danh học viên

> Là **GV / giáo vụ**, tôi muốn **điểm danh nhanh bằng QR hoặc bấm tay** để **có chuyên cần chính xác và báo phụ huynh kịp thời**.

**Nguồn:** `SessionAttendanceTab.tsx`, `attendanceQr.ts`, cổng HV `ScanAttendanceModal.tsx`, cổng GV `TeachingSessionPage.tsx`
· **Quyền cần:** GV của buổi (cổng GV) · `academic.sessions.update` (nhân viên) · HV (quét QR của buổi mình học)

**Các bước:**
1. Danh sách = HV ghi danh hiệu lực + HV lớp khác **đến học bù** buổi này (DT-18). Mặc định: đơn xin nghỉ đã duyệt → `excused`; đang bảo lưu → `excused`; còn lại `absent`.
2. **QR**: GV / giáo vụ mở QR của buổi (mở được từ **30 phút trước giờ học**). HV quét trên cổng (camera) hoặc nhập **mã 6 ký tự** →
   `present` (quá **10 phút** sau giờ bắt đầu → `late`), `check_in_at`, `method = qr`.
3. **Bấm tay**: GV (`teacher`) hoặc nhân viên (`staff`) chọn có mặt / muộn / vắng có phép / vắng không phép, ghi chú; "Tất cả có mặt".
4. **Về sớm**: giờ về + lý do (vẫn tính có mặt).
5. Lưu → `attendance_by/at` → **thông báo phụ huynh** (Cài đặt: báo đến lớp; báo vắng sau N phút) + nhật ký hỗ trợ nếu nhân viên điểm hộ.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-09.E1 | Quét QR sai / mã không khớp buổi | "Mã điểm danh không đúng" 🆕 `academic.attendance.invalid_code` |
| DT-09.E2 | HV quét QR của lớp mình không học / không đăng ký học bù | 🆕 `academic.attendance.not_in_roster` |
| DT-09.E3 | Quét trước giờ mở / sau khi buổi kết thúc | 🆕 `academic.attendance.qr_closed` |
| DT-09.E4 | Buổi đã hủy | `academic.session.cancelled` |
| DT-09.E5 | Sau khi lưu có HV vắng 2 buổi liên tiếp | Cảnh báo "N HV vắng 2 buổi liên tiếp — nhắc giáo vụ liên hệ" + đẩy vào HV-14 |
| DT-09.E6 | Sửa điểm danh sau khi buổi đã xác nhận | Cho (giáo vụ), ghi nhật ký `warning`; GV **không** sửa được sau khi xác nhận |
| DT-09.E7 | Đơn xin nghỉ được duyệt **sau** khi đã điểm danh `absent` | Tự sửa thành `excused` theo đơn; HV vẫn đến (present / late) thì giữ nguyên |
| DT-09.E8 | Quét 2 lần | Giữ lần đầu (không đổi `late` → `present`) |

---

## DT-10 · Dời lịch, đổi GV / phòng cho 1 buổi; buổi học thêm, học bù

> Là **giáo vụ**, tôi muốn **dời / đổi người dạy / đổi phòng cho riêng 1 buổi hoặc thêm buổi** để **xử lý lịch phát sinh mà không đụng tới cả lớp**.

**Nguồn:** `SessionScheduleModal.tsx`, `sessionUtils.ts › findConflicts` · **Quyền cần:** `academic.sessions.update` (sửa) / `.create` (thêm)

**Các bước:**
1. **Sửa buổi**: ngày*, giờ từ* – đến*, GV* (đổi = dạy thay), trợ giảng, phòng*, **lý do thay đổi*** → lưu lịch gốc vào `rescheduled_from_*`, `reschedule_reason`.
2. **Thêm buổi**: loại *học bù* (chọn buổi bị hủy cần bù* → `makeup_for_id`) hoặc *học thêm* (không đánh số chuỗi, không cần bù).
3. Kiểm tra trùng → **cảnh báo** (không chặn): trùng buổi khác của lớp, GV đang dạy lớp khác, phòng đã có lớp, ngày lễ. Không trùng → "Không trùng lịch GV, phòng, ngày lễ".
4. Lưu → thông báo HV / PH / GV liên quan (đổi giờ / phòng).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-10.E1 | Giờ kết thúc ≤ bắt đầu | `academic.session.invalid_time` |
| DT-10.E2 | Sửa buổi không còn `scheduled` | `academic.session.not_scheduled` |
| DT-10.E3 | Học bù không chọn buổi gốc | `academic.session.makeup_for_required` |
| DT-10.E4 | Buổi gốc đã có buổi bù khác | 🆕 `academic.session.already_made_up` |
| DT-10.E5 | Thiếu lý do khi sửa | Lỗi theo trường |
| DT-10.E6 | Đổi GV cho buổi | Chấm công vai trò `substitute` cho GV mới; GV cũ không tính buổi đó |

---

## DT-11 · Hủy 1 buổi

**Quyền cần:** `academic.sessions.update` · `ClassSessionsTab.tsx › cancel`

**Các bước:** "Hủy buổi" → hộp nêu: không tính tiến độ, lương GV; N HV nhận thông báo nghỉ (cổng + Zalo); buổi thường → có thể xếp bù ngay (mở DT-10 loại học bù);
buổi học bù bị hủy → buổi gốc quay lại "Chờ xếp bù"; buổi học thêm → không cần bù. **Lý do bắt buộc** → `cancelled`, `cancelled_by/at`, `note`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-11.E1 | Buổi đã bắt đầu | "Buổi học đã bắt đầu — không thể hủy" 🆕 `academic.session.already_started` (dùng DT-13 sự cố) |
| DT-11.E2 | Buổi đã hủy | `academic.session.cancelled` |
| DT-11.E3 | Buổi thi | Cảnh báo thêm "Buổi thi — cần xếp lại kỳ thi" |
| DT-11.E4 | Đơn xin nghỉ đang chờ của buổi này | Tự đóng đơn (không cần duyệt), báo HV / PH |

---

## DT-12 · Chấm công GV / trợ giảng theo buổi

> Là **GV**, tôi muốn **check-in / check-out buổi dạy trên cổng GV** để **được tính lương đúng buổi**; giáo vụ chấm hộ khi GV quên.

**Nguồn:** cổng GV `TeachingSessionPage.tsx`, `SessionDetailDrawer.tsx`, `SessionSupport.tsx` · **Quyền cần:** GV của buổi · `academic.sessions.update` (chấm hộ)

**Các bước:**
1. GV mở buổi (từ 30 phút trước) → **check-in** (`present`, hoặc `late` nếu sau giờ bắt đầu) → dạy → **check-out** khi kết thúc buổi (DT-14).
2. Vai trò: `teacher` GV lớp · `substitute` dạy thay (GV ghi đè ở buổi) · `assistant` trợ giảng. `source` = `self` / `staff`.
3. Giáo vụ chấm hộ: chọn trạng thái, giờ vào / ra, ghi chú; GV vắng → chọn người dạy thay (`substitute_id`) → ghi nhật ký hỗ trợ "teacher_checkin".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-12.E1 | Người không phải GV / trợ giảng của buổi check-in | 403 (PQ-11) |
| DT-12.E2 | Check-out trước check-in | 🆕 `academic.teacher_attendance.not_checked_in` |
| DT-12.E3 | Buổi đã xác nhận | GV không sửa; giáo vụ sửa có nhật ký |
| DT-12.E4 | GV `absent` | Buổi không tính lương cho GV đó; nếu không có người thay → gợi ý DT-11 |

---

## DT-13 · Báo sự cố giữa buổi

> Là **giáo vụ / lễ tân**, tôi muốn **ghi nhận GV gặp sự cố giữa buổi và cách xử lý** để **có căn cứ tính lương, báo PH và theo dõi**.

**Nguồn:** `IncidentModal.tsx`, `sessionOps.ts › INCIDENT_REASONS` · **Quyền cần:** `academic.sessions.update`

**Các bước:**
1. Giờ xảy ra*, lý do* (ngất, ốm, việc gia đình, tai nạn, sự cố kỹ thuật, khác), chi tiết, cách xử lý* (tạm dừng · GV khác dạy tiếp · trợ giảng dạy tiếp · kết thúc sớm),
   người dạy tiếp (nếu có), đã báo HV / PH.
2. Sau đó ghi **kết quả**: học tiếp (ai dạy) / cho nghỉ → `incident_outcome*`.
3. Người dạy tiếp được chấm công vai trò `substitute` cho buổi.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-13.E1 | Ghi kết quả khi chưa có sự cố | `academic.session.no_incident` |
| DT-13.E2 | Buổi chưa bắt đầu | Không cho — dùng DT-10 / DT-11 |
| DT-13.E3 | Kết thúc sớm | Buổi vẫn có thể xác nhận; 🆕 lương tính theo quy định "buổi kết thúc sớm" ở Finance (TC-13) |

---

## DT-14 · Kết thúc buổi & báo cáo buổi của GV

> Là **GV**, tôi muốn **ghi đã dạy gì, BTVN, tiến độ bài** để **giáo vụ nắm tình hình và buổi sau dạy tiếp đúng chỗ**.

**Nguồn:** cổng GV `EndSessionModal.tsx` · **Quyền cần:** GV của buổi

**Các bước:** "Kết thúc buổi học" → nội dung đã dạy, BTVN, vấn đề cần trung tâm xử lý, tiến độ (xong bài / một phần / chưa dạy), tick chapter đã dạy xong
→ `report_*`, `report_ended_at` (lần đầu) → check-out GV (DT-12) → buổi chuyển "Chờ xác nhận".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-14.E1 | Sửa báo cáo sau khi buổi đã xác nhận | 🆕 Cho sửa trong 24 giờ, sau đó khóa |
| DT-14.E2 | Tiến độ "một phần" / "chưa dạy" | Buổi sau hiện chapter còn dở; giáo vụ nhận cảnh báo chậm tiến độ khi ≥ 2 buổi liên tiếp |
| DT-14.E3 | Có "vấn đề cần xử lý" | Thông báo giáo vụ chi nhánh |

---

## DT-15 · Xác nhận buổi đã dạy

> Là **giáo vụ**, tôi muốn **xác nhận buổi đã thực sự diễn ra** để **buổi được tính tiến độ, trừ gói học phí và làm căn cứ lương GV**.

**Nguồn:** `SessionDetailDrawer.tsx › confirmHeld`, bảng "Chờ xác nhận" ở `SessionsPage.tsx` · **Quyền cần:** `academic.sessions.approve`

**Các bước:**
1. Checklist: GV đã check-in · đã check-out · đã điểm danh HV (x/y) · có nhật ký buổi (không bắt buộc). Thiếu → hiện "Lưu ý: …" nhưng **vẫn cho** xác nhận.
2. Xác nhận → `confirmed_by/at`, `confirm_note`; buổi `scheduled` → `completed` → phát `SessionConfirmed` (Finance: lương GV).
3. "Bỏ xác nhận" → quay lại "Chờ xác nhận", **chưa tính lương** đến khi xác nhận lại.
4. Xác nhận hàng loạt nhiều buổi (1 request).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-15.E1 | Buổi đã xác nhận | `academic.session.already_confirmed` |
| DT-15.E2 | Buổi chưa tới giờ kết thúc | 🆕 `academic.session.not_finished` |
| DT-15.E3 | Buổi đã hủy | `academic.session.cancelled` |
| DT-15.E4 | Chưa điểm danh HV nào | `academic.session.not_attended` — chặn (khác các mục còn lại chỉ cảnh báo) |
| DT-15.E5 | Bỏ xác nhận khi tháng lương đã chốt | 🆕 `finance.payroll.locked` — phải mở khóa bảng lương trước (TC-13) |

---

## DT-16 · Đơn hủy buổi của GV & mời dạy thay

> Là **GV**, tôi muốn **xin hủy 1 buổi trước khi dạy**; là **giáo vụ**, tôi muốn **duyệt rồi mời GV khác dạy thay (ai nhận trước dạy) hoặc hủy buổi và xếp bù**.

**Nguồn:** cổng GV `CancelRequestModal.tsx`, Đơn chờ xử lý (`RequestsPage.tsx`), cổng GV "Lời mời dạy thay" · **Quyền cần:** GV (gửi) · `academic.requests.approve` (duyệt)

**Các bước:**
1. GV chọn buổi chưa diễn ra*, loại lý do*, chi tiết* (≥ 10 ký tự), đề xuất ngày dạy bù → `pending` → thông báo giáo vụ. GV **thu hồi** được khi còn `pending`.
2. Giáo vụ **từ chối** (lý do) hoặc **duyệt** → chọn 1 trong 2:
   - **Mời dạy thay**: chọn ≥ 1 GV (gợi ý GV rảnh giờ đó, dạy được cấp độ, không trùng lịch), hạn trả lời → mỗi GV 1 lời mời `pending` (thông báo realtime).
     GV đầu tiên **nhận** → buổi ghi đè `teacher_ids` = GV đó, các lời mời khác → `expired` "GV khác đã nhận". GV từ chối → ghi lý do.
   - **Hủy buổi & xếp học bù**: như DT-11 rồi DT-10 (học bù).
3. Hết hạn trả lời mà chưa ai nhận (job) → lời mời `expired`, báo giáo vụ chọn cách khác.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-16.E1 | Gửi cho buổi đã bắt đầu | "Buổi đã bắt đầu — gọi giáo vụ để báo sự cố" 🆕 `academic.session.already_started` |
| DT-16.E2 | Thiếu lý do | `academic.cancel_request.reason_required` |
| DT-16.E3 | Duyệt / từ chối đơn không `pending` | `academic.request.not_pending` |
| DT-16.E4 | Nhận / từ chối lời mời đã hết hiệu lực | `academic.invite.closed` |
| DT-16.E5 | 2 GV bấm nhận cùng lúc | Khóa lạc quan trên buổi — người sau nhận `academic.invite.closed` |
| DT-16.E6 | Buổi hoặc lớp không còn tồn tại | "Buổi học hoặc lớp học liên kết không còn tồn tại" — đóng đơn |
| DT-16.E7 | GV đã có đơn `pending` cho buổi đó 🆕 | `academic.cancel_request.duplicate` |

---

## DT-17 · Lên lớp: giữ chỗ lớp kỳ sau & giao Sale

> Là **giáo vụ**, tôi muốn **giữ chỗ tạm cho HV lớp sắp kết thúc vào lớp kỳ sau theo gợi ý** để **Sale tư vấn có sẵn chỗ, không bị lớp đầy**.

**Nguồn:** Chi tiết lớp › tab Lên lớp (`ClassPromotionTab.tsx`, `promotionUtils.ts`) · **Quyền cần:** `academic.classes.update` · **Phía Sale:** CRM-19

**Các bước:**
1. Danh sách HV đang học + gợi ý từ **nhận xét cuối khóa** (DT-19: lên trình độ / nâng cao / JLPT / học lại), chuyên cần, điểm (nguồn `teacher` / `stats` / `manual`).
2. Chọn lớp đích (lớp `enrolling` cùng chi nhánh, thường có `previous_class_id` = lớp này) → **giữ chỗ tạm** `seat_held`, `hold_until` (không tính sĩ số, không hóa đơn).
3. HV / PH bấm **"Quan tâm"** lớp kỳ sau trên cổng → `interest_at`, `interest_by`.
4. Lớp cũ kết thúc (DT-04) → các đề xuất tự **giao Sale** (Sale phụ trách lead liên kết; không có → giáo vụ chọn) → CRM-19.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-17.E1 | Giữ chỗ khi chưa chọn lớp đích | `academic.promotion.to_class_required` |
| DT-17.E2 | Lớp đích + chỗ giữ vượt sĩ số max | Cảnh báo "Giữ chỗ vượt sĩ số" — vẫn cho (chỗ giữ không tính) |
| DT-17.E3 | Hết hạn giữ chỗ (job hằng ngày) | `seat_held = false` (CRM-19.E3) |
| DT-17.E4 | Giao Sale thủ công trước khi lớp kết thúc | Cho ("Giao Sale ngay") |

---

## DT-18 · Đơn xin nghỉ của HV / PH & học bù lớp khác

> Là **HV / phụ huynh**, tôi muốn **xin nghỉ 1 buổi và đăng ký học bù ở lớp khác cùng khóa** để **không bị mất bài**.

**Nguồn:** cổng HV / PH `LeaveRequestModal.tsx`, `LeaveRequestList.tsx`, Đơn chờ xử lý · **Quyền cần:** HV / PH của HV đó (gửi) · `academic.requests.approve` (duyệt)

**Các bước:**
1. Chọn buổi **chưa diễn ra***, loại lý do*, chi tiết* (≥ 5 ký tự); PH gửi thay con (`by = parent`).
2. Tùy chọn học bù: buổi của **lớp khác cùng khóa** trong **3 tuần tới**, cùng bài, **không trùng giờ** các lớp HV đang học, lớp đích còn chỗ.
3. Giáo vụ duyệt / từ chối → duyệt: buổi gốc HV tự `excused` (DT-09); HV có tên ở danh sách điểm danh buổi bù (`makeup_for_session_id`).
4. Sau buổi bù: kết quả điểm danh buổi bù ghi vào `made_up_status` của buổi gốc.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-18.E1 | Buổi đã bắt đầu | "Buổi này đã bắt đầu — liên hệ giáo vụ để báo vắng" 🆕 `academic.leave_request.too_late` |
| DT-18.E2 | Đã có đơn cho buổi đó 🆕 | `academic.leave_request.duplicate` |
| DT-18.E3 | Buổi bù đầy chỗ khi duyệt | Duyệt đơn nghỉ, bỏ đăng ký bù, báo HV chọn buổi khác |
| DT-18.E4 | Duyệt / từ chối đơn không `pending` | `academic.request.not_pending` |
| DT-18.E5 | PH xin cho HV không liên kết | 403 (PQ-11) |
| DT-18.E6 | Xin nghỉ quá N buổi / khóa 🆕 | Cảnh báo cho giáo vụ khi duyệt (ngưỡng ở Cài đặt) |

---

## DT-19 · Nhận xét cuối khóa của GV

> Là **GV**, tôi muốn **đánh giá từng HV cuối khóa (kỹ năng, thái độ, đề xuất bước tiếp)** để **Sale tư vấn lớp tiếp theo đúng trình độ**.

**Nguồn:** `EvaluationModal.tsx`, `EvaluationCard.tsx` · **Quyền cần:** GV của lớp (cổng GV)

**Các bước:** Mỗi HV: từ vựng, ngữ pháp, đọc, nghe, nói (1–5 sao), thái độ & chuyên cần (1–5), điểm mạnh, điểm yếu, đề xuất* (lên trình độ / nâng cao / JLPT / học lại / khác),
ghi chú cho Sale → lưu (UQ lớp × HV, sửa được tới khi lớp kết thúc + 14 ngày 🆕).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-19.E1 | Điểm ngoài 1–5 | `academic.evaluation.invalid_rating` |
| DT-19.E2 | Hiển thị | **Chỉ nhân viên** (hồ sơ HV, CRM-19); **không** trả về cổng HV / PH |
| DT-19.E3 | Lớp gần kết thúc (≤ 2 buổi) chưa nhận xét | Nhắc GV trên cổng |

---

## DT-20 · Thời khóa biểu & bảng buổi học trong ngày

> Là **giáo vụ**, tôi muốn **một bảng mọi buổi hôm nay của chi nhánh (sắp diễn ra / đang học / chờ xác nhận / có sự cố)** để **điều phối lớp, phòng, GV trong ngày**.

**Nguồn:** Đào tạo › Buổi học (`SessionsPage.tsx`, `SessionBoards.tsx`), `SessionTimetablePage.tsx`; cổng GV / HV / PH "Thời khóa biểu"
· **Quyền cần:** `academic.sessions.view` (nhân viên) · chủ tài khoản (cổng)

**Các bước:** Bảng theo trạng thái chạy (sắp tới · đang diễn ra · chờ xác nhận · đã xác nhận · đã hủy), lịch tuần theo phòng / GV; mở buổi → ngăn chi tiết
(điểm danh, chấm công GV, báo cáo, sự cố, nhật ký hỗ trợ: điểm danh hộ, HV về sớm, phòng, khác).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DT-20.E1 | Cổng GV | Buổi mình dạy (gồm dạy thay), không hiện học phí |
| DT-20.E2 | Cổng PH | Lịch của tất cả các con, phân biệt theo màu / tên |
| DT-20.E3 | Giờ hiển thị | Lưu `date` + `TimeOnly` theo giờ Việt Nam của chi nhánh (giờ học là giờ địa phương, không đổi UTC) |

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/academic/...`) | Quyền |
|---|---|---|
| DT-01 | `GET classes?branchId=&courseId=&teacherId=&status=&endingSoon=&underMin=&page=` | `academic.classes.view` |
| DT-02, 07 | `POST classes` · `PUT classes/{id}` `{ …, applyToUpcoming, version }` · `POST classes/check` (trùng lịch) | `.create` / `.update` |
| DT-03 → 06 | `POST classes/{id}/start` `{ actualStartDate, reschedule }` · `…/finish` `{ actualEndDate }` · `…/cancel` `{ reason, confirmCode }` · `…/pause` `{ reason }` · `…/resume` | `academic.classes.update` |
| DT-08 | `POST classes/{id}/sessions/preview` · `POST classes/{id}/sessions/generate` | `academic.sessions.create` |
| DT-09 | `GET class-sessions/{id}/attendance` · `PUT class-sessions/{id}/attendance` (cả danh sách) · `GET class-sessions/{id}/qr` · cổng HV `POST portal/attendance/scan` `{ payload \| code }` | `.update` / GV / HV |
| DT-10, 11 | `PUT class-sessions/{id}` `{ …, reason }` · `POST class-sessions` (học bù / học thêm) · `POST class-sessions/check` · `POST class-sessions/{id}/cancel` | `academic.sessions.*` |
| DT-12 | `POST class-sessions/{id}/teacher-checkin` · `…/teacher-checkout` · `PUT class-sessions/{id}/teacher-attendance` (chấm hộ) | GV / `.update` |
| DT-13 | `POST class-sessions/{id}/incident` · `…/incident/outcome` | `academic.sessions.update` |
| DT-14 | `PUT class-sessions/{id}/report` | GV của buổi |
| DT-15 | `POST class-sessions/{id}/confirm` · `…/unconfirm` · `POST class-sessions/confirm` `{ ids }` | `academic.sessions.approve` |
| DT-16 | cổng GV `POST portal/cancel-requests` · `DELETE …/{id}` · `POST requests/cancel/{id}/approve|reject` · `POST substitute-invites` · cổng GV `POST portal/substitute-invites/{id}/accept|decline` | GV / `academic.requests.approve` |
| DT-17 | `GET classes/{id}/promotions` · `POST promotions` · `PUT promotions/{id}` (lớp đích, giữ chỗ) · `POST classes/{id}/promotions/hand-over` | `academic.classes.update` |
| DT-18 | cổng `POST portal/leave-requests` · `GET portal/leave-requests/makeup-options?sessionId=` · `POST requests/leave/{id}/approve|reject` | HV / PH / `academic.requests.approve` |
| DT-19 | cổng GV `PUT portal/classes/{id}/evaluations/{studentId}` | GV của lớp |
| DT-20 | `GET class-sessions?date=&branchId=&state=` · `GET timetable?from=&to=&roomId=&teacherId=` · cổng `GET portal/timetable` | `.view` / chủ tài khoản |

- JSON khớp `frontend/src/features/academic/classes/types.ts` (`ClassInfo`, `ClassSession`, `AttendanceRecord`, `StudentEvaluation`).
- **Integration event (Academic.Contracts)** 🆕: `ClassOpened`, `ClassStarted`, `ClassFinished`, `ClassCancelled`, `SessionCancelled`, `SessionRescheduled`,
  `SessionConfirmed` / `SessionUnconfirmed` (Finance lương GV), `AttendanceRecorded` (Notifications báo PH, Students cảnh báo bỏ học), `SubstituteAccepted`.
- **Interface cho module khác** 🆕: `IClassLookup` (mã, chi nhánh, học phí, trạng thái, sĩ số max — Students / Finance / Crm), `ISessionSchedule`
  (buổi trong khoảng giờ theo phòng / GV — Crm kiểm trùng lịch hẹn), `IAttendanceStats` (chuyên cần theo HV × lớp — Students HV-14, HV-15).
- **Nghe event:** `StudentEnrolled` / `EnrollmentDropped` / `EnrollmentTransferred` (Students) → cập nhật danh sách điểm danh; `EmployeeQuit` (Hr) → báo lớp / buổi
  tương lai còn gán GV đó.
- **Job:** hết hạn lời mời dạy thay (mỗi 5 phút) · hết hạn giữ chỗ lên lớp (hằng ngày) · nhắc GV chưa check-in 10 phút sau giờ học 🆕.
- **Mã lỗi cần thêm vào Domain** (chưa có): `academic.class.duplicate_code`, `academic.class.schedule_conflict`, `academic.class.invalid_teacher`,
  `academic.room.unavailable`, `academic.course.not_available`, `academic.attendance.invalid_code`, `academic.attendance.not_in_roster`,
  `academic.attendance.qr_closed`, `academic.session.already_made_up`, `academic.session.already_started`, `academic.session.not_finished`,
  `academic.teacher_attendance.not_checked_in`, `academic.cancel_request.duplicate`, `academic.leave_request.too_late`, `academic.leave_request.duplicate`.

## Đã làm — đợt 1 (backend, 2026-11-20, giai đoạn 13 phase 3)

Code: `src/Modules/Academic/JPCenter.Modules.Academic/Application/{Rooms,Classes,ClassSessions}/` · Test: `tests/JPCenter.IntegrationTests/Academic/ClassFlowTests.cs`.

| Story | API (`/api/v1/academic/...`) | Quyền |
|---|---|---|
| Phòng | `GET rooms?branchId=` · `GET rooms/options?branchId=` · `POST rooms` · `PUT rooms/{id}` `{ …, rowVersion }` · `POST rooms/{id}/status` `{ status, reason?, until? }` | `academic.rooms.*` · options: nhân viên đã đăng nhập |
| Báo hỏng | `GET rooms/issues?roomId=` · `POST rooms/issues` (ai xem được phòng) · `POST rooms/issues/{id}/start|resolve|reopen` | `.view` / `.update` |
| DT-01 | `GET classes?branchId=&courseId=&teacherId=&status=` · `GET classes/{id}` | `academic.classes.view` |
| DT-02, 07 | `POST classes/check` (số buổi, ngày kết thúc dự kiến, lớp trùng) · `POST classes` `{ …, generateSessions = true }` · `PUT classes/{id}` `{ …, applyToUpcoming, rowVersion }` → `{ class, warnings }` | `.view` / `.create` / `.update` |
| DT-03 → 06 | `POST classes/{id}/start` `{ actualStartDate, reschedule }` · `…/pause` `{ reason }` · `…/resume` · `…/finish` `{ actualEndDate }` · `…/cancel` `{ reason, confirmCode }` | `.update` |
| DT-08 | `GET classes/{id}/sessions` · `POST classes/{id}/sessions/preview` · `POST classes/{id}/sessions/generate` `{ from?, weekdays?, startTime?, endTime?, count?, skipHolidays }` | `.view` / `academic.sessions.create` |
| DT-10, 11 | `POST class-sessions/check` · `POST class-sessions` (học bù / học thêm) · `PUT class-sessions/{id}` `{ …, reason, rowVersion }` · `POST class-sessions/{id}/cancel` | `academic.sessions.*` |
| DT-13 | `POST class-sessions/{id}/incident` · `…/incident/outcome` · `POST class-sessions/{id}/support` (nhật ký hỗ trợ) | `.update` |
| DT-15 | `POST class-sessions/{id}/confirm` · `…/unconfirm` · `POST class-sessions/confirm` `{ ids }` → `{ confirmed, failed[] }` | `.approve` |
| DT-20 | `GET class-sessions?from=&to=&branchId=&classId=&teacherId=&roomId=` (bắt buộc lớp, hoặc khoảng ≤ 3 tháng) | `.view` |

**Chốt khi code:**
- Ngày lễ lấy từ Cài đặt chung (`settings.academic.holidays`, qua `IHolidayCalendar`) — DT-08.E5.
- Sinh lịch: thay buổi thường chưa diễn ra (chưa điểm danh, chưa xác nhận); số buổi mặc định = số buổi khóa − buổi còn giữ (không tính buổi hủy, học thêm);
  đánh số tiếp từ số lớn nhất còn giữ. Mở lớp mặc định sinh lịch ngay.
- Sửa lớp: GV / phòng của lớp luôn áp cho buổi không ghi đè (buổi chỉ lưu phần ghi đè); `applyToUpcoming` chỉ quyết định đổi **giờ** cho buổi chưa dạy.
  Đổi thứ học → cảnh báo dùng "Tạo lịch tự động".
- Học phí lớp chỉ trả khi người xem mở / sửa lớp được hoặc có `finance.invoices.view` (DT-01.E2, nới thêm cho giáo vụ vì phải nhập học phí khi mở lớp).
- Phạm vi `own` = lớp / buổi mình là GV, trợ giảng, dạy thay hoặc có chấm công (chưa tính "người tạo").
- Integration event (`Academic.Contracts`): `ClassOpened`, `ClassStarted`, `ClassFinished`, `ClassCancelled`, `SessionCancelled`, `SessionRescheduled`,
  `SessionConfirmed`, `SessionUnconfirmed` — chưa module nào nghe.
- Lớp Việt + Nhật: mới kiểm đủ 2 GV, **chưa** kiểm 1 Việt + 1 Nhật (Hr.Contracts chưa trả quốc tịch).
- ✅ 2026-11-20 (giai đoạn 14): sĩ số thật qua `Students.Contracts.IEnrollmentLookup` — `enrolled`, khai giảng chặn khi 0 HV (DT-03.E3), DT-07.E3;
  lớp kết thúc / bị hủy → Students hoàn thành / cho nghỉ ghi danh (nghe `ClassFinished` / `ClassCancelled`). Academic cung cấp `IClassLookup`.
- ⏳ Đợt 2 (giai đoạn 15): DT-09 điểm danh, DT-12 chấm công GV, DT-14 báo cáo buổi, DT-16 đơn hủy buổi / dạy thay, DT-17 lên lớp, DT-18 đơn xin nghỉ,
  DT-19 nhận xét cuối khóa, cổng GV / HV / PH, `IClassLookup` / `ISessionSchedule` / `IAttendanceStats`.
- ⏳ DT-15.E5 (bảng lương đã chốt) làm cùng Tài chính (giai đoạn 17).
- Bổ sung khi nối frontend (giai đoạn 13 phase 4): báo sự cố / ghi kết quả tự chỉnh chấm công GV giống giao diện cũ
  (GV dừng dạy lúc sự cố, người thay vào lớp lúc `takeoverAt`; học tiếp lúc `at` → mở lại giờ ra hoặc thêm người đứng lớp);
  `incident.outcome.at` trả "HH:mm"; buổi học thêm nhận `lessonId`.

### Frontend (giai đoạn 13 phase 4)
- `features/academic/rooms/roomApi.ts`, `features/academic/classes/classApi.ts`: tải lúc đăng nhập (phòng → lớp → buổi ±45 ngày), đổ vào store cũ
  (Id mock nhờ `seedIds`); chi tiết lớp tải đủ buổi của lớp; trang Buổi học tải ±45 ngày quanh tuần đang xem.
- Đã nối: Phòng học (thêm / sửa / bảo trì / ngừng / báo hỏng), mở lớp, sửa lớp, khai giảng / tạm dừng / học lại / kết thúc / hủy lớp, tạo lịch tự động,
  hủy buổi, học bù / học thêm / đổi lịch, sự cố + học tiếp / cho nghỉ, nhật ký hỗ trợ, xác nhận / bỏ xác nhận / xác nhận hàng loạt.
- Buổi học bù chưa xác nhận hiện trạng thái `makeup` như mock; lý do tạm dừng hiện trong ghi chú lớp (không ghi đè ghi chú thật khi lưu).
- Còn dữ liệu giả (ghi trong trình duyệt): điểm danh, chấm công GV tự làm, báo cáo buổi, đơn hủy buổi / dạy thay, đơn xin nghỉ, lên lớp,
  học viên / học phí khi hủy / kết thúc lớp, cổng GV / HV / PH — giai đoạn 14, 15.

## Đã làm — đợt 2 (backend, 2026-11-20, giai đoạn 15 phase 3)

Code: `Application/{ClassSessions,Requests,Promotions,Evaluations,Portals}/` · Test: `tests/JPCenter.IntegrationTests/Academic/PortalFlowTests.cs`.

| Story | API (`/api/v1/academic/...`) | Quyền |
|---|---|---|
| DT-09, 12 (giáo vụ) | `GET class-sessions/{id}/qr` · `PUT class-sessions/{id}/attendance` `{ records[] }` · `PUT class-sessions/{id}/teacher-attendance` | `academic.sessions.update` |
| Cổng GV | `portal/teaching/sessions?from=&to=` · `sessions/{id}` · `…/qr` · `PUT …/attendance` · `POST …/check-in` · `…/check-out` · `PUT …/report` | GV của buổi (portal teacher) |
| DT-16 (GV) | `GET/POST portal/teaching/cancel-requests` · `DELETE …/{id}` · `GET portal/teaching/substitute-invites` · `POST …/{id}/accept` · `…/decline` | GV |
| DT-16 (giáo vụ) | `GET requests/cancel?status=&branchId=` · `POST requests/cancel/{id}/approve|reject` · `GET substitute-invites?sessionId=&status=` · `GET substitute-invites/candidates?sessionId=` · `POST substitute-invites` · `POST substitute-invites/{id}/withdraw` | `academic.requests.view / approve` |
| DT-18 (HV / PH) | `GET portal/learning/sessions?studentId=&from=&to=` · `GET/POST portal/learning/leave-requests` · `GET …/makeup-options?sessionId=` · `PUT …/{id}/makeup` · `DELETE …/{id}` · `POST portal/learning/attendance/scan` `{ payload | code }` (chỉ HV) | HV / PH |
| DT-18 (giáo vụ) | `GET requests/leave?status=&branchId=&classId=` · `POST requests/leave/{id}/approve|reject` → `{ request, warnings }` | `academic.requests.view / approve` |
| DT-17 | `GET/POST classes/{id}/promotions` · `PUT promotions/{id}` `{ toClassId, holdSeat, holdUntil }` · `POST classes/{id}/promotions/hand-over` · cổng `GET portal/learning/promotions` · `POST …/{id}/interest` | `academic.classes.view / update` · HV / PH |
| DT-19 | cổng GV `GET/PUT portal/teaching/classes/{id}/evaluations[/{studentId}]` · nhân viên `GET classes/{id}/evaluations` | GV của lớp · `academic.classes.view` |

**Chốt khi code:**
- Realtime (SignalR `/hubs/realtime`): `substitute-invite.created | accepted | declined | closed | expired`, `cancel-request.decided`, `leave-request.decided`, `attendance.scanned` (GV đang mở QR).
- Job: hết hạn lời mời dạy thay (mỗi 5 phút) · trả chỗ giữ lên lớp quá hạn (00:30). Lớp kết thúc → tự giao Sale đề xuất lên lớp.
- Điểm danh phát `AttendanceRecorded` → Students tính lại nguy cơ bỏ học ngay; cảnh báo "N HV vắng 2 buổi liên tiếp" (E5). Điểm danh khách học bù → ghi kết quả về đơn + buổi gốc "đã học bù".
- Hủy buổi → đơn xin nghỉ đang chờ tự đóng; đơn chọn buổi đó làm buổi bù → bỏ đăng ký bù (DT-11.E4).
- Academic.Contracts: `IAttendanceStats`, event `AttendanceRecorded`, `SubstituteAccepted`. Students.Contracts: `IEnrollmentLookup.OfStudentAsync / NamesAsync`. Crm.Contracts: `IGuardianDirectory`.
- ⏳ PH chưa có con: `IGuardianDirectory` tạm trả rỗng tới khi Crm lưu DB (giai đoạn 16) → cổng PH xem rỗng, gửi đơn 403.
- ⏳ Dữ liệu mẫu: vai trò Giáo vụ (R-GV) chưa có `academic.requests.approve` → chỉ quản lý chi nhánh duyệt được đơn / mời dạy thay.
- ⏳ Thông báo chuông (đơn mới, vấn đề trong báo cáo buổi, HV bấm Quan tâm) chờ module Thông báo; ngưỡng "xin nghỉ nhiều buổi" tạm cố định 3.
