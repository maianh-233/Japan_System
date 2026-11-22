# 12 · Nội dung học: bài tập, kỳ thi, điểm, thông báo lớp, đánh giá GV, flashcard, tài liệu GV

Module: **Academic** (tài liệu GV: **Library**) · Bảng: `academic.assignments`, `submissions`, `class_exams` 🆕, `exam_attempts` 🆕, `exam_results`,
`class_announcements`, `teacher_reviews`, `flash_decks`, `flash_cards`, `card_progress` · `library.teacher_documents`, `teacher_document_views`
([database.md](../database.md))
Frontend: `features/academic/classes/detail/Class{Assignments,Exams,Announcements,TeacherReviews}Tab.tsx`, `pages/staff/academic/classes/ExamResultsPage.tsx`,
cổng GV `pages/teacher/{assignments,documents}`, cổng HV `pages/student/{assignments,exams,flashcards,documents,progress}`, cổng PH `pages/parent/{results,weekly}`,
`features/portal/{quiz.ts,components/QuizRunner.tsx}` · Giai đoạn 20 ([ke-hoach.md](../ke-hoach.md))

## Mô hình (đọc trước)

```
Class ──┬─ Assignment  upload (HV nộp file / chữ, GV chấm)  ·  quiz (bốc câu ĐÃ DUYỆT từ Ngân hàng theo bài / kỹ năng / độ khó, tự chấm)
        │     └─ Submission (1 / HV)  quiz: giữ lần điểm cao nhất, giới hạn số lần  ·  upload: nộp lại trước khi chấm
        ├─ ClassExam 🆕 (kỳ thi) ── gắn MỐC THI của lộ trình (ExamMilestone: giữa / cuối kỳ, phạm vi bài)
        │     ├─ đề: các phần (kỹ năng, số câu, điểm) — câu bốc từ Ngân hàng trong phạm vi bài của mốc, chốt khi phát hành
        │     ├─ online: HV làm trên Cổng trong khung giờ thi → ExamAttempt (đề riêng nếu đảo) → tự chấm từng phần
        │     └─ giấy:  in đề, thi tại lớp → GV / giáo vụ nhập điểm từng phần
        │     └─ ExamResult (1 / HV): điểm từng phần, vắng thi, nhận xét — luôn sửa được (ghi nhật ký)
        ├─ ClassAnnouncement  tới HV / PH / GV, kênh cổng (Zalo / email: giai đoạn 22), hẹn giờ, chỉ HV nợ phí, chỉ HV được chọn
        └─ TeacherReview  HV đánh giá GV (giữa / cuối kỳ), ẩn danh được
Curriculum.Lesson ── FlashDeck (từ vựng / Kanji) ── FlashCard ── CardProgress (HV × thẻ, hộp Leitner 0–5)
Teacher ── TeacherDocument (tự soạn, gửi lớp mình dạy, gắn buổi) ── lượt xem của HV
```

**Quy tắc vàng:**
1. Lộ trình chỉ có **mốc thi** — đề thật nằm ở kỳ thi, bốc từ Ngân hàng câu hỏi (chỉ câu **đã duyệt**). Không chép đề vào lộ trình.
2. **Đáp án không bao giờ gửi về cổng HV trước khi nộp**; tự chấm ở server. Sau khi nộp: quiz bài tập xem đáp án ngay; kỳ thi xem khi giáo vụ công bố kết quả.
3. GV chỉ thao tác lớp mình dạy (GV chính / trợ giảng / dạy thay buổi đó); HV / PH chỉ thấy lớp của mình / của con.
4. Điểm bài tập + kết quả thi là **dấu hiệu thật cho cảnh báo bỏ học** (HV-14: tỷ lệ nộp, điểm trung bình bài tập, số bài thi dưới ngưỡng) — thay số 0 tạm thời.
5. Nhận xét cuối khóa (DT-19) **khác** nhận xét bài tập / bài thi — không gộp.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| ND-01 | Giao bài tập (upload / quiz) | GV, giáo vụ | Cao |
| ND-02 | HV làm & nộp bài | HV | Cao |
| ND-03 | Chấm bài, nhận xét | GV | Cao |
| ND-04 | Tạo kỳ thi từ mốc lộ trình | Giáo vụ | Cao |
| ND-05 | HV làm bài thi online | HV | Cao |
| ND-06 | Nhập / sửa điểm thi, công bố kết quả | GV, giáo vụ | Cao |
| ND-07 | Thông báo cho lớp | Giáo vụ, GV | TB |
| ND-08 | HV đánh giá GV | HV, quản lý | TB |
| ND-09 | Flashcard: soạn bộ thẻ & HV ôn Leitner | Đào tạo, HV | TB |
| ND-10 | Tài liệu GV gửi lớp | GV, HV | TB |
| ND-11 | Điểm & tiến độ ở cổng HV / PH | HV, PH | Cao |

**Quyền:** trang Lớp học `academic.classes` (`view` xem tab · `update` giao bài / tạo kỳ thi / thông báo / nhập điểm) · flashcard `academic.curriculum.update` ·
cổng GV / HV / PH theo **chủ tài khoản** (không dùng mã quyền).

---

## ND-01 · Giao bài tập

**Nguồn:** tab Bài tập của lớp (giáo vụ), Cổng GV › Bài tập · **Quyền:** `academic.classes.update` hoặc GV của lớp

**Các bước:** lớp*, loại*, tiêu đề*, bài học, mô tả, hạn nộp*, thang điểm (mặc định 10).
- **upload:** đề / tài liệu đính kèm (file ≤ 20 MB hoặc link).
- **quiz:** bài lấy câu* (≥ 1 bài, trong lộ trình của lớp), kỹ năng* (≥ 1), độ khó, số câu* (1–100), giới hạn phút, số lần làm (1–10), đảo đề.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-01.E1 | Hạn nộp ≤ lúc giao | `academic.assignment.invalid_due` |
| ND-01.E2 | Thang điểm ≤ 0 hoặc > 100 | `academic.assignment.invalid_max_score` |
| ND-01.E3 | Quiz thiếu bài / kỹ năng, số câu ngoài 1–100, số lần ngoài 1–10 | `academic.assignment.quiz_config` |
| ND-01.E4 | Ngân hàng không đủ câu đã duyệt theo tiêu chí | Cảnh báo "Chỉ có N câu phù hợp" — vẫn giao (bù câu cùng trình độ / thấp hơn 1 bậc như frontend `pickQuiz`) |
| ND-01.E5 | Lớp đã kết thúc / hủy | `academic.class.closed` |
| ND-01.E6 | Sửa bài đã có HV nộp | Chỉ sửa tiêu đề, mô tả, hạn nộp (gia hạn); đổi loại / tiêu chí → `academic.assignment.has_submissions` |
| ND-01.E7 | Xóa bài đã có bài nộp | Xóa mềm (Thùng rác), bài nộp giữ để khôi phục |

## ND-02 · HV làm & nộp bài

**Nguồn:** Cổng HV › Bài tập

- **upload:** ≥ 1 file (ảnh / PDF / Word / ghi âm, ≤ 20 MB mỗi file, ≤ 5 file) hoặc bài làm dạng chữ. Nộp lại được khi **chưa chấm**.
- **quiz:** bấm làm → server bốc đề (đảo đề: mỗi HV / mỗi lần 1 đề), trả câu **không kèm đáp án**; nộp → server chấm, lưu lần **cao nhất**, trả đáp án + giải thích.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-02.E1 | Không file, không chữ | `academic.submission.empty` |
| ND-02.E2 | Nộp sau hạn | Nhận, đánh dấu **nộp muộn** (GV thấy) |
| ND-02.E3 | Hết số lần làm quiz | `academic.submission.no_attempts_left` |
| ND-02.E4 | Quá giới hạn phút (+1 phút trễ mạng) | Chấm phần đã trả lời, đánh dấu hết giờ |
| ND-02.E5 | Nộp lại bài upload đã chấm | `academic.submission.already_graded` |
| ND-02.E6 | HV không còn trong lớp (nghỉ / chuyển) | `academic.submission.not_in_class` |

## ND-03 · Chấm bài

**Quyền:** GV của lớp, hoặc `academic.classes.update` · Điểm 0 → thang điểm (bước 0,25), nhận xét. Sửa điểm được; quiz GV ghi đè được (vd bù câu lỗi).
**E1** điểm ngoài thang → `academic.submission.invalid_score` · **E2** chấm bài chưa nộp → `academic.submission.not_found`.

---

## ND-04 · Tạo kỳ thi từ mốc lộ trình

**Nguồn:** tab Bài thi của lớp › "Tạo kỳ thi" · **Quyền:** `academic.classes.update`

**Các bước:**
1. Chọn mốc thi (giữa / cuối kỳ) — phạm vi bài lấy từ lộ trình (bài sau mốc trước → bài của mốc).
2. Hình thức*: **online** (HV làm trên Cổng) · **giấy** (in đề, thi tại lớp). Thời điểm bắt đầu* (mặc định giờ buổi thi của lớp), thời lượng* (10–240 phút), % đạt (mặc định 60).
3. Các phần*: tên, kỹ năng, số câu, điểm mỗi câu (vd Từ vựng 20 câu × 1, Ngữ pháp 15 × 2, Nghe 10 × 2). Bốc câu **đã duyệt** trong phạm vi bài → xem trước, đổi từng câu.
4. **Phát hành** → đề chốt (không đổi câu nữa); online: HV thấy ở "Lịch thi" · giấy: tải bản in (đề + đáp án riêng).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-04.E1 | Mốc thi đã có kỳ thi (chưa hủy) | `academic.exam.duplicate` — 1 mốc / lớp 1 kỳ thi |
| ND-04.E2 | Không đủ câu trong phạm vi | `academic.exam.not_enough_questions` kèm số câu thiếu từng phần |
| ND-04.E3 | Không có phần nào / phần 0 câu | `academic.exam.sections` |
| ND-04.E4 | Sửa đề sau phát hành | `academic.exam.published` — chỉ đổi giờ thi (khi chưa ai làm) |
| ND-04.E5 | Hủy kỳ thi đã có bài làm | `academic.exam.has_attempts` |

## ND-05 · HV làm bài thi online

**Nguồn:** Cổng HV › Bài thi · Mở từ **giờ bắt đầu** đến **giờ bắt đầu + thời lượng** (mở sớm 0 phút, vào trễ được nhưng hết giờ chung).
1 lần làm duy nhất; đề riêng (đảo câu / đáp án theo HV); câu hỏi trả **không đáp án**; tự lưu câu đã chọn; nộp / hết giờ → chấm từng phần → `exam_results`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-05.E1 | Chưa tới giờ / đã hết giờ | `academic.exam.not_open` |
| ND-05.E2 | Đã nộp | `academic.exam.already_submitted` |
| ND-05.E3 | Mất mạng giữa chừng | Mở lại tiếp tục bài đang làm, giờ vẫn chạy |
| ND-05.E4 | Hết giờ mà chưa nộp | Job / lần gọi sau tự nộp các câu đã lưu |
| ND-05.E5 | Kỳ thi giấy | Không làm online — chỉ xem lịch thi |

## ND-06 · Nhập / sửa điểm thi, công bố

**Nguồn:** trang Kết quả kỳ thi (`ExamResultsPage`) · **Quyền:** GV của lớp hoặc `academic.classes.update`
- Thi giấy: nhập điểm từng phần (0 → tối đa phần) hoặc **vắng thi**; online: điểm tự chấm, sửa được (phần tự luận / bù câu lỗi) + nhận xét.
- **Công bố kết quả** → HV / PH thấy điểm + nhận xét (+ đáp án với thi online). Chưa công bố → cổng chỉ thấy "Đã nộp".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-06.E1 | Điểm phần ngoài 0 → tối đa | `academic.exam.invalid_score` |
| ND-06.E2 | Sửa điểm sau công bố | Cho, ghi nhật ký (trước → sau) |
| ND-06.E3 | HV vắng thi | `absent`, không tính vào điểm trung bình lớp; tính 1 "bài thi yếu" cho cảnh báo bỏ học |

---

## ND-07 · Thông báo cho lớp

**Nguồn:** tab Thông báo của lớp (gửi cả lớp) / tab Học viên (thao tác hàng loạt → chỉ HV chọn + PH) · **Quyền:** `academic.classes.update` hoặc GV của lớp
Tiêu đề*, nội dung*, gửi tới* (HV / PH / GV), kênh* (cổng — chuông + trang thông báo; Zalo / email ⏳ giai đoạn 22), chỉ HV còn nợ học phí, hẹn giờ.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-07.E1 | Không có người nhận (lọc nợ phí ra 0) | `academic.announcement.no_recipients` |
| ND-07.E2 | Hẹn giờ trong quá khứ | `academic.announcement.invalid_schedule` |
| ND-07.E3 | Sửa / hủy thông báo đã gửi | Không cho — chỉ hủy được thông báo **chờ hẹn giờ** |
| ND-07.E4 | Đếm đã xem | HV / PH mở thông báo trên cổng → +1 (mỗi người 1 lần) |

## ND-08 · HV đánh giá GV

**Nguồn:** Cổng HV (khảo sát mở khi lớp qua **giữa khóa** — mốc thi giữa kỳ đã diễn ra — và **2 tuần cuối khóa**) 🆕 giao diện; xem ở tab Đánh giá GV + hồ sơ nhân sự
Mỗi HV × GV × đợt: 1 lần; tổng 1–5 sao + 4 tiêu chí (phương pháp, dễ hiểu, nhiệt tình, đúng giờ), góp ý, ẩn danh.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-08.E1 | Đánh giá 2 lần cùng đợt | `academic.teacher_review.duplicate` |
| ND-08.E2 | Sao ngoài 1–5 | `academic.teacher_review.invalid_rating` |
| ND-08.E3 | Ngoài thời gian khảo sát | `academic.teacher_review.not_open` |
| ND-08.E4 | GV xem đánh giá của mình | Ẩn danh → không trả tên / Id HV |

## ND-09 · Flashcard

- **Soạn bộ thẻ** 🆕 giao diện (Lộ trình › bài học › Flashcard, `academic.curriculum.update`): tên*, loại (từ vựng / Kanji), bài học*; thẻ: mặt trước*, cách đọc, nghĩa*, ví dụ; sắp xếp; nhập nhiều dòng một lần.
- **HV ôn** (Cổng HV › Flashcard): bộ thẻ của các bài trong lộ trình lớp đang học. Nhớ → lên hộp (ôn lại sau 1 → 3 → 7 → 14 → 30 ngày), quên → hộp 0, ôn ngay. Chuỗi ngày học.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| ND-09.E1 | Thẻ thiếu mặt trước / nghĩa | `academic.flashcard.card_required` |
| ND-09.E2 | Xóa thẻ đã có HV ôn | Xóa thẻ + tiến độ của thẻ đó |
| ND-09.E3 | HV ôn thẻ của bộ không thuộc lớp mình | `academic.flashcard.not_available` |

## ND-10 · Tài liệu GV

**Nguồn:** Cổng GV › Tài liệu của tôi; Cổng HV › Tài liệu · Tiêu đề*, mô tả, file (≤ 50 MB) hoặc link*, lớp* (chỉ lớp mình dạy), buổi học. Đổi file → lượt xem về 0.
**E1** gửi lớp không dạy → `library.teacher_doc.class_not_allowed` (lớp GV dạy đọc qua `Academic.Contracts`) · **E2** HV xem → ghi lượt xem (1 lần / HV), tải qua link tạm · **E3** xóa → xóa mềm, xóa file khi dọn Thùng rác.

## ND-11 · Điểm & tiến độ ở cổng

HV / PH: bài tập (đã nộp / chưa / muộn / điểm / nhận xét), kết quả thi đã công bố, flashcard; PH xem theo con. Không trả ghi chú nội bộ, học phí ở cổng GV.

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/academic/...`) | Quyền |
|---|---|---|
| ND-01, 03 | `GET/POST/PUT/DELETE classes/{id}/assignments` · `GET assignments/{id}/submissions` · `PUT submissions/{id}/grade` | lớp / GV của lớp |
| ND-02 | cổng `GET portal/learning/assignments` · `POST …/{id}/submissions` (upload) · `POST …/{id}/quiz/start` · `POST …/quiz/submit` | chủ tài khoản |
| ND-04, 06 | `GET/POST/PUT classes/{id}/exams` · `POST exams/{id}/publish|cancel|publish-results` · `GET exams/{id}/results` · `PUT exams/{id}/results/{studentId}` | lớp |
| ND-05 | cổng `GET portal/learning/exams` · `POST …/{id}/start` · `PUT …/attempts/{id}/answers` · `POST …/attempts/{id}/submit` | chủ tài khoản |
| ND-07 | `GET/POST classes/{id}/announcements` · `DELETE announcements/{id}` (chờ gửi) · cổng `GET portal/announcements` · `POST …/{id}/read` | lớp / GV / chủ tài khoản |
| ND-08 | `GET classes/{id}/teacher-reviews` · cổng `POST portal/learning/teacher-reviews` · cổng GV `GET portal/teaching/reviews` | lớp / chủ tài khoản |
| ND-09 | `GET/POST/PUT/DELETE flash-decks` · cổng `GET portal/learning/flashcards` · `POST …/reviews` | `academic.curriculum` / chủ tài khoản |
| ND-10 | `/api/v1/library/…` cổng GV `GET/POST/PUT/DELETE portal/teaching/documents` · cổng HV `GET portal/learning/documents` · `GET …/{id}/link` | chủ tài khoản |

- **Job:** gửi thông báo hẹn giờ (mỗi phút) · tự nộp bài thi online hết giờ (mỗi phút).
- **Sự kiện:** `NotificationRequested` (Notifications) cho bài tập mới, điểm, kết quả thi, thông báo lớp.
- **Students HV-14:** đọc `IAssignmentStats` / `IExamStats` (Academic.Contracts) thay cho số 0 trong `RiskCalculator`.
