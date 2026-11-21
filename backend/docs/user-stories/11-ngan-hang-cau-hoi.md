# 11 · Ngân hàng câu hỏi

Module: **Academic** · Bảng: `academic.questions` ([database.md](../database.md) bảng 42)
Frontend: `pages/staff/academic/questions/QuestionsPage.tsx`, `features/academic/questions/{types,constants,questionStore}.ts`,
`features/academic/questions/{QuestionFormFields,QuestionDetailDrawer,QuestionImportDialog,QuestionCard,Furigana}.tsx`, `mocks/data/questions.ts`

## Mô hình (đọc trước)

```
Question  (dùng chung toàn trung tâm — KHÔNG thuộc chi nhánh)
  level N5–N1 · skill vocab | kanji | grammar | reading | listening · format choice (chọn 1/4) | star (sắp xếp ★) · difficulty
  stem (furigana 漢字{かんじ}) · passage (đọc hiểu) · audio + transcript (nghe hiểu) · 4 đáp án · answer · giải thích
  gắn lộ trình + bài học (để bốc theo bài) · thẻ
  status: draft ──Gửi duyệt──▶ review ──Duyệt──▶ approved ──Ngừng dùng──▶ retired ──Mở lại──▶ review
                                  └──Trả lại (lý do)──▶ draft
          Sửa câu approved → về review (nội dung đổi phải kiểm tra lại)
  stats: used (số đề đã bốc) · attempts / correct (lượt làm / đúng) · picks (số lần chọn từng đáp án)
```

**Quy tắc vàng:**
1. **Chỉ câu `approved` được bốc vào đề** (quiz, kỳ thi, test xếp lớp).
2. **Người soạn không tự duyệt** câu của mình (cả duyệt lẻ lẫn hàng loạt).
3. Đáp án đúng (`answer`), giải thích, lời thoại **không** trả về cổng HV / lead trước khi nộp bài — chỉ API nhân viên có.
4. Câu đã được dùng trong đề **không xóa** — chỉ ngừng dùng (giữ thống kê, đề cũ vẫn hiển thị được).

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| NH-01 | Xem danh sách, lọc, thống kê | Tổ chuyên môn, giáo vụ | Cao |
| NH-02 | Thêm câu hỏi (lưu & gửi duyệt) | Người soạn | Cao |
| NH-03 | Sửa câu hỏi | Người soạn | Cao |
| NH-04 | Tải file nghe | Người soạn | Cao |
| NH-05 | Gửi duyệt · duyệt · trả lại (lẻ + hàng loạt) | Người soạn, người duyệt | Cao |
| NH-06 | Ngừng dùng · mở lại | Người duyệt | TB |
| NH-07 | Nhân bản | Người soạn | TB |
| NH-08 | Nhập từ Excel | Người soạn | TB |
| NH-09 | Bốc câu cho đề (module khác dùng) | (hệ thống) | Cao |
| NH-10 | Ghi thống kê lượt làm | (hệ thống) | TB |

**Quyền:** `academic.questions` — `view` · `create` · `update` · `approve` (duyệt / trả lại / ngừng dùng / mở lại) · `export`.
Phạm vi `own` = chỉ câu mình soạn; `branch` / `all` = mọi câu (ngân hàng dùng chung, không lọc chi nhánh).

---

## NH-01 · Xem danh sách, lọc, thống kê

> Là **tổ chuyên môn**, tôi muốn **xem và lọc ngân hàng câu hỏi** để **biết còn thiếu câu ở cấp độ / kỹ năng nào và câu nào quá khó**.

**Nguồn:** `QuestionsPage.tsx` · **Quyền cần:** `academic.questions.view` · **Ưu tiên:** Cao

**Các bước:**
1. Lọc: cấp độ, kỹ năng, dạng câu, độ khó, trạng thái, lộ trình / bài học, thẻ, người soạn, tìm trong mã / đề bài / đoạn văn.
2. Mỗi dòng: mã, cấp độ, kỹ năng, dạng, độ khó, đề bài, trạng thái, người soạn, số đề đã dùng, tỷ lệ đúng.
3. Thẻ đầu trang: số câu đã duyệt / tổng, chờ duyệt, câu "quá khó" (đã duyệt, ≥ 20 lượt làm, tỷ lệ đúng < 40%), câu chưa gắn bài học.

**Trường hợp đặc biệt:**
| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NH-01.E1 | Phạm vi `own` | Chỉ câu mình soạn (cả danh sách lẫn thống kê) |
| NH-01.E2 | Xem chi tiết câu | Trả đủ đáp án, giải thích, lời thoại, lịch sử duyệt, thống kê từng đáp án (picks) |

**Dữ liệu / API:** `GET /api/v1/academic/questions?level=&skill=&format=&difficulty=&status=&curriculumId=&lessonId=&tag=&authorId=&q=` ·
`GET /api/v1/academic/questions/{id}` · `GET /api/v1/academic/questions/stats`

## NH-02 · Thêm câu hỏi

> Là **người soạn**, tôi muốn **thêm câu hỏi đúng chuẩn đề JLPT** để **bổ sung ngân hàng**.

**Quyền cần:** `academic.questions.create`

**Các bước:**
1. Nhập phân loại, đề bài, đoạn văn (đọc hiểu) / file nghe + lời thoại (nghe hiểu), 4 đáp án, chọn đáp án đúng, giải thích, gắn lộ trình + bài học, thẻ.
2. Bấm "Lưu nháp" (`draft`) hoặc "Lưu & gửi duyệt" (`review`).
3. Hệ thống sinh mã `<cấp độ>-<KN>-<số 4 chữ số>` (vd `N3-GR-1024`, KN = 2 chữ đầu kỹ năng), số tăng dần toàn ngân hàng.

**Trường hợp đặc biệt:**
| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NH-02.E1 | Thiếu đề bài / đáp án / đáp án đúng | `validation.failed` theo trường |
| NH-02.E2 | Không đủ đúng 4 đáp án, hoặc 2 đáp án trùng nhau (bỏ khoảng trắng, không phân biệt hoa thường) | `academic.question.options` |
| NH-02.E3 | Dạng `star` mà đề không có ký tự ★ | `academic.question.star_missing` |
| NH-02.E4 | Đọc hiểu không có đoạn văn | `academic.question.passage_required` |
| NH-02.E5 | Nghe hiểu chưa có file nghe khi **gửi duyệt** (lưu nháp được) | `academic.question.audio_required` |
| NH-02.E6 | Lộ trình khác cấp độ câu hỏi, hoặc bài học không thuộc lộ trình | `academic.question.lesson_invalid` |
| NH-02.E7 | Gắn bài học mà không chọn lộ trình | `academic.question.lesson_invalid` |

**Dữ liệu / API:** `POST /api/v1/academic/questions` `{ …, submit }` → câu hỏi đầy đủ

## NH-03 · Sửa câu hỏi

**Quyền cần:** `academic.questions.update` (phạm vi `own`: chỉ câu mình soạn)

**Tiêu chí chấp nhận:**
- [ ] Kiểm tra như NH-02; chống sửa đè (`rowVersion`).
- [ ] Câu `approved` sửa xong → `review` (NH-03.E1). Câu `review` sửa xong vẫn `review`. Câu `draft` vẫn `draft`.
- [ ] Câu `retired` không sửa được (NH-03.E2 `academic.question.retired`) — mở lại trước.
- [ ] Đổi số / thứ tự đáp án → đặt lại thống kê chọn đáp án (`picks`) vì không còn khớp; giữ `used`.

**Dữ liệu / API:** `PUT /api/v1/academic/questions/{id}` `{ …, rowVersion }`

## NH-04 · Tải file nghe

**Tiêu chí chấp nhận:**
- [ ] Chỉ câu kỹ năng `listening`. File mp3 / m4a / wav ≤ 20 MB, lưu qua `IFileStorage` (ADR 0003), thay file cũ thì xóa file cũ.
- [ ] Độ dài (giây) client gửi kèm (trình duyệt đọc được), 1 → 1800.
- [ ] Nghe thử: link tải có hạn (như tài liệu lộ trình), chỉ nhân viên có `academic.questions.view`.

**Dữ liệu / API:** `POST /api/v1/academic/questions/{id}/audio` (multipart `file`, `durationSec`) · `GET /api/v1/academic/questions/{id}/audio-link`

## NH-05 · Gửi duyệt · duyệt · trả lại

> Là **người duyệt** (tổ trưởng chuyên môn), tôi muốn **duyệt hàng loạt câu đã kiểm tra** để **đưa nhanh vào ngân hàng dùng được**.

**Quyền cần:** gửi duyệt `academic.questions.update` · duyệt / trả lại `academic.questions.approve`

**Tiêu chí chấp nhận:**
- [ ] Gửi duyệt: từ `draft` (kiểm tra lại đủ điều kiện như NH-02, kể cả file nghe).
- [ ] Duyệt: từ `review` → `approved`, ghi người duyệt + lúc duyệt. Người soạn không tự duyệt (NH-05.E1 `academic.question.self_review`).
- [ ] Trả lại: từ `review` → `draft`, bắt buộc lý do; người soạn thấy lý do ở chi tiết câu.
- [ ] Hàng loạt (1 request): `{ ids }` → `{ done, skipped[{ id, code, reason }] }` — câu sai trạng thái / của chính mình bị bỏ qua, không làm hỏng cả lô (NH-05.E2).

**Dữ liệu / API:** `POST /api/v1/academic/questions/submit` · `…/approve` · `…/return` `{ ids, note }`

## NH-06 · Ngừng dùng · mở lại

**Quyền cần:** `academic.questions.approve`

- [ ] Ngừng dùng: từ `approved`, bắt buộc lý do (lộ đề, sai sót). Đề đã tạo trước đó vẫn giữ câu.
- [ ] Mở lại: từ `retired` → `review` (phải duyệt lại).
- [ ] Hàng loạt như NH-05: `POST …/retire` `{ ids, note }` · `POST …/reopen` `{ ids }`.

## NH-07 · Nhân bản

- [ ] Tạo câu mới `draft` cùng nội dung (cả file nghe — dùng chung khóa file, không sao chép), mã mới, người soạn = người bấm, thống kê = 0.
- [ ] Quyền `academic.questions.create`.

**Dữ liệu / API:** `POST /api/v1/academic/questions/{id}/duplicate`

## NH-08 · Nhập từ Excel

> Là **người soạn**, tôi muốn **nhập hàng trăm câu từ file Excel** để **không phải gõ từng câu**.

**Tiêu chí chấp nhận:**
- [ ] Frontend đọc file + kiểm tra sơ bộ (như hiện tại), gửi **1 request** các dòng hợp lệ (≤ 500 dòng).
- [ ] Server kiểm tra lại từng dòng như NH-02; tạo các dòng đúng, trả lỗi theo số dòng; chọn trạng thái `draft` hoặc `review`.
- [ ] Câu nghe hiểu nhập từ Excel chưa có file nghe → luôn tạo `draft` (tải file sau rồi gửi duyệt) — báo trong kết quả.

**Dữ liệu / API:** `POST /api/v1/academic/questions/import` `{ rows[], submit }` → `{ created, errors[{ row, messages[] }] }`

## NH-09 · Bốc câu cho đề

> Là **hệ thống** (quiz, kỳ thi — giai đoạn 20; test xếp lớp — giai đoạn 18), tôi muốn **bốc ngẫu nhiên câu đã duyệt theo tiêu chí**.

**Tiêu chí chấp nhận:**
- [ ] `Academic.Contracts.IQuestionPicker.PickAsync(criteria)`: cấp độ, kỹ năng (nhiều), độ khó (hoặc trộn), lộ trình / bài học, số câu, loại trừ câu đã dùng (Id).
- [ ] Chỉ `approved`. Không đủ câu → trả số câu có được + cảnh báo "thiếu N câu" (không lỗi).
- [ ] Bốc xong tăng `used` của các câu (1 lần / đề).
- [ ] Trả nội dung câu **không** gồm đáp án — đáp án lấy riêng khi chấm (`AnswersAsync(ids)`).

## NH-10 · Ghi thống kê lượt làm

- [ ] Khi chấm bài (giai đoạn 20) module chấm gọi `RecordAnswers([{ questionId, picked }])` → tăng lượt làm, lượt đúng, picks.
- [ ] Làm được ngay ở giai đoạn này để test; nối thật cùng bài tập / bài thi.

---

## Phạm vi giai đoạn 17 (chốt 2026-11-20)

- **Làm:** NH-01 → NH-09 (API + nối frontend), NH-10 phần domain + contract. Dữ liệu mẫu từ `mocks/data/questions.ts`.
- **Chưa làm:** đề thi / quiz / test xếp lớp dùng `IQuestionPicker` (giai đoạn 18, 20). Câu hỏi không vào Thùng rác (không xóa, chỉ ngừng dùng).

## Đã làm — backend (giai đoạn 17 phase 3, 2026-11-21)

Code: `src/Modules/Academic/JPCenter.Modules.Academic/Application/Questions/` · `Infrastructure/Services/QuestionPicker.cs` ·
Test: `tests/JPCenter.IntegrationTests/Academic/{QuestionFlowTests,QuestionSeedTests}.cs`, `tests/Modules/Academic/…/Domain/QuestionTests.cs`.

| Story | API (`/api/v1/academic/questions`) | Quyền |
|---|---|---|
| NH-01 | `GET ?level=&skill=&format=&difficulty=&status=&curriculumId=&lessonId=&tag=&authorId=&q=` (trả đủ, không phân trang) · `GET {id}` · `GET stats` | `view` |
| NH-02 | `POST` `{ …nội dung, submit }` → 201 | `create` |
| NH-03 | `PUT {id}` `{ …nội dung, rowVersion }` | `update` |
| NH-04 | `POST {id}/audio` (multipart `file`, `durationSec`) · `GET {id}/audio-link` → `{ url, expiresAt }` | `update` / `view` |
| NH-05, 06 | `POST submit` (`update`) · `approve` · `return` · `retire` · `reopen` (`approve`) `{ ids, note }` → `{ done[], skipped[{ id, code, reason }] }` | |
| NH-07 | `POST {id}/duplicate` → 201 | `create` |
| NH-08 | `POST import` `{ rows[{ row, …nội dung }], submit }` → `{ created[], errors[{ row, messages[] }], draftListening }` (≤ 500 dòng, body ≤ 5 MB) | `create` |
| NH-09, 10 | `Academic.Contracts.IQuestionPicker`: `PickAsync` · `GetAsync` · `AnswersAsync` · `RecordAnswersAsync` | (module khác) |

**Chốt khi code:**
- Phạm vi `own` theo từng hành động (xem / sửa / duyệt): chỉ câu mình soạn; ngoài phạm vi → 404 / bỏ qua trong lô.
- Người thao tác = nhân viên (Hr) của tài khoản. Duyệt hàng loạt lưu 1 lần, ghi 1 dòng nhật ký.
- File nghe: `FilePolicy.Audio` (mp3 / wav / m4a ≤ 20 MB, kiểm nội dung), thư mục `academic/questions`; thay / bỏ file → xóa file cũ nếu không câu nào
  dùng chung (nhân bản dùng chung khóa file); file mẫu `academic/questions/demo` không bao giờ xóa.
- Bốc câu: `random()` của PostgreSQL trong câu đã duyệt, tối đa 200 câu / lần, loại trừ theo Id; tăng `used_count` khi bốc.
