# ADR 0004 — Báo cáo đọc số liệu qua Contracts, không dựng read model riêng

- Ngày: 2026-11-24
- Trạng thái: **Chấp nhận**

## Bối cảnh

Giai đoạn 24 làm module Reporting (user story [17](../user-stories/17-bao-cao.md)): bảng điều khiển, việc hôm nay theo vai trò,
báo cáo tuyển sinh / doanh thu / công nợ / đào tạo / so sánh chi nhánh, xuất Excel. Số liệu nằm ở 5 module (Crm, Students, Academic, Finance, System).

[database.md](../database.md) ghi dự kiến ban đầu: schema `reporting` chứa view / read model **dựng từ integration event**.
CLAUDE.md §3 cấm join bảng / dùng DbContext của module khác; §7 cho phép "raw SQL có tham số hoặc view trong schema `reporting`" cho báo cáo.

Quy mô: 1 trung tâm, vài chi nhánh, chạy 1 instance, không Redis. Dữ liệu cỡ chục nghìn dòng.

## Các phương án

| Phương án | Ưu | Nhược |
|---|---|---|
| **A. Mỗi module có `I…ReportSource` trong Contracts**, tự GROUP BY trong DbContext của mình; Reporting ghép + tính tỷ lệ | Giữ ranh giới module (quy tắc nghiệp vụ — tuổi nợ, SLA, trạng thái ghi danh — ở module sở hữu), số liệu luôn đúng hiện tại, không có dữ liệu thứ hai phải đồng bộ, test bằng DbContext thật như mọi module | Mỗi trang báo cáo gọi 5–6 truy vấn tổng hợp; nhiều nguồn cần thêm interface |
| B. View SQL trong schema `reporting` đọc thẳng bảng các schema khác | 1 truy vấn / số liệu, nhanh | Lặp lại quy tắc nghiệp vụ bằng SQL (tuổi nợ, phòng của buổi, trạng thái tiền…) → lệch với domain; đổi bảng module khác làm vỡ view mà ArchitectureTests không bắt được |
| C. Read model dựng từ integration event (bảng `reporting.*`) | Đọc rất nhanh, tách hẳn | Phải phát thêm hàng chục event, nạp lại lịch sử, xử lý event lỗi / trùng — quá nặng so với quy mô; số liệu trễ |

## Quyết định

1. **Phương án A.** Reporting **không có bảng, không có view, không có DbContext** ở giai đoạn này. Schema `reporting` để trống.
2. Module sở hữu dữ liệu khai báo interface đọc trong `<Module>.Contracts` (vd `ILeadReportSource`, `IRevenueReportSource`),
   trả **số thô** (đếm, tổng tiền, phút) hoặc dòng mỏng chỉ chiếu vài cột — không trả tỷ lệ. Lọc chi nhánh / khoảng ngày chạy trong DB;
   số liệu cần quy tắc tính khi đọc (công nợ `Invoice.Summarize`, tuổi nợ, SLA lead) đọc bản ghi lên rồi tính bằng chính domain của module đó.
3. Reporting (Domain) giữ phần tính toán thuần: kỳ báo cáo theo giờ VN, tỷ lệ / tăng trưởng, xếp hạng, so sánh chi nhánh, mức độ của việc hôm nay,
   ngưỡng cảnh báo. Unit test không cần DB.
4. **Excel ghi ở frontend** (`lib/spreadsheet.ts`), giống xuất nhật ký hệ thống (NK-02) — backend trả số liệu + ghi nhật ký. Không thêm thư viện.
5. Xem lại khi: 1 trang báo cáo > 1 giây ở dữ liệu thật, hoặc cần báo cáo theo kỳ đã đóng sổ (số liệu "đóng băng") — lúc đó chuyển phần nặng
   sang read model (C) cho riêng số liệu đó, viết ADR mới.

## Thư viện thêm

Không có.
