# Nghiệp vụ & chức năng — JP Center ERP

> Tổng hợp từ giao diện frontend (`frontend/src/config/navigation.ts`, `frontend/src/features/**/types.ts`, các store).
> Là căn cứ để thiết kế bảng ([database.md](database.md)) và API. Thêm / đổi chức năng trên giao diện → cập nhật file này.

**Đối tượng dùng hệ thống (4 cổng)**

| Cổng | Ai dùng | Đặc điểm |
|---|---|---|
| Nhân viên `/admin` | Super Admin, Admin, Quản lý chi nhánh, Giáo vụ, Kế toán, Sale, Marketing, Nhân sự | Phân quyền theo mã quyền + phạm vi (của mình / chi nhánh / toàn hệ thống) |
| Giáo viên `/teacher` | Giáo viên, trợ giảng | Chỉ thấy lớp mình dạy; **không** thấy học phí, lịch sử chăm sóc |
| Học viên `/student` | Học viên | Chỉ dữ liệu của mình |
| Phụ huynh `/parent` | Lead có liên kết với học viên | Thấy tất cả các con; người quyết định với học viên nhỏ tuổi |

---

## 1. Tổng quan (Dashboard)

| Chức năng | Mô tả |
|---|---|
| Bảng điều khiển | Việc cần làm hôm nay theo vai trò (Sale: lead quá SLA, lịch hẹn · Giáo vụ: buổi chờ xác nhận, đơn chờ duyệt · Kế toán: hóa đơn quá hạn, giao dịch chưa khớp) |
| Báo cáo | Chuyển đổi lead theo nguồn / chiến dịch / Sale · doanh thu, công nợ theo tháng / chi nhánh · sĩ số, chuyên cần, lấp đầy phòng · so sánh chi nhánh · xuất Excel |

Chỉ **đọc** — dựng từ dữ liệu các module khác (module Reporting).

## 2. Tuyển sinh (CRM)

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Lead** | Danh sách + Kanban theo giai đoạn: Mới → Đang tư vấn → Hẹn test → Chốt / Mất. Lead là **người liên hệ** (chính người học hoặc phụ huynh). Phát hiện trùng SĐT / email (cảnh báo, vẫn cho lưu). Ẩn lead = bật cờ, không ảnh hưởng học viên liên kết |
| Lịch sử tư vấn | Mỗi lần gọi / Zalo / gặp / nhắn: kênh, kết quả, nội dung, hẹn gọi lại. Dòng hệ thống tự sinh (vd "Tạo hồ sơ học viên") |
| Chuyển lead → học viên | Tạo hồ sơ học viên (chính người liên hệ hoặc người khác: con, anh chị em…). Lead → "Đã chốt", giữ lịch sử. Quan hệ lead ↔ học viên **nhiều–nhiều** (1 phụ huynh nhiều con; bố và mẹ cùng liên hệ cho 1 con) |
| **Chia lead & SLA** | Quy tắc theo thứ tự ưu tiên, điều kiện: nguồn, chi nhánh, khóa quan tâm. Cách chia: xoay vòng / ít lead mở nhất / cố định. Chỉ chia cho Sale đang trong ca. Quá SLA → báo người phụ trách; quá 2×SLA → tự chuyển Sale khác (mỗi lead 1 lần). Ghi nhật ký vì sao chọn Sale |
| **Test xếp lớp** | Tạo đề từ Ngân hàng câu hỏi (bốc N câu **đã duyệt** theo cấp độ + kỹ năng), thời gian làm, thang gợi ý trình độ / khóa theo % điểm. Gửi link (Zalo/SMS/email, có hạn) → lead làm online → tự chấm → gợi ý trình độ, ghi vào lịch sử tư vấn |
| **Lịch hẹn** | Hẹn lead đến tư vấn / làm test tại trung tâm. Kiểm tra trùng phòng (cả buổi học), trùng người tiếp (cả giờ dạy), trùng lead. Tự nhắc lead + người tiếp trước 1 giờ. Kết quả: đã đến / không đến / hủy |
| **Tư vấn lên lớp** | Danh sách học viên lớp sắp kết thúc được giữ chỗ lớp kỳ sau → giao Sale tư vấn **người quyết định** (học viên nhỏ → phụ huynh, gom các con). Chốt → ghi danh + hóa đơn; từ chối ghi lý do; tỉ lệ lên lớp |
| Phễu chuyển đổi | Lead → Đã gọi → Test → Chốt theo Sale / chiến dịch / nguồn; thời gian trung bình mỗi bước (tính từ dữ liệu, không lưu riêng) |
| **Mẫu tin nhắn** | Mẫu Zalo / SMS theo tình huống (chào lead, nhắc test, nhắc học phí, báo nghỉ, báo đến lớp, báo vắng, báo cáo tuần…), biến `{ten}`, `{lop}`, `{so_tien}`. Nhật ký gửi |
| **Chiến dịch** | Kênh, chi nhánh, thời gian, ngân sách / đã chi, chỉ tiêu lead, mã UTM (lead từ form tự gắn chiến dịch), khóa quảng bá. Số lead, tỷ lệ chốt, chi phí / lead |

## 3. Học viên

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Hồ sơ học viên** | Thông tin cá nhân, chi nhánh, lớp đang / đã học (từ ghi danh), học phí, tài khoản cổng, phụ huynh, nhận xét cuối khóa của GV (chỉ nhân viên thấy). Chỉ ẩn được khi không còn ghi danh "Đang học" / "Bảo lưu" |
| **Ghi danh** | Học viên vào lớp → sinh hóa đơn. Trạng thái: đang học · bảo lưu (đến ngày) · hoàn thành · chuyển lớp (nối ghi danh cũ ↔ mới) · nghỉ |
| **Phụ huynh / Người giám hộ** | 1 phụ huynh nhiều con. Cờ trên liên kết: người liên hệ chính (mỗi HV tối đa 1), nhận hóa đơn, nhận thông báo. Kênh nhận: Zalo / SMS / email. Cấp tài khoản Cổng phụ huynh |
| **Cảnh báo bỏ học** | Tự phát hiện theo ngưỡng ở Cài đặt chung: vắng không phép ≥ N, vắng liên tiếp, chuyên cần thấp, không nộp bài, điểm thấp, nợ quá hạn. Mức rủi ro thấp / TB / cao. Giao người chăm sóc, trạng thái (chưa liên hệ → đang liên hệ → theo dõi → ổn định), hẹn gọi lại, nhật ký liên hệ |
| **Chứng chỉ JLPT** | Kỳ thi tháng 7 & 12. Đăng ký: cấp độ, địa điểm thi, hồ sơ (ảnh, CCCD, phiếu ĐK), lệ phí. Trạng thái: đăng ký → đóng phí → nộp hội đồng → có phiếu dự thi → có kết quả / vắng / hủy. Nhập điểm từng phần, tỷ lệ đỗ theo lớp / GV. Nhận chứng chỉ gốc từ hội đồng → phát cho học viên |
| **Chứng nhận hoàn thành** | Chứng nhận của **trung tâm** khi học viên pass lớp (khác JLPT): mã tra cứu + QR xác thực, PDF theo mẫu, gửi email, gửi lại, cấp ngoại lệ có lý do |
| **Du học / XKLĐ** | Chương trình (du học, Tokutei, TTS, kỹ sư). Đối tác tiếp nhận, nơi đến, kỳ nhập học. Giai đoạn: tư vấn → giấy tờ → phỏng vấn → COE → visa → xuất cảnh. Checklist giấy tờ, phí, ghi chú |

## 4. Đào tạo

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Khóa học** | "Sản phẩm" bán: danh mục (JLPT, Kaiwa, Kids, Business, XKLĐ), trình độ vào → ra, hình thức nhóm / 1-1, GV Việt / Nhật / kết hợp, offline / online, số buổi, phút/buổi, buổi/tuần, học phí (nhóm: trọn khóa; 1-1: mỗi buổi), chi nhánh bán. Gắn nhiều lộ trình, đúng 1 mặc định |
| **Lộ trình (Curriculum)** | Lesson → Chapter → Tài liệu (pdf, ppt, audio, video, Drive…; cờ HV xem được hay chỉ GV). Mốc thi giữa kỳ / cuối kỳ chèn **sau** 1 lesson (chỉ đánh dấu, không chứa đề). Bậc chuẩn / nâng cao. **Phiên bản**: đã phát hành thì khóa nội dung, muốn sửa → tạo phiên bản mới |
| **Ngân hàng câu hỏi** | Kỹ năng: từ vựng, Kanji, ngữ pháp, đọc hiểu, nghe hiểu (audio + lời thoại). Dạng: chọn 1/4, sắp xếp ★. Cấp độ N5–N1, độ khó, furigana `漢字{かんじ}`, gắn lesson. Trạng thái: nháp → chờ duyệt → đã duyệt → ngừng dùng. **Chỉ câu đã duyệt được bốc vào đề**. Thống kê lượt dùng / tỷ lệ đúng |
| **Lớp học** | Mở theo khóa + chốt lộ trình. Mã lớp theo loại GV (`GVV_`, `GVN_`, `GVVN_`). GV (1–2), trợ giảng, phòng / online, lịch lặp (thứ + giờ), tổng giờ → số buổi, sĩ số min/max, học phí. Vòng đời: chờ khai giảng → đang học ⇄ tạm dừng → kết thúc; hủy từ trạng thái chưa kết thúc (chỉ trung tâm). Kiểm tra trùng phòng & GV. Lớp kế tiếp (lên lớp) |
| **Buổi học (Session)** | Sinh tự động từ lịch. Loại: bài học / thi; nguồn: thường / học bù / học thêm. Đổi GV, phòng, dời lịch cho riêng buổi. **Điểm danh** (QR cố định theo buổi / GV / nhân viên): có mặt, muộn, vắng có phép, vắng không phép, về sớm; học bù ở lớp khác. **Chấm công GV / trợ giảng theo buổi** (vai trò: GV lớp, dạy thay, trợ giảng). Sự cố giữa buổi + cách xử lý. Nhật ký hỗ trợ của nhân viên. Báo cáo buổi của GV (nội dung, BTVN, tiến độ đủ / một phần / chưa, chapter đã dạy). **Xác nhận buổi đã dạy** → căn cứ lương GV |
| **Đơn chờ xử lý** | Gom: đơn hủy buổi (GV) · đơn xin nghỉ (HV/PH) · đơn từ nhân viên. Duyệt đơn hủy buổi → mời GV dạy thay (ai nhận trước dạy, lời mời khác tự hết hạn) **hoặc** hủy buổi & xếp học bù |
| **Phòng học** | Theo chi nhánh: loại, sức chứa, diện tích, thiết bị. Trạng thái: hoạt động / bảo trì (đến ngày) / ngừng. Lịch sử dụng & lấp đầy **suy ra từ buổi học**. Báo hư hỏng: mức độ, người nhận sửa, hạn, chi phí; mở → đang sửa → đã xong |
| **Bài tập** | 2 loại: **upload** (HV nộp ảnh / file / ghi âm, GV chấm tay) và **quiz** (bốc câu từ ngân hàng theo bài học, kỹ năng, độ khó, số câu; giới hạn giờ, số lần làm, đảo câu; tự chấm). Hạn nộp, thang điểm |
| **Bài thi** | Kỳ thi tổ chức theo mốc thi của lộ trình, đề lấy từ ngân hàng. Kết quả từng phần, vắng thi, nhận xét |
| Thông báo lớp | Gửi HV / PH / GV qua cổng / Zalo / email; chỉ HV nợ phí hoặc chọn HV; hẹn giờ; số đã xem |
| Đánh giá giáo viên | HV chấm sao GV giữa / cuối kỳ theo tiêu chí, ẩn danh được |
| **Nhận xét cuối khóa** | GV đánh giá từng HV (kỹ năng, thái độ, điểm mạnh / yếu, đề xuất bước tiếp) — dành cho Sale tư vấn, **không** hiện cho HV |
| **Lên lớp** | Gợi ý từ nhận xét GV + chuyên cần + điểm → giáo vụ giữ chỗ tạm vào lớp kỳ sau (không tính sĩ số, không hóa đơn) → lớp cũ kết thúc thì giao Sale → chốt / từ chối / không liên lạc được. Hết hạn giữ chỗ tự trả |
| Flashcard | Bộ thẻ từ vựng / Kanji theo bài, ôn lặp ngắt quãng (hộp Leitner 0–5), chuỗi ngày học |

## 5. Thư viện

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Thư viện tài liệu** | Sách tham khảo, đề JLPT các năm, tài liệu tự học, audio / video. Lọc cấp độ, kỹ năng, thẻ. Đối tượng xem (HV / GV / NV), chi nhánh. Xem trực tuyến có **watermark** tên người xem, mặc định chặn tải. Thống kê lượt xem, người đọc, thời gian đọc. **Không** gắn lộ trình |
| Tài liệu của giáo viên | GV tự soạn, gửi cho lớp mình dạy (có thể gắn buổi), biết HV nào đã xem |

## 6. Tài chính

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Học phí & bảng giá** | Giá riêng theo chi nhánh (không có = giá niêm yết của khóa). Ưu đãi: %, số tiền, combo nhiều khóa; thời gian, giới hạn lượt. Chính sách đóng nhiều đợt (% từng đợt, hạn theo ngày khai giảng, phụ thu) |
| **Hóa đơn & thu tiền** | Sinh từ ghi danh. Mã theo mẫu ở Cài đặt (`HD-{CN}-{YY}{MM}-{SO:4}`). Thu nhiều đợt (tiền mặt, chuyển khoản, VietQR, thẻ). Hoàn tiền (từ ngưỡng cấu hình cần **duyệt 2 người**). Hủy phần còn lại khi nghỉ / lớp hủy. **Hủy hóa đơn** lập sai → duyệt 2 người. Nhắc học phí qua Zalo / email |
| **Đối soát VietQR** | Nhận giao dịch ngân hàng → tự khớp hóa đơn theo nội dung CK; danh sách chưa khớp xử lý tay / bỏ qua |
| **Công nợ** | Theo học viên / lớp / chi nhánh, tuổi nợ; lịch sử nhắc (tính từ hóa đơn) |
| **Lương giáo viên** | Buổi đã xác nhận × đơn giá (offline / online), trừ buổi hủy, cộng dạy thay / học bù; thưởng / phạt; chốt bảng lương tháng (snapshot) |
| **Lương nhân viên** | Lương cứng + công từ Chấm công / Xếp ca + phụ cấp − khấu trừ + hoa hồng; chốt tháng |
| **Hoa hồng & KPI Sale** | Bậc hoa hồng theo doanh thu thực thu, chỉ tiêu tháng từng Sale, bảng xếp hạng, tự đưa vào lương |

## 7. Nhân sự

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Tuyển dụng** | Tin tuyển (chi nhánh, phòng ban, số lượng, hạn). Ứng viên: Ứng tuyển → Sàng lọc → Phỏng vấn → Offer → Nhận việc / Loại. Lịch phỏng vấn, điểm, offer |
| **Onboarding** | Ứng viên đạt → checklist (giấy tờ, tài khoản, thiết bị, đào tạo), người hướng dẫn → tự tạo hồ sơ nhân viên + tài khoản |
| **Nhân viên** | 1 bảng cho nhân viên / giáo viên / trợ giảng. Phòng ban, vị trí, quản lý trực tiếp (cây quản lý), hợp đồng, làm theo ca, phép năm còn lại, trạng thái |
| **Giáo viên** | Hồ sơ giảng dạy: GV Việt / Nhật, cấp độ dạy được, chứng chỉ, đơn giá offline / online, lịch rảnh theo thứ × ca, chi nhánh dạy thêm |
| **Chấm công** | Nhân viên check-in / check-out; so với giờ hành chính / ca được xếp → muộn, về sớm, vắng. Sửa tay có lý do. (GV chấm công theo buổi dạy, không ở đây) |
| **Đơn từ & phê duyệt** | Nghỉ phép (có / không lương), đổi ca, công tác, đi muộn / về sớm, đề xuất, tạm ứng. Duyệt nhiều cấp, mỗi bước 1 người |

## 8. Công việc

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Task toàn hệ thống** | HR / quản lý giao cho mọi nhân viên: hạn, ưu tiên, người theo dõi, lặp lại, checklist, bình luận, gắn lead / lớp / học viên. Kanban: cần làm → đang làm → chờ duyệt → xong |
| Giao việc cấp dưới | Chỉ giao cho người dưới quyền theo cây quản lý (không cần mã quyền) |
| **Xếp ca** | Nhân viên "làm theo ca" (sáng / chiều / tối) theo tuần. NV đăng ký ca mong muốn, khai lịch rảnh / bận, xin đổi ca. Cảnh báo thiếu người |
| **Bàn giao ca** | Cuối ca: tiền mặt (hệ thống tính / NV đếm / người nhận đếm lại), lead đang dở, việc tồn, sự cố. Ca sau xác nhận nhận / khiếu nại |

## 9. Cá nhân (mọi nhân viên, không cần mã quyền)

Chấm công của tôi · Việc của tôi · Lịch rảnh / bận · Thông tin cá nhân · Mật khẩu & bảo mật (2FA).
**Tách riêng** với trang quản lý của HR / quản lý.

## 10. Hệ thống

| Chức năng | Mô tả & quy tắc |
|---|---|
| **Chi nhánh** | Mã, tên, địa chỉ, SĐT, email, quản lý, giờ mở cửa, tài khoản ngân hàng nhận học phí (VietQR), tiền tố hóa đơn, trạng thái (đang chuẩn bị / hoạt động / đóng) |
| **Vai trò & quyền** | Role mặc định (không xóa) + role tự tạo. Ma trận: mã quyền `module.page.action` → phạm vi own / branch / all. Super Admin toàn quyền |
| **Phân quyền người dùng** | Gán role theo chi nhánh (1 người có thể là Giáo vụ ở CN A và Sale ở CN B). Cấp / chặn quyền lẻ có thời hạn + lý do. Chỉ cấp được quyền mình đang có, phạm vi không rộng hơn. Khóa tài khoản, 2FA |
| Tài khoản cổng | Tài khoản đăng nhập cho học viên / phụ huynh (và GV qua tài khoản nhân viên) |
| **Tích hợp** | Zalo OA, ngân hàng / VietQR, Zoom / Google Meet, email, nguồn lead (Facebook, form web). Cấu hình (khóa bí mật), sự kiện bật / tắt, webhook, nhật ký gọi |
| **Nhật ký hệ thống** | Ai, lúc nào, vai trò, chi nhánh, nhóm (đăng nhập, quyền, dữ liệu, xuất file, tài chính, cài đặt), mức độ, trước → sau, IP, thiết bị |
| **Duyệt thao tác** | Maker–checker: hoàn tiền, hủy hóa đơn, cấp quyền tài chính. Người tạo không tự duyệt; chờ duyệt → duyệt / từ chối / rút lại |
| **Thùng rác** | Dữ liệu bị xóa khôi phục được trong N ngày (cấu hình), sau đó xóa hẳn |
| **Cài đặt chung** | SLA mặc định, giờ hành chính, mẫu mã hóa đơn, ngày khóa bảng lương, ngưỡng cảnh báo bỏ học, thông báo phụ huynh (báo đến lớp, báo vắng sau N phút, báo cáo tuần), ngưỡng hoàn tiền cần duyệt, số ngày giữ thùng rác |

## 11. Thông báo

Chuông thông báo trong app (realtime), Zalo OA, email, SMS. Tự gửi: nhắc lịch hẹn, báo đến lớp / vắng, nhắc học phí, báo cáo tuần cho phụ huynh, lời mời dạy thay.

## 12. Các cổng

| Cổng | Chức năng |
|---|---|
| **Giáo viên** | Tổng quan · Thời khóa biểu · Lớp của tôi (mở buổi → chapter + tài liệu, điểm danh, chấm công buổi, kết thúc buổi + báo cáo, nhập điểm, nhận xét cuối khóa) · Bài tập (tạo, chấm) · Tài liệu của tôi · Đơn hủy buổi · Lời mời dạy thay (nhận / từ chối) · Chấm công (buổi đã dạy, lương tạm tính) |
| **Học viên** | Tổng quan · Thời khóa biểu · Bài tập (làm, nộp) · Bài thi (đếm giờ, kết quả) · Flashcard · Tài liệu (lớp + thư viện) · Học phí (VietQR) · Đơn xin nghỉ + đăng ký học bù · Tiến độ · Chứng nhận · JLPT · Đánh giá GV · Bấm "Quan tâm" lớp kỳ sau |
| **Phụ huynh** | Tổng quan · Lịch học của con · Chuyên cần (xin nghỉ thay con) · Báo cáo tuần · Kết quả học tập · Học phí · Quyết định lên lớp cho các con |

## 13. Quy tắc xuyên suốt

- Mọi dữ liệu nghiệp vụ thuộc 1 chi nhánh → lọc theo phạm vi quyền.
- Xóa = xóa mềm (vào Thùng rác). Thao tác nhạy cảm ghi Nhật ký hệ thống.
- Thông tin nội bộ (lịch sử chăm sóc, học phí, nhận xét cuối khóa, ghi chú nội bộ) **không** trả về cổng GV / HV / PH (GV không thấy học phí; HV không thấy nhận xét cuối khóa).
- Học viên nhỏ tuổi: người quyết định là phụ huynh (lead liên kết, người liên hệ chính).
- Tiền: VND, số nguyên. Giờ lưu UTC, hiển thị giờ Việt Nam.
