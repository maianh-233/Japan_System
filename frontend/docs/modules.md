# Cấu trúc module — JP Center ERP

> Menu trong code: `src/config/navigation.ts`.

## 1. Cổng nhân viên (`/admin`)

Mục **(mới)** = thêm từ danh sách gợi ý. Mỗi mục có danh sách "Chức năng dự kiến" trong `navigation.ts` (hiển thị trên trang đang xây dựng).

| # | Module | Mục ở sidebar | Ghi chú |
|---|---|---|---|
| 1 | **Tổng quan** | Bảng điều khiển · Báo cáo | Số liệu theo chi nhánh đang chọn |
| 2 | **Tuyển sinh (CRM)** | Lead · Chia lead & SLA (mới) · Test xếp lớp (mới) · Chiến dịch | Lead = người quan tâm, **chưa học**. Bấm vào lead → thông tin + **lịch sử tư vấn** |
| 3 | **Học viên** | Hồ sơ học viên · Phụ huynh / Người giám hộ · Cảnh báo bỏ học (mới) · Thi JLPT (mới) · Chứng nhận (mới) · Du học / XKLĐ (mới) | Học viên = **đã đăng ký lớp** |
| 4 | **Đào tạo** | Khóa học · Lộ trình (Curriculum) · Ngân hàng câu hỏi · Lớp học · Buổi học (Session) · Phòng học | Trùng lịch, dạy thay, học bù nằm trong Lớp học / Buổi học |
| 5 | **Thư viện** | Thư viện tài liệu | Không gắn lộ trình; có watermark, thống kê lượt đọc |
| 6 | **Tài chính** | Học phí & bảng giá · Hóa đơn & thu tiền · Đối soát VietQR (mới) · Công nợ · Lương giáo viên · Lương nhân viên · Hoa hồng & KPI Sale (mới) | Lương GV tự tính từ Session |
| 7 | **Nhân sự** | Tuyển dụng · Onboarding (mới) · Nhân viên · Giáo viên · Chấm công · Đơn từ & phê duyệt | |
| 8 | **Công việc** | Task · Xếp ca làm việc · Bàn giao ca | Task dạng Kanban |
| 9 | **Hệ thống** | Chi nhánh · Vai trò & quyền · Phân quyền người dùng · Tích hợp (mới) · Nhật ký | Tích hợp: Zalo OA, VietQR, Zoom, nguồn lead |

**Cổng mới — Phụ huynh (`/parent`)**: Tổng quan · Lịch học của con · Chuyên cần · Kết quả học tập · Học phí.
Thêm ở cổng giáo viên: **Lời mời dạy thay**. Cổng học viên: **Flashcard**, **Chứng nhận & JLPT**.

## 2. Quan hệ dữ liệu phần Đào tạo

```
Khóa học (Course)  — sản phẩm bán: JLPT N3, Kaiwa B1… (giá, số buổi, trình độ đầu vào/đầu ra)
 └─ Lộ trình (Curriculum)  — 1 khóa có 1 lộ trình (có phiên bản)
     ├─ Lesson (Bài học)  — thứ tự, số buổi dự kiến
     │   ├─ Chapter  — nội dung giảng dạy
     │   │   └─ Tài liệu giảng dạy (PDF, slide, audio, video, link)  ← kho riêng của lộ trình
     │   └─ Bài kiểm tra nhỏ (Quiz)  ← câu hỏi lấy từ Ngân hàng câu hỏi
     └─ Exam lớn (giữa kỳ / cuối kỳ / thi thử JLPT)  ← câu hỏi lấy từ Ngân hàng câu hỏi

Lớp học (Class) — mở theo 1 Khóa học, dùng lộ trình của khóa
 ├─ Giáo viên, Phòng, Lịch lặp (vd: T2-T4-T6 18:00–19:30), Sĩ số
 ├─ Học viên ghi danh (Enrollment) → sinh Hóa đơn học phí
 └─ Buổi học (Session) — sinh tự động từ lịch lặp
     ├─ Gắn Lesson dự kiến → GV mở buổi là thấy Chapter + tài liệu cần dạy
     ├─ Trạng thái: Đã lên lịch → Đang diễn ra → Đã diễn ra / Đã hủy / Học bù
     ├─ Điểm danh học viên, nhật ký buổi (dạy đến đâu, nhận xét)
     └─ Buổi "Đã diễn ra" + đã xác nhận → tính Lương giáo viên
```

Thư viện tài liệu đứng **riêng**: sách tham khảo, đề JLPT các năm, tài liệu tự học — học viên/giáo viên mượn đọc, không ảnh hưởng lộ trình.

## 2b. Lead ↔ Học viên (đã dựng giao diện — `/admin/crm/leads`)

```
Lead (người LIÊN HỆ: chính người học hoặc phụ huynh)
 ├─ Lịch sử tư vấn (tab 2 trong chi tiết lead)
 └─ LeadStudentLink { relationship: bản thân | con | anh chị em | vợ chồng | khác }  ──►  Học viên (hồ sơ RIÊNG)
```

- Quan hệ **nhiều–nhiều**: 1 phụ huynh nhiều con · bố và mẹ cùng là lead của 1 con · người học tự đăng ký (`self`).
- Tạo hồ sơ học viên từ lead: chọn "Chính người liên hệ" hoặc "Người khác" (nhập tên, quan hệ). Lead tự chuyển "Đã chốt", lịch sử tư vấn tự ghi dòng hệ thống.
- **Ẩn lead** = cờ `hidden` trên lead; học viên liên kết không bị ảnh hưởng; xem lại bằng bộ lọc "Hiện lead đã ẩn".
- Trùng SĐT: cảnh báo (vẫn cho lưu) + link mở lead cũ.
- Dữ liệu giả: `src/mocks/data/crm.ts`, store `src/features/crm/crmStore.ts` (thay bằng API khi có backend).

## 3. Cần làm rõ / lưu ý

| Vấn đề | Đề xuất |
|---|---|
| **"Handover"** có thể là 2 việc khác nhau | Tách: **Xếp ca** (quản lý xếp lịch làm cho nhân viên làm theo ca) và **Bàn giao ca** (cuối ca ghi lại lead đang dở, tiền mặt tại quầy, việc tồn — ca sau xác nhận đã nhận). Menu đã tách sẵn 2 mục. |
| Ai làm theo ca? | Thêm thuộc tính **"Làm theo ca"** cho vị trí/role. Chỉ nhân viên có thuộc tính này mới xuất hiện ở Xếp ca; nhân viên hành chính chấm công theo giờ cố định. |
| Lương nhân viên theo ca phụ thuộc Chấm công + Xếp ca | Chấm công so khớp với ca được xếp → đi muộn/về sớm/vắng ca → bảng công tháng → Lương nhân viên. |
| Sửa lộ trình khi lớp đang học | Lộ trình cần **phiên bản**: lớp đang học giữ bản cũ, lớp mới dùng bản mới — tránh lệch bài giữa chừng. |
| Tài liệu trong Chapter vs Thư viện | Hai kho riêng như yêu cầu. Nếu sau này muốn dùng lại 1 file thư viện trong Chapter → chỉ thêm "liên kết", không copy. |
| Lead → Học viên | Khi chuyển đổi: giữ nguyên lịch sử tư vấn, lead chuyển trạng thái "Đã chốt" và trỏ sang hồ sơ học viên (không xóa lead). Học viên đã nghỉ có thể quay lại thành lead "tái tuyển sinh". |
| Điểm danh & Lịch học | Không còn menu riêng — nằm trong **Buổi học (Session)** (xem dạng danh sách hoặc lịch). |

## 4. Gợi ý chức năng thêm — ĐÃ ĐƯA VÀO MENU

| Ưu tiên | Module | Chức năng | Giá trị |
|---|---|---|---|
| Cao | Tuyển sinh | **Tự chia lead theo ca**: lead mới tự giao cho Sale đang trong ca (xoay vòng) | Gắn trực tiếp với Xếp ca, không lead nào bị bỏ sót |
| Cao | Tuyển sinh | **Cảnh báo SLA**: lead mới chưa được gọi sau 15 phút → nhắc Sale + báo quản lý | Tăng tỷ lệ chốt |
| Cao | Tuyển sinh | **Test xếp lớp online** cho lead (dùng Ngân hàng câu hỏi) → gợi ý khóa phù hợp | Tư vấn chính xác, chuyên nghiệp |
| Cao | Đào tạo | **Kiểm tra trùng lịch** phòng & giáo viên khi tạo lớp/buổi; gợi ý **GV dạy thay** khi duyệt đơn hủy buổi | Tránh sự cố vận hành |
| Cao | Học viên | **Cảnh báo nguy cơ bỏ học**: vắng ≥ 3 buổi liên tiếp, điểm tụt, nợ học phí → nhắc Giáo vụ gọi chăm sóc | Giữ chân học viên |
| Cao | Tài chính | **Lương GV tự tính** từ Session đã diễn ra × đơn giá theo cấp độ/hình thức (online/offline), trừ buổi bị hủy | Hết tính tay |
| TB | Tài chính | **Hoa hồng / KPI Sale** theo doanh số chốt từ lead | Động lực cho Sale |
| TB | Tài chính | **Thanh toán VietQR** + tự đối soát giao dịch; nhắc học phí qua Zalo/Email | Thu tiền nhanh |
| TB | Đào tạo | **Theo dõi đăng ký thi JLPT** (kỳ tháng 7 & 12): ai đăng ký, cấp độ, kết quả | Rất đặc thù trung tâm tiếng Nhật |
| TB | Đào tạo | **Cấp chứng nhận hoàn thành khóa** (PDF có mã QR xác thực) | Marketing + uy tín |
| TB | Nhân sự | **Tuyển dụng → Onboarding**: ứng viên đạt → tự tạo hồ sơ nhân viên + tài khoản + checklist nhận việc | Nối liền quy trình |
| TB | Công việc | Task dạng **Kanban**, task lặp lại, gắn task vào lead/lớp/học viên, nhắc hạn | Quản lý việc trực quan |
| Thấp | Thư viện | Xem trực tuyến có **watermark** tên người xem, thống kê lượt đọc, lọc theo cấp độ N5–N1 | Bảo vệ bản quyền |
| Thấp | Chung | **Cổng phụ huynh** (xem lịch, chuyên cần, học phí của con) · **Zalo OA** gửi thông báo | Tăng tương tác |
| Thấp | Học viên | **Flashcard từ vựng/Kanji** theo bài học + chuỗi ngày học (streak) | Học viên dùng app mỗi ngày |
| Tùy | Học viên | **Hồ sơ du học / XKLĐ** (giấy tờ, COE, visa) — nếu trung tâm có mảng này | Mở rộng dịch vụ |

## 5. Cổng giáo viên (`/teacher`), học viên (`/student`), phụ huynh (`/parent`)

- **Giáo viên**: Tổng quan · Thời khóa biểu · Lớp của tôi (mở buổi → xem Chapter + tài liệu, điểm danh, nhật ký buổi, cập nhật tiến độ) · Bài tập · Tài liệu của tôi · Đơn hủy buổi · Lời mời dạy thay · Chấm công
- **Học viên**: Tổng quan · Thời khóa biểu · Bài tập · Bài thi · Flashcard · Tài liệu · Học phí · Đơn xin nghỉ · Tiến độ học tập · Chứng nhận & JLPT
- **Phụ huynh**: Tổng quan · Lịch học của con · Chuyên cần · Kết quả học tập · Học phí
