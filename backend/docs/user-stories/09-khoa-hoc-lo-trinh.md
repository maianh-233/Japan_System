# 09 · Khóa học & lộ trình

Module: **Academic** · Bảng: `academic.courses`, `course_curricula`, `curricula`, `lessons`, `chapters`, `materials`, `exam_milestones` ([database.md](../database.md))
· Liên quan: `finance.branch_prices` (giá theo chi nhánh — TC-01), `academic.classes.curriculum_id` (lớp chốt lộ trình — DT-02)
Frontend: `pages/staff/academic/courses/CoursesPage.tsx`, `features/academic/courses/{CourseFormFields,CourseDetailDrawer,CourseCurriculumTab}.tsx`,
`pages/staff/academic/curriculum/CurriculumPage.tsx`, `features/academic/curriculum/{CurriculumFormFields,CurriculumDetailDrawer,CurriculumContent}.tsx`,
`features/academic/{courses,curriculum}/{types,constants,*Store}.ts`, cổng GV `pages/teacher/teaching/TeachingSessionPage.tsx`, cổng HV `pages/student/documents/StudentDocumentsPage.tsx`
· Lưu file: `IFileStorage` ([ADR 0003](../decisions/0003-luu-tru-file.md))

## Mô hình (đọc trước)

```
Course  = SẢN PHẨM BÁN   (vd "JLPT N3 · nhóm · offline · GV Việt")
  ├─ 3 lựa chọn độc lập: format (group | private 1-1) × teacher_type (vn | jp | mixed → tiền tố mã lớp GVV_/GVN_/GVVN_) × mode (offline | online)
  ├─ sessions, minutes_per_session, sessions_per_week, fee (nhóm: trọn khóa / HV · 1-1: mỗi buổi), max_students (chỉ nhóm), branch_ids
  ├─ status: draft ──Mở tuyển sinh──▶ active ⇄ paused
  └─< CourseCurriculum >─ Curriculum      NHIỀU–NHIỀU, mỗi khóa đúng 1 lộ trình MẶC ĐỊNH (cho lớp mở mới)

Curriculum = NỘI DUNG DẠY   (code + version là duy nhất; tier: standard | advanced)
  ├─ Lesson[]  (thứ tự) ─ Chapter[] (thứ tự) ─ Material[]  file (PDF/Word/PPT/Excel/audio → IFileStorage) hoặc link (video, Drive, web)
  ├─ ExamMilestone[]  giữa kỳ / cuối kỳ, chèn SAU 1 lesson — chỉ đánh dấu, đề tạo khi tổ chức thi (ngân hàng câu hỏi)
  └─ status: draft ──Phát hành──▶ published ──Lưu trữ──▶ archived ──Khôi phục──▶ published
             published ──Tạo phiên bản mới──▶ (bản nháp mới, previous_version_id = bản cũ)

Class.curriculum_id  ← chốt lúc mở lớp; KHÔNG đổi theo phiên bản mới
```

**Quy tắc vàng:**
1. Khóa = **cái bán** (giá, hình thức, chi nhánh); lộ trình = **cái dạy** (bài, tài liệu, mốc thi). Một lộ trình dùng chung cho nhiều khóa (N3 offline / N3 online cùng nội dung).
2. **Đã phát hành thì khóa nội dung.** Muốn sửa → tạo phiên bản mới (bản nháp sao chép toàn bộ) → sửa → phát hành. Lớp đang học giữ bản cũ.
3. Mỗi khóa có **đúng 1** lộ trình mặc định khi đã gắn lộ trình; không gỡ / lưu trữ được lộ trình đang là mặc định hoặc đang có lớp học theo.
4. Không xóa khóa / lộ trình (đã có lớp, hóa đơn tham chiếu) — chỉ **ngừng tuyển sinh** / **lưu trữ**. Bản nháp chưa dùng được xóa (xóa mềm).
5. File tài liệu ở **kho riêng tư**; giao diện chỉ nhận **link tạm** sau khi kiểm quyền. Tài liệu "chỉ GV" (giáo án, đáp án) **không bao giờ** trả về cổng HV.
6. Đổi học phí / số buổi của khóa chỉ áp cho **lớp mở mới** (lớp chốt học phí và tổng giờ lúc mở — DT-02, DT-07.E2).

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| KH-01 | Xem danh sách khóa học | Giáo vụ, Sale, quản lý | Cao |
| KH-02 | Thêm khóa học | Giáo vụ, quản lý | Cao |
| KH-03 | Nhân bản khóa học (tạo biến thể) | Giáo vụ | TB |
| KH-04 | Sửa khóa học | Giáo vụ, quản lý | Cao |
| KH-05 | Mở / ngừng tuyển sinh | Quản lý, giáo vụ | Cao |
| KH-06 | Gắn / gỡ lộ trình, đặt mặc định | Giáo vụ | Cao |
| LT-01 | Xem danh sách lộ trình | Giáo vụ, GV | Cao |
| LT-02 | Tạo lộ trình | Giáo vụ | Cao |
| LT-03 | Nhân bản lộ trình (tạo bậc nâng cao) | Giáo vụ | TB |
| LT-04 | Sửa thông tin lộ trình | Giáo vụ | TB |
| LT-05 | Soạn nội dung: bài học, chapter, thứ tự | Giáo vụ | Cao |
| LT-06 | Tải tài liệu lên / thêm link tài liệu | Giáo vụ | Cao |
| LT-07 | Xem / tải tài liệu (nhân viên, GV, HV) | Mọi cổng | Cao |
| LT-08 | Mốc thi giữa kỳ / cuối kỳ | Giáo vụ | TB |
| LT-09 | Phát hành lộ trình | Giáo vụ, quản lý đào tạo | Cao |
| LT-10 | Tạo phiên bản mới | Giáo vụ | Cao |
| LT-11 | Lưu trữ / khôi phục lộ trình | Giáo vụ | Thấp |

**Quyền:** `academic.courses` (`view / create / update / export`) · `academic.curriculum` (`view / create / update / delete / export`; 🆕 `approve` = phát hành).
Module Đào tạo dùng phạm vi **toàn hệ thống** cho khóa và lộ trình (danh mục dùng chung); phạm vi `branch` chỉ lọc khóa **được bán** ở chi nhánh mình khi xem.
GV (cổng GV) đọc lộ trình của lớp mình dạy không cần mã quyền (PQ-11).

---

## KH-01 · Xem danh sách khóa học

> Là **giáo vụ / Sale**, tôi muốn **xem mọi khóa đang bán với giá, hình thức, số lớp đang mở** để **tư vấn và mở lớp đúng khóa**.

**Nguồn:** Đào tạo › Khóa học (`CoursesPage.tsx`) · **Quyền cần:** `academic.courses.view`

**Các bước:**
1. Tab theo hình thức: Tất cả · Lớp nhóm · 1 kèm 1 (có số đếm). Lọc: chi nhánh bán, trình độ (khóa có dạy qua cấp đó: đầu vào ≤ cấp ≤ đầu ra), loại GV, offline / online, trạng thái; tìm mã / tên.
2. Cột: mã, tên, loại khóa, trình độ vào → ra, hình thức, GV, offline / online, số buổi × phút, buổi / tuần, học phí (nhóm "/ khóa", 1-1 "/ buổi"),
   chi nhánh bán, lộ trình mặc định, **lớp đang mở** (mã các lớp `enrolling` / `ongoing`), trạng thái.
3. Bấm → ngăn chi tiết: tab **Thông tin** (KH-04) · tab **Lộ trình** (KH-06).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| KH-01.E1 | Chưa có khóa | "Tạo khóa học trước, sau đó mở lớp theo khóa ở mục Lớp học" |
| KH-01.E2 | Học phí hiển thị | Giá niêm yết; đang chọn 1 chi nhánh có giá riêng (TC-01) → hiện giá chi nhánh + nhãn "Giá CN" |
| KH-01.E3 | Khóa chưa có lộ trình mặc định | Nhãn cảnh báo "Chưa có lộ trình — chưa mở lớp được" |
| KH-01.E4 | Xuất Excel | `academic.courses.export` |
| KH-01.E5 | Sale tư vấn (CRM) | Ô chọn khóa ở Tuyển sinh chỉ hiện khóa `active` bán ở chi nhánh của lead — `GET courses/options` cho người có `crm.leads.view` 🆕 |

---

## KH-02 · Thêm khóa học

> Là **giáo vụ**, tôi muốn **khai báo khóa mới với hình thức, số buổi, học phí, chi nhánh bán** để **Sale bán và giáo vụ mở lớp**.

**Nguồn:** Khóa học › "Thêm khóa học" (`CourseFormFields.tsx`) · **Quyền cần:** `academic.courses.create`

**Các bước:**
1. Nhập: tên*, mã* (chữ hoa không dấu, số, `-`), loại khóa* (JLPT / Kaiwa / Kids / Business / XKLĐ), trình độ vào* → ra*, hình thức* (nhóm / 1-1),
   loại GV* (Việt / Nhật / kết hợp), offline / online*, tổng số buổi*, phút / buổi*, buổi / tuần*, học phí* (nhóm: trọn khóa; 1-1: mỗi buổi — 1-1 hiện "tổng = số buổi × giá"),
   sĩ số tối đa* (chỉ nhóm), chi nhánh mở bán* (≥ 1, chỉ chi nhánh `active`), mô tả.
2. Lưu → `draft` → mở ngay tab **Lộ trình** để gắn (KH-06). Nhật ký "Thêm khóa học".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| KH-02.E1 | Trùng mã (không phân biệt hoa thường) | "Mã khóa đã tồn tại" — 🆕 `academic.course.duplicate_code` |
| KH-02.E2 | Mã sai định dạng | "Chỉ gồm chữ không dấu, số và dấu gạch ngang" (tự viết hoa) |
| KH-02.E3 | Đầu ra thấp hơn đầu vào | `academic.course.invalid_levels` |
| KH-02.E4 | Số buổi / phút / buổi mỗi tuần ≤ 0 | `academic.course.invalid_schedule` |
| KH-02.E5 | Lớp nhóm thiếu sĩ số tối đa | `academic.course.max_students_required` |
| KH-02.E6 | Học phí ≤ 0 | 🆕 `academic.course.invalid_fee` |
| KH-02.E7 | Không chọn chi nhánh / chi nhánh đã đóng | 🆕 `academic.course.branch_required` / `system.branch.inactive` |
| KH-02.E8 | 1-1 có sĩ số | Bỏ qua `max_students` (luôn 1) |

---

## KH-03 · Nhân bản khóa học

> Là **giáo vụ**, tôi muốn **tạo biến thể từ khóa có sẵn (đổi GV, online/offline, 1-1)** để **không nhập lại và dùng chung lộ trình**.

**Quyền cần:** `academic.courses.create`

**Các bước:** "Nhân bản" → form KH-02 điền sẵn từ khóa gốc (mã để trống) → đổi lựa chọn cần thiết → Lưu → khóa mới `draft` + **gắn sẵn mọi lộ trình của khóa gốc, giữ lộ trình mặc định**.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| KH-03.E1 | Lộ trình của khóa gốc đã lưu trữ | Không gắn bản lưu trữ; nếu đó là mặc định → khóa mới chưa có mặc định, cảnh báo |
| KH-03.E2 | Giá theo chi nhánh của khóa gốc | **Không** chép (giá chi nhánh đặt riêng ở TC-01) |

---

## KH-04 · Sửa khóa học

**Nguồn:** ngăn chi tiết › tab Thông tin · **Quyền cần:** `academic.courses.update`

**Các bước:** Form như KH-02 (nút Lưu chỉ bật khi có thay đổi) → lưu → nhật ký trước → sau. Khóa đang có lớp mở → dải cảnh báo
"Khóa đang có lớp mở — đổi học phí / số buổi chỉ áp dụng cho lớp mở và ghi danh mới".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| KH-04.E1 | Đổi mã khi đã có lớp | 🆕 Khóa ô mã — `academic.course.code_locked` (mã khóa in trên báo cáo, hóa đơn) |
| KH-04.E2 | Đổi hình thức nhóm ↔ 1-1, loại GV khi đã có lớp | 🆕 `academic.course.format_locked` — gợi ý **nhân bản** (KH-03) thay vì sửa |
| KH-04.E3 | Bỏ chi nhánh đang có lớp mở của khóa | Cho; lớp đang mở học tiếp, chỉ chặn mở lớp mới ở chi nhánh đó |
| KH-04.E4 | Sửa đè | `xmin` → "Dữ liệu vừa được người khác sửa" |
| KH-04.E5 | Đổi học phí | Không sửa lớp / hóa đơn đã có; phát `CoursePriceChanged` 🆕 (Finance hiện giá mới ở bảng giá) |

---

## KH-05 · Mở / ngừng tuyển sinh

> Là **quản lý**, tôi muốn **mở bán hoặc tạm ngừng 1 khóa** để **Sale chỉ tư vấn khóa đang tuyển**.

**Quyền cần:** `academic.courses.update`

**Các bước:**
- **Mở tuyển sinh** (`draft` / `paused` → `active`) — không cần xác nhận.
- **Ngừng tuyển sinh** (`active` → `paused`) — hộp nêu: không mở lớp mới, không ghi danh mới cho khóa này; Sale không thấy khóa khi tư vấn;
  N lớp đang mở (liệt kê mã) vẫn học bình thường đến hết khóa. "Có thể mở lại bất cứ lúc nào."

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| KH-05.E1 | Mở tuyển sinh khi chưa có lộ trình mặc định đã phát hành | 🆕 Chặn `academic.course.no_default_curriculum` — lớp mở theo khóa cần lộ trình |
| KH-05.E2 | Ghi danh vào lớp đang mở của khóa `paused` | **Cho** — chỉ chặn ghi danh vào lớp **mới** mở sau khi ngừng 🆕 (lớp đã mở vẫn lấp chỗ trống được) |
| KH-05.E3 | Mở lớp cho khóa `paused` / `draft` | `academic.course.not_available` (DT-02.E10) |
| KH-05.E4 | Ưu đãi / chiến dịch đang gắn khóa | Không tự tắt; trang Ưu đãi / Chiến dịch hiện nhãn "Khóa đã ngừng" |

---

## KH-06 · Gắn / gỡ lộ trình, đặt mặc định

> Là **giáo vụ**, tôi muốn **gắn lộ trình chuẩn và nâng cao cho khóa, chọn 1 bản mặc định** để **lớp mở mới tự lấy đúng nội dung**.

**Nguồn:** ngăn chi tiết khóa › tab **Lộ trình** (`CourseCurriculumTab.tsx`) · **Quyền cần:** `academic.courses.update`

**Các bước:**
1. Danh sách lộ trình đang gắn: tên, phiên bản, bậc (chuẩn / nâng cao + điều kiện đầu vào), trạng thái, số lớp đang học theo, nhãn **Mặc định**.
2. **Gắn lộ trình**: chọn lộ trình* (ưu tiên đúng cấp đầu ra của khóa; chỉ bản `published` hoặc `draft`), tùy chọn "đặt làm mặc định".
   Khóa chưa có lộ trình nào → lộ trình đầu tiên **tự thành mặc định**.
3. **Đặt mặc định**: xác nhận "Lớp mở mới của khóa X sẽ học theo lộ trình này · các lớp đã mở vẫn giữ lộ trình hiện tại" → bỏ mặc định cũ.
4. **Gỡ**: xác nhận "Chỉ gỡ liên kết — nội dung lộ trình và các khóa khác dùng chung không bị ảnh hưởng".
5. Khi mở lớp (DT-02): ô lộ trình liệt kê lộ trình **đã phát hành** gắn với khóa, chọn sẵn mặc định; bậc nâng cao hiện điều kiện đầu vào.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| KH-06.E1 | Gỡ lộ trình đang mặc định | "Đặt lộ trình khác làm mặc định trước khi gỡ" — `academic.course.default_curriculum` |
| KH-06.E2 | Gỡ lộ trình có lớp **đang mở** của khóa học theo | "Lớp X, Y đang học theo lộ trình này — không thể gỡ" 🆕 `academic.course.curriculum_in_use` |
| KH-06.E3 | Gắn lộ trình đã gắn | Bỏ qua (idempotent) |
| KH-06.E4 | Đặt mặc định cho bản nháp / lưu trữ | 🆕 `academic.course.default_not_published` — mặc định phải là bản đã phát hành (bản nháp chỉ gắn để chuẩn bị) |
| KH-06.E5 | Gắn lộ trình đã lưu trữ | 🆕 `academic.curriculum.archived` |
| KH-06.E6 | Cấp độ lộ trình khác cấp đầu ra của khóa | Cảnh báo, vẫn cho (khóa luyện thi nhiều cấp) |

---

## LT-01 · Xem danh sách lộ trình

> Là **giáo vụ**, tôi muốn **xem mọi lộ trình theo cấp độ, bậc, phiên bản và khóa đang dùng** để **biết bản nào đang dạy, bản nào đang soạn**.

**Nguồn:** Đào tạo › Lộ trình (`CurriculumPage.tsx`) · **Quyền cần:** `academic.curriculum.view`

**Các bước:** Tab trạng thái (tất cả / đã phát hành / nháp / lưu trữ, có số đếm). Lọc cấp độ, bậc, thẻ; tìm mã / tên / giáo trình.
Cột: mã + phiên bản, tên, cấp độ, bậc, giáo trình, thẻ (3 thẻ đầu + "+N"), số bài / chapter / tài liệu / mốc thi, khóa đang gắn (nhãn mặc định), số lớp đang học theo, người soạn, cập nhật lúc.
Bấm → ngăn chi tiết: **Nội dung** (LT-05 → LT-08) · **Thông tin** (LT-04) · **Phiên bản** (chuỗi `previous_version_id`) · **Khóa & lớp dùng**.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-01.E1 | Nhiều phiên bản cùng mã | Hiện mỗi bản 1 dòng; bộ lọc "chỉ bản mới nhất" 🆕 |
| LT-01.E2 | GV xem | Cổng GV chỉ thấy lộ trình (đúng phiên bản) của lớp mình dạy, gồm cả tài liệu "chỉ GV" |

---

## LT-02 · Tạo lộ trình

**Nguồn:** Lộ trình › "Tạo lộ trình" (`CurriculumFormFields.tsx`) · **Quyền cần:** `academic.curriculum.create`

**Các bước:** Tên*, mã* (chữ hoa, số, `-`), phiên bản* dạng `v1.0`, cấp độ*, bậc* (chuẩn / nâng cao), **điều kiện đầu vào*** (bắt buộc khi nâng cao —
"để Giáo vụ chọn đúng lộ trình khi mở lớp"), giáo trình, mô tả, thẻ → `draft`, người soạn = người tạo → mở tab Nội dung.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-02.E1 | Thiếu tên | `academic.curriculum.name_required` |
| LT-02.E2 | Mã + phiên bản đã tồn tại | 🆕 `academic.curriculum.duplicate_version` |
| LT-02.E3 | Phiên bản sai dạng | "Dạng v1.0" — **chốt** lưu đúng chuỗi `v1.0` như frontend (database.md ghi "1.0" → sửa theo khi code) |
| LT-02.E4 | Bậc nâng cao thiếu điều kiện | 🆕 `academic.curriculum.entry_requirement_required` |

---

## LT-03 · Nhân bản lộ trình (tạo bậc nâng cao)

**Quyền cần:** `academic.curriculum.create`

**Các bước:** "Nhân bản" từ 1 lộ trình → form điền sẵn. Bản gốc **chuẩn** → gợi ý bậc **nâng cao**, tên "… (nâng cao)", mã `…-ADV`, phiên bản `v1.0`;
bản gốc khác → "(bản sao)", phiên bản kế tiếp. Lưu → bản nháp **sao chép toàn bộ** bài / chapter / tài liệu / mốc thi (Id mới).
Bản nâng cao **tự gắn vào các khóa của bản gốc** (không đặt mặc định) → thông báo "Phát hành xong, Giáo vụ chọn được khi mở lớp có đầu vào tốt".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-03.E1 | Tài liệu file khi sao chép | **Dùng chung** `file_key` (không chép file trong kho — tiết kiệm dung lượng); xóa file chỉ khi không còn tài liệu nào trỏ tới (LT-06.E8) |
| LT-03.E2 | Trùng mã + phiên bản | Như LT-02.E2 |

---

## LT-04 · Sửa thông tin lộ trình

**Nguồn:** ngăn chi tiết › Thông tin · **Quyền cần:** `academic.curriculum.update`

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-04.E1 | Bản đã phát hành / lưu trữ | Chỉ sửa **mô tả, thẻ** (không ảnh hưởng nội dung dạy); tên, mã, cấp độ, bậc, phiên bản khóa — `academic.curriculum.not_draft` |
| LT-04.E2 | Sửa đè | `xmin` |

---

## LT-05 · Soạn nội dung: bài học, chapter, thứ tự

> Là **giáo vụ**, tôi muốn **dựng khung bài học → phần học** để **GV mở buổi là thấy đúng phần cần dạy và tài liệu kèm theo**.

**Nguồn:** tab Nội dung (`CurriculumContent.tsx`) · **Quyền cần:** `academic.curriculum.update` · **Chỉ bản nháp**

**Các bước:**
1. **Bài học**: thêm (tên*, "có bài kiểm tra nhỏ cuối bài"), sửa, xóa, lên / xuống (đánh số tự động "Bài 1, 2…").
2. **Chapter** trong bài: thêm (tên*), sửa, xóa, đổi thứ tự.
3. Xóa bài: hộp nêu "N chapter và M tài liệu sẽ bị xóa"; mốc thi gắn sau bài đó **chuyển sang sau bài liền trước** (không có thì bài liền sau).
4. Xóa chapter: "N tài liệu trong chapter cũng bị xóa".
5. Tổng hợp đầu tab: số bài · chapter · tài liệu · mốc thi · số chapter **chưa có tài liệu**.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-05.E1 | Sửa nội dung bản đã phát hành | `academic.curriculum.not_draft` ("Lộ trình đã phát hành — tạo phiên bản mới để sửa") |
| LT-05.E2 | Bài / chapter không tồn tại | `academic.curriculum.lesson_not_found` / `academic.curriculum.chapter_not_found` |
| LT-05.E3 | Thiếu tên | Lỗi theo trường |
| LT-05.E4 | Đổi thứ tự | 1 request gửi cả danh sách Id theo thứ tự mới (bulk = 1 request) |
| LT-05.E5 | Xóa bài là bài duy nhất có mốc thi phía sau | Mốc thi bị xóa theo nếu không còn bài nào → cảnh báo trong hộp xác nhận 🆕 |
| LT-05.E6 | Câu hỏi ngân hàng gắn bài bị xóa | Không ảnh hưởng ngân hàng (câu hỏi giữ `lesson_ids` cũ, chỉ không bốc được theo bài đó nữa) |

---

## LT-06 · Tải tài liệu lên / thêm link tài liệu

> Là **giáo vụ**, tôi muốn **đính kèm PDF, slide, file nghe, hoặc link video / Drive vào từng chapter, đánh dấu cái nào HV được xem** để **GV và HV có đúng tài liệu theo bài**.

**Nguồn:** `CurriculumContent.tsx › MaterialModal`, `components/common/FileUpload` · **Quyền cần:** `academic.curriculum.update` · **Chỉ bản nháp**

**Các bước:**
1. Chọn loại: **file** (PDF, Word, PowerPoint, Excel/CSV, Audio mp3/wav/m4a) hoặc **link** (Video, Google Drive, Liên kết khác).
2. **File**: chọn ≥ 1 tệp (≤ **50 MB** / tệp) → mỗi tệp **1 request** `multipart/form-data` → API gọi
   `IFileStorage.UploadAsync(stream, tên, "academic/materials", FilePolicy.TeachingMaterials)` → lưu tài liệu với `file_key`, tên gốc, cỡ, loại.
   Tên hiển thị mặc định = tên tệp (bỏ đuôi), sửa được.
3. **Link**: tên hiển thị*, đường dẫn* `https://` (Drive phải là `drive.google.com` / `docs.google.com` — nhắc bật "Bất kỳ ai có đường liên kết đều xem được").
4. Cờ **"Học viên xem được"** (mặc định bật; tắt = chỉ GV: giáo án, đáp án). Sửa tên / cờ; xóa tài liệu.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-06.E1 | Tệp > 50 MB | `file.too_large` ("Tệp vượt quá 50 MB") — frontend chặn trước khi gửi |
| LT-06.E2 | Đuôi không cho phép (mp4, exe, zip…) | `file.type_not_allowed` kèm danh sách đuôi được nhận; **video → dán link YouTube (không công khai) / Drive** |
| LT-06.E3 | Đổi đuôi để lách (exe → pdf) | `file.content_mismatch` (kiểm magic bytes) |
| LT-06.E4 | Link sai / không phải Drive khi chọn loại Drive | Lỗi theo trường |
| LT-06.E5 | Tải lên thành công nhưng lưu tài liệu lỗi (vd lộ trình vừa bị phát hành) | Xóa file vừa lưu khỏi kho (dọn rác ngay trong handler) |
| LT-06.E6 | Bản đã phát hành | `academic.curriculum.not_draft` |
| LT-06.E7 | Tải nhiều tệp cùng lúc | Mỗi tệp 1 request (hiện tiến độ từng tệp), lỗi tệp nào báo tệp đó |
| LT-06.E8 | Xóa tài liệu file | Xóa mềm bản ghi; **file trong kho chỉ xóa** khi dọn Thùng rác **và** không còn tài liệu nào (bản sao / phiên bản khác) dùng chung `file_key` |
| LT-06.E9 | Chưa cấu hình kho file | Lỗi hệ thống 500 "Chưa cấu hình nơi lưu file" (cấu hình `Storage__*` — ADR 0003) |
| LT-06.E10 | Hết dung lượng miễn phí 🆕 | Trang Hệ thống hiện tổng dung lượng đã dùng (Σ `size`) — cảnh báo khi > 8 GB / 10 GB của R2 |

---

## LT-07 · Xem / tải tài liệu

> Là **GV / HV**, tôi muốn **mở tài liệu của bài đang học ngay trên trình duyệt** để **không phải xin file qua Zalo**.

**Nguồn:** nhân viên: tab Nội dung · GV: `TeachingSessionPage.tsx` (mở buổi → chapter + tài liệu) · HV: `StudentDocumentsPage.tsx` (Tài liệu → của lớp)
· **Quyền:** nhân viên `academic.curriculum.view` · GV dạy lớp dùng lộ trình đó · HV đang học (`studying`) lớp dùng lộ trình đó

**Các bước:**
1. Danh sách tài liệu **không** chứa link file. Bấm "Xem" / "Tải" → `GET …/materials/{id}/link` → server kiểm quyền → trả **link tạm 15 phút**
   (`IFileStorage.GetDownloadUrlAsync(key, new FileLinkOptions(DownloadName: tên gốc, Inline: xem ? true : false))`).
2. Trình duyệt mở link thẳng từ kho (PDF / audio mở xem; Word / PPT / Excel tải về). Tài liệu dạng link → trả đường dẫn gốc.
3. Ghi lượt xem 🆕 (tài liệu nào, ai, lúc nào) — dùng cho "HV nào đã xem" sau này.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-07.E1 | HV xin link tài liệu "chỉ GV" | 404 (không tiết lộ là có tồn tại) — `academic.material.not_found` 🆕 |
| LT-07.E2 | HV không học lớp nào dùng lộ trình đó / đã nghỉ | 403 |
| LT-07.E3 | Link hết hạn | Kho trả 403 — giao diện xin link mới (bấm lại) |
| LT-07.E4 | Phụ huynh | Không có mục tài liệu ở cổng PH |
| LT-07.E5 | Lộ trình cũ (lớp học bản v1.0, đã có v1.1) | HV / GV thấy đúng phiên bản **của lớp** |
| LT-07.E6 | HV bảo lưu | Vẫn xem được tài liệu (để tự học) 🆕 |

---

## LT-08 · Mốc thi giữa kỳ / cuối kỳ

**Nguồn:** tab Nội dung › "Thêm mốc thi" · **Quyền cần:** `academic.curriculum.update` · **Chỉ bản nháp**

**Các bước:** Loại* (giữa kỳ / cuối kỳ), tên*, thi sau bài*, ghi chú cho người tạo kỳ thi → mốc hiện giữa danh sách bài, ngay sau bài đã chọn.
Phạm vi kiến thức gợi ý: giữa kỳ = từ đầu (hoặc sau mốc trước) đến bài hiện tại; cuối kỳ = toàn khóa. Xóa mốc: "Chỉ xóa mốc thi khỏi lộ trình. Các kỳ thi đã tổ chức không bị ảnh hưởng".
Sinh lịch buổi học (DT-08) chèn **buổi thi** ngay sau bài của mốc.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-08.E1 | Lộ trình chưa có bài | Nút "Thêm mốc thi" khóa; API `academic.curriculum.lesson_not_found` |
| LT-08.E2 | Hai mốc sau cùng 1 bài | Cho (vd thi viết + thi nói) |
| LT-08.E3 | Hơn 1 mốc cuối kỳ 🆕 | Cảnh báo, vẫn cho |

---

## LT-09 · Phát hành lộ trình

> Là **giáo vụ**, tôi muốn **chốt bản nháp thành bản chính thức** để **giáo vụ chọn được khi mở lớp và nội dung không bị sửa giữa chừng**.

**Quyền cần:** `academic.curriculum.update` (🆕 đề xuất `academic.curriculum.approve`)

**Các bước:** "Phát hành" → hộp nêu: N bài · M chapter · K mốc thi · T tài liệu; "X chapter chưa có tài liệu" (cảnh báo) hoặc "Mọi chapter đều đã có tài liệu";
"Giáo vụ chọn được lộ trình này khi mở lớp" → `published` → **khóa nội dung** → phát `CurriculumPublished` 🆕.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-09.E1 | Không ở trạng thái nháp | `academic.curriculum.not_draft` |
| LT-09.E2 | Chưa có bài nào | `academic.curriculum.empty` |
| LT-09.E3 | Chapter trống | Chỉ cảnh báo |
| LT-09.E4 | Bài không có chapter nào 🆕 | Cảnh báo "Bài N chưa có chapter" — vẫn cho |
| LT-09.E5 | Đây là phiên bản mới của 1 bản đang là mặc định | Hỏi thêm "Đặt bản mới làm mặc định cho N khóa?" → có thì đổi mặc định (lớp cũ giữ bản cũ) |

---

## LT-10 · Tạo phiên bản mới

> Là **giáo vụ**, tôi muốn **sửa lộ trình đang dạy mà không làm xáo trộn lớp đang học** để **cải tiến nội dung cho các lớp sau**.

**Quyền cần:** `academic.curriculum.create`

**Các bước:** Bản `published` → "Tạo phiên bản mới" → xác nhận "Sao chép toàn bộ bài, chapter, tài liệu, mốc thi sang bản nháp. Bản hiện tại giữ nguyên cho các lớp đang học.
Bản nháp vX.Y+1 được gắn vào N khóa đang dùng (không đặt mặc định)" → bản nháp `previous_version_id` = bản cũ, phiên bản tăng số phụ (`v1.0 → v1.1`) → mở bản nháp.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-10.E1 | Đã có bản nháp mới hơn từ bản này 🆕 | `academic.curriculum.draft_exists` — mở bản nháp đó thay vì tạo thêm |
| LT-10.E2 | Tạo từ bản nháp | Không cho — `academic.curriculum.not_published` 🆕 |
| LT-10.E3 | Tài liệu file | Dùng chung `file_key` như LT-03.E1 |

---

## LT-11 · Lưu trữ / khôi phục lộ trình

**Quyền cần:** `academic.curriculum.update`

**Các bước:** "Lưu trữ" → hộp nêu: không chọn được khi mở lớp mới; N lớp đang học theo (liệt kê) vẫn dùng bình thường → `archived`. "Khôi phục" → `published`.
Bản **nháp** chưa từng phát hành, không lớp nào dùng → được **xóa** (Thùng rác) 🆕.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| LT-11.E1 | Đang là mặc định của khóa | "Lộ trình đang là mặc định của N khóa — đặt lộ trình khác làm mặc định ở trang Khóa học trước" 🆕 `academic.curriculum.is_default` |
| LT-11.E2 | Lưu trữ bản nháp | Không cho — xóa bản nháp thay vì lưu trữ |
| LT-11.E3 | Xóa bản nháp đã có lớp / đã gắn mặc định | 🆕 `academic.curriculum.in_use` |

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/academic/...`) | Quyền |
|---|---|---|
| KH-01 | `GET courses?format=&branchId=&level=&teacherType=&mode=&status=&q=&page=` · `GET courses/options?branchId=` (khóa `active` cho ô chọn) | `academic.courses.view` (options: đăng nhập nhân viên) |
| KH-02, 03 | `POST courses` `{ …, cloneFromId? }` | `academic.courses.create` |
| KH-04 | `PUT courses/{id}` `{ …, version }` | `academic.courses.update` |
| KH-05 | `POST courses/{id}/activate` · `…/pause` | `academic.courses.update` |
| KH-06 | `GET courses/{id}/curricula` · `POST courses/{id}/curricula` `{ curriculumId, isDefault }` · `PUT courses/{id}/curricula/{curriculumId}/default` · `DELETE courses/{id}/curricula/{curriculumId}` | `academic.courses.update` |
| LT-01 | `GET curricula?status=&level=&tier=&tag=&q=&latestOnly=&page=` · `GET curricula/{id}` (kèm `lessons[].chapters[].materials[]`, `exams[]`, `courses[]`, `classCount`) | `academic.curriculum.view` |
| LT-02, 03 | `POST curricula` `{ …, cloneFromId? }` | `academic.curriculum.create` |
| LT-04 | `PUT curricula/{id}` `{ …, version }` | `.update` |
| LT-05 | `POST curricula/{id}/lessons` · `PUT …/lessons/{lessonId}` · `DELETE …/lessons/{lessonId}` · `PUT curricula/{id}/lessons/order` `{ ids }` · tương tự `…/lessons/{lessonId}/chapters[/order]` | `.update` |
| LT-06 | `POST curricula/{id}/chapters/{chapterId}/materials/file` (multipart: `file`, `name?`, `sharedWithStudents`) · `POST …/materials/link` `{ name, type, url, sharedWithStudents }` · `PUT …/materials/{materialId}` · `DELETE …/materials/{materialId}` | `.update` |
| LT-07 | `GET curricula/{id}/materials/{materialId}/link?download=` · cổng GV `GET portal/classes/{classId}/materials/{materialId}/link` · cổng HV `GET portal/materials/{materialId}/link` | `.view` / GV / HV |
| LT-08 | `POST curricula/{id}/exams` · `PUT …/exams/{examId}` · `DELETE …/exams/{examId}` | `.update` |
| LT-09 → 11 | `POST curricula/{id}/publish` `{ setDefaultFor?: courseIds }` · `…/new-version` · `…/archive` · `…/restore` · `DELETE curricula/{id}` (chỉ bản nháp) | `.update` / `.create` / `.delete` |

- JSON khớp `frontend/src/features/academic/courses/types.ts` (`Course`, `CourseCurriculum`) và `curriculum/types.ts` (`Curriculum`, `Lesson`, `Chapter`, `Material`, `Exam`).
  `Material.url`: tài liệu link = đường dẫn gốc; tài liệu file = **không trả** (frontend gọi LT-07 lấy link tạm) — 🆕 thêm `fileName`, `contentType`, `isFile`.
- Endpoint upload: giới hạn body riêng **51 MB** (chung 1 MB cho JSON — request-pipeline.md), chỉ nhận `multipart/form-data`, rate limit riêng.
- **Integration event (Academic.Contracts)** 🆕: `CourseActivated`, `CoursePaused`, `CoursePriceChanged` (Crm, Finance) · `CurriculumPublished`.
- **Interface cho module khác** 🆕: `ICourseCatalog` (khóa đang bán theo chi nhánh, học phí niêm yết, loại GV — Crm CRM-02 / CRM-07, Finance TC-01 / TC-03).
- **Lưu file:** `IFileStorage` + `FilePolicy.TeachingMaterials`, thư mục `academic/materials` (ADR 0003).

## Dữ liệu cần bổ sung (sửa database.md khi code)

| Nhu cầu | Đề xuất | Story |
|---|---|---|
| Tách file với link | `materials` + `file_key` text? (key trong kho, null = link), `file_name` text?, `content_type` text?; `url` chỉ còn cho link | LT-06, LT-07 |
| Dùng chung file giữa bản sao / phiên bản | Không thêm bảng — đếm `materials` cùng `file_key` trước khi xóa file | LT-03.E1, LT-06.E8 |
| Thứ tự chapter / bài | `lessons.order`, `chapters.order` (đã có) | LT-05 |
| Trùng mã khóa / phiên bản lộ trình | UQ `courses.code` (đã có) · UQ (`curricula.code`, `version`) (đã ghi) | KH-02.E1, LT-02.E2 |
| Phiên bản dạng `v1.0` | Sửa giải thích `curricula.version` theo frontend | LT-02.E3 |
| Lượt xem tài liệu | 🆕 `material_views` (material_id, viewer_id, portal, at) — làm khi cần thống kê | LT-07 |
| Chống sửa đè | `xmin` (quy ước chung) | KH-04.E4, LT-04.E2 |

**Mã lỗi cần thêm vào Domain** (chưa có): `academic.course.duplicate_code`, `academic.course.invalid_fee`, `academic.course.branch_required`,
`academic.course.code_locked`, `academic.course.format_locked`, `academic.course.no_default_curriculum`, `academic.course.curriculum_in_use`,
`academic.course.default_not_published`, `academic.course.not_available`, `academic.curriculum.archived`, `academic.curriculum.duplicate_version`,
`academic.curriculum.entry_requirement_required`, `academic.curriculum.draft_exists`, `academic.curriculum.not_published`, `academic.curriculum.is_default`,
`academic.curriculum.in_use`, `academic.material.not_found`.

## Đã làm (backend, 2026-11-17)

Code: `src/Modules/Academic/JPCenter.Modules.Academic/Application/{Courses,Curricula}/` · Test: `tests/JPCenter.IntegrationTests/Academic/AcademicFlowTests.cs`,
`tests/Modules/Academic/JPCenter.Modules.Academic.Tests/Domain/{CourseTests,CurriculumTests}.cs`.

| Story | API (`/api/v1/academic/...`) | Quyền |
|---|---|---|
| KH-01 | `GET courses` → `CourseDto[]` (lọc phạm vi xem theo chi nhánh bán) · `GET courses/{id}` · `GET courses/options?branchId=` (khóa đang tuyển sinh) | `academic.courses.view` · options: nhân viên đã đăng nhập |
| KH-02, 03 | `POST courses` `{ …, cloneFromId? }` | `.create` |
| KH-04 | `PUT courses/{id}` `{ …, rowVersion }` | `.update` |
| KH-05 | `POST courses/{id}/activate` · `…/pause` | `.update` |
| KH-06 | `POST courses/{id}/curricula` `{ curriculumId, isDefault }` · `PUT courses/{id}/curricula/{cid}/default` · `DELETE courses/{id}/curricula/{cid}` | `.update` |
| LT-01 | `GET curricula` (kèm toàn bộ nội dung) · `GET curricula/{id}` | `academic.curriculum.view` |
| LT-02, 03 | `POST curricula` `{ …, cloneFromId? }` | `.create` |
| LT-04 | `PUT curricula/{id}` `{ …, rowVersion }` | `.update` |
| LT-05 | `POST curricula/{id}/lessons` · `PUT …/lessons/order` `{ ids }` · `PUT/DELETE …/lessons/{lessonId}` · `POST …/lessons/{lessonId}/chapters` · `PUT …/chapters/order` · `PUT/DELETE …/chapters/{chapterId}` | `.update` |
| LT-06 | `POST …/chapters/{chapterId}/materials/file` (multipart `file`, `name?`, `sharedWithStudents?`; ≤ 50 MB) · `POST …/materials/link` · `PUT/DELETE curricula/{id}/materials/{materialId}` | `.update` |
| LT-07 | `GET curricula/{id}/materials/{materialId}/link?download=` → `{ url, expiresAt }` (link tạm 15 phút) | `.view` |
| LT-08 | `POST curricula/{id}/exams` · `PUT/DELETE …/exams/{examId}` | `.update` |
| LT-09 → 11 | `POST curricula/{id}/publish` `{ setDefaultFor? }` · `…/new-version` · `…/archive` · `…/restore` · `DELETE curricula/{id}` (chỉ bản nháp) | `.update` / `.create` / `.delete` |

**Chốt khi code:**
- Mọi thao tác soạn nội dung trả về lộ trình đầy đủ mới nhất (frontend thay nguyên object). JSON bỏ trường `null` (vd `url` của tài liệu file).
- Cấp độ trả đúng dạng frontend (`beginner`, `N5`…); `rowVersion` = chống sửa đè (`version` là phiên bản nội dung `v1.0`).
- LT-06.E8: xóa tài liệu / bài / chapter khỏi bản nháp → file bị xóa khỏi kho **ngay** nếu không còn tài liệu nào (kể cả bản sao, phiên bản khác,
  lộ trình trong Thùng rác) dùng chung key. Xóa bản nháp lộ trình = xóa mềm, nội dung + file giữ nguyên.
- Endpoint tải file đọc form trong handler — kiểm quyền **trước** khi nhận body; giới hạn riêng 51 MB (chung 1 MB).
- KH-03: nhân bản bỏ qua lộ trình đã lưu trữ; nếu bản mặc định bị bỏ thì lộ trình đầu tiên còn lại tự thành mặc định (quy tắc "khóa luôn có 1 mặc định").
- Dữ liệu mẫu: 14 khóa + 14 lộ trình từ mock frontend; tài liệu file trỏ tới 1 file mẫu / đuôi (PDF mở được thật, đuôi khác chỉ đúng phần đầu).
- ⏳ chờ Lớp học (giai đoạn 14): `ClassUsage` đang trả rỗng — KH-04.E1/E2 (khóa mã / hình thức khi có lớp), KH-06.E2, LT-11.E3, cột "lớp đang mở",
  số lớp dùng lộ trình; link tài liệu cho cổng GV / HV (LT-07 phía cổng).

