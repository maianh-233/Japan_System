# Kế hoạch hoàn thiện — JP Center ERP

> Chốt 2026-11-20, sau giai đoạn 15. Cập nhật cột **Trạng thái** khi xong từng giai đoạn.
> Mỗi giai đoạn gồm 4 bước, xong bước nào thì dừng và commit bước đó: **(1)** domain + unit test · **(2)** lưu DB (cấu hình EF, migration, dữ liệu mẫu) ·
> **(3)** API, test tích hợp, SignalR / job · **(4)** nối frontend, dọn mock. Nhóm nào chưa có user story thì viết story trước bước 1.

## Hiện trạng (sau giai đoạn 15)

| Module | Backend | Frontend nối API |
|---|---|---|
| System: đăng nhập, phân quyền, chi nhánh, tài khoản cổng, duyệt thao tác | ✅ | ✅ |
| Hr: hồ sơ nhân viên / giáo viên | ✅ | ✅ |
| Hr: chấm công, đơn từ, tuyển dụng, onboarding · Work: task, xếp ca, bàn giao ca | ✅ | ✅ |
| Academic: khóa học, lộ trình, phòng, lớp, buổi, điểm danh, chấm công GV, đơn từ, dạy thay, lên lớp, nhận xét | ✅ | ✅ |
| Students: hồ sơ, ghi danh (HV-01 → 11), chăm sóc bỏ học (HV-14), cổng GV / HV / PH | ✅ | ✅ |
| Crm: lead, chia lead + SLA, test xếp lớp (cả trang làm bài công khai), lịch hẹn, chiến dịch, mẫu tin, phễu, tư vấn lên lớp | ✅ | ✅ (cả trang Phụ huynh HV-12) |
| Notifications (chuông) | ✅ | ✅ |
| Finance đợt 1 (hóa đơn, thu / hoàn / hủy, giá, ưu đãi, đối soát, công nợ) | ✅ | ✅ |
| Finance đợt 2 (lương, hoa hồng), Library | Chỉ có domain | ❌ |
| Reporting | Chưa có | ❌ |
| System: cài đặt chung, nhật ký, thùng rác, tích hợp | ✅ (trừ tích hợp — GĐ 22) | ✅ |
| Academic: ngân hàng câu hỏi | ✅ | ✅ |
| Academic: bài tập, bài thi, flashcard, thông báo lớp, đánh giá GV (+ tài liệu GV — Library) | ✅ | ✅ |

## Các giai đoạn

| GĐ | Nội dung | Phụ thuộc | Trạng thái |
|---|---|---|---|
| 16 | **Nền chung**: Cài đặt chung (API + đọc từ module khác), Thông báo (chuông lưu DB + SignalR, sự kiện `NotificationRequested`), Nhật ký hệ thống (xem / lọc), Thùng rác (khôi phục / xóa hẳn, job dọn). Email / Zalo / SMS mới là interface. Story [10](user-stories/10-he-thong-chung.md) | — | ✅ 2026-11-20 |
| 17 | **Ngân hàng câu hỏi**: kỹ năng, dạng câu, cấp độ, furigana, audio; nháp → chờ duyệt → đã duyệt → ngừng dùng; thống kê. Story [11](user-stories/11-ngan-hang-cau-hoi.md) | 16 | ✅ 2026-11-21 |
| 18 | **Tuyển sinh / Crm** (story 05 + HV-12): lead, Kanban, trùng, lịch sử tư vấn, CRM-07 lead → HV, phụ huynh, chia lead + SLA, lịch hẹn, test xếp lớp, CRM-19 tư vấn lên lớp, chiến dịch, mẫu tin nhắn, phễu. Phạm vi `own` thật cho Students | 16, 17 | ✅ 2026-11-21 |
| 19 | **Tài chính đợt 1** (story 08): bảng giá, ưu đãi, chính sách đóng; nghe `StudentEnrolled` / `EnrollmentEnded` sinh / hủy hóa đơn; thu tiền, hoàn / hủy (duyệt 2 người), đối soát VietQR, công nợ, nhắc phí; học phí ở cổng; dấu hiệu nợ quá hạn cho cảnh báo bỏ học | 16 (18 nếu cần ghi danh từ lead) | ✅ 2026-11-22 |
| 20 | **Nội dung học**: bài tập (upload / quiz), bài thi theo mốc (online / giấy), điểm, thông báo lớp, đánh giá GV, flashcard, tài liệu GV. Dấu hiệu bài tập / điểm thật cho cảnh báo bỏ học. Story [12](user-stories/12-noi-dung-hoc.md) | 17 | ✅ 2026-11-22 |
| 21 | **Nhân sự mở rộng + Công việc**: chấm công NV, đơn từ duyệt nhiều cấp, tuyển dụng, onboarding, task / Kanban, giao việc cấp dưới, xếp ca, bàn giao ca, trang cá nhân. Story [13](user-stories/13-cham-cong-don-tu-tuyen-dung.md), [14](user-stories/14-cong-viec-xep-ca-ban-giao.md) | 16 | ✅ 2026-11-23 |
| 22 | **Tài chính đợt 2 + tích hợp**: lương GV (DT-15.E5 khóa bảng lương), lương NV, hoa hồng / KPI Sale; Zalo OA, SMS, ngân hàng, Zoom / Meet. Story [08](user-stories/08-tai-chinh.md) TC-12 → 14, [15](user-stories/15-tich-hop.md) | 18, 19, 21 | ✅ 2026-11-24 |
| 23 | **Thư viện + nhóm HV riêng**: thư viện (watermark, chặn tải, thống kê), HV-15 chứng nhận, HV-16 JLPT, HV-17 du học / XKLĐ. Story [16](user-stories/16-thu-vien.md), [06](user-stories/06-hoc-vien.md) HV-15 → 17 | 16 | 🚧 phase 1 |
| 24 | **Báo cáo + bảng điều khiển** (Reporting, chỉ đọc): việc cần làm theo vai trò, chuyển đổi, doanh thu, công nợ, chuyên cần, lấp đầy phòng, so sánh chi nhánh, xuất Excel | tất cả | |
| 25 | **Sẵn sàng chạy thật**: bỏ chế độ mock, E2E Playwright luồng chính, rà bảo mật + hiệu năng, Docker 1 instance, backup Postgres, Seq, CI, hướng dẫn vận hành | tất cả | |

**Có thể đổi thứ tự:** 19 lên trước 18 nếu cần doanh thu sớm (chỉ thiếu ghi danh từ lead).
