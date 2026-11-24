# 15 · Tích hợp dịch vụ ngoài (Zalo OA, SMS, email, ngân hàng, Zoom / Meet)

Module: **System** (cấu hình + nhật ký) · Bảng: `system.integrations`, `system.integration_logs` ([database.md](../database.md))
Frontend: `pages/staff/system/integrations/IntegrationsPage.tsx`, `features/system/{systemStore,types,constants}.ts` · **Quyền:** `system.integrations` (`view`, `update`)
Người gọi tích hợp: Finance (nhắc học phí — TC-11, webhook ngân hàng — TC-10), Academic (báo vắng, đổi lịch, phòng học online), Crm (mẫu tin nhắn), System (OTP, thư mời)

## Mô hình (đọc trước)

```
Integration (1 dòng / dịch vụ, khóa cố định)  ── config (bí mật mã hóa) · events (bật / tắt từng việc) · webhook_url
   │  Connect(config)   → connected        Disconnect() → disconnected, xóa cấu hình, tắt hết việc
   │  RecordCall(ok)    → integration_logs (chỉ thêm) · lỗi xác thực → error + last_error
   └─ Module khác KHÔNG đọc bảng này — gọi System.Contracts:
        IMessageChannels.SendAsync(eventKey, người nhận, nội dung)   Zalo OA → (lỗi / chưa quan tâm OA) → SMS
        IMeetingRooms.CreateAsync(lớp online)                          Zoom → (chưa kết nối) → Google Meet
        IIntegrationLog.WriteAsync(key, ok, message)                   webhook ngân hàng ghi nhật ký
```

| Khóa | Tên | Nhóm | Trường cấu hình (* bí mật) | Việc (event) |
|---|---|---|---|---|
| `zalo-oa` | Zalo OA (ZNS) | notify | oaId, appId, secret* | attendance, tuition, schedule, result |
| `sms` | SMS Brandname | notify | brandname, apiKey* | otp, fallback |
| `email` | Email (SMTP) | notify | host, port, user, pass* | invoice, invite, test |
| `vietqr` | VietQR + webhook ngân hàng | payment | clientId, apiKey*, checksum* | qr, autoMatch |
| `zoom` | Zoom | meeting | accountId, clientId, clientSecret* | autoRoom, record |
| `meet` | Google Meet | meeting | domain | autoRoom |
| `fb-lead`, `web-form`, `tiktok-lead` | Nguồn lead | lead | (như frontend) | import, dedupe / captcha |

**Quy tắc vàng:**
1. Khóa bí mật **mã hóa** khi lưu (ASP.NET Data Protection), API chỉ trả 4 ký tự cuối (`••••••abcd`). Kết nối lại mà để trống trường bí mật = **giữ khóa cũ**.
2. Mọi lần gửi / nhận ghi `integration_logs` (không ghi nội dung nhạy cảm: OTP che số, chỉ ghi 4 số cuối SĐT). Mọi lần sửa cấu hình ghi Nhật ký hệ thống `category = setting`.
3. **Chi phí:** dự án cá nhân dùng gói miễn phí. Zalo ZNS và SMS Brandname tính tiền theo tin → mặc định **chế độ thử (sandbox)**: không gửi thật,
   chỉ ghi nhật ký "Đã gửi (thử)". Bật gửi thật bằng cấu hình `Integrations:<key>:Live = true` khi trung tâm có tài khoản. Zoom (Server-to-Server OAuth,
   tài khoản miễn phí), email (SMTP) và webhook ngân hàng (SePay / Casso gói miễn phí) chạy thật.
4. Tích hợp lỗi không làm hỏng nghiệp vụ gốc: thu tiền, điểm danh, tạo lớp vẫn thành công; việc gửi chạy nền (Outbox) và thử lại.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| TH-01 | Xem danh sách tích hợp & tình trạng | Admin | Cao |
| TH-02 | Kết nối / kết nối lại / ngắt | Admin | Cao |
| TH-03 | Bật / tắt từng việc dùng kênh | Admin | TB |
| TH-04 | Kiểm tra kết nối | Admin | TB |
| TH-05 | Nhật ký tích hợp | Admin | TB |
| TH-06 | Gửi tin Zalo OA, dự phòng SMS | Hệ thống | Cao |
| TH-07 | Webhook ngân hàng theo cấu hình tích hợp | Hệ thống | Cao |
| TH-08 | Tự tạo phòng học online (Zoom / Meet) | Hệ thống, giáo vụ | TB |

---

## TH-01 · Xem danh sách tích hợp

> Là **admin**, tôi muốn **thấy các dịch vụ đã kết nối, lỗi gần nhất và số lượt 30 ngày** để **biết kênh nào đang hỏng**.

**Các bước:** mở Hệ thống › Tích hợp → thẻ theo nhóm (thông báo / thanh toán / học online / nguồn lead): trạng thái, tài khoản, lần đồng bộ cuối,
lượt dùng 30 ngày (**đếm từ `integration_logs`**, không lưu số), số lỗi. Tích hợp `error` hiện băng cảnh báo + nút "Kết nối lại".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TH-01.E1 | Không có `system.integrations.update` | Chỉ xem, ẩn nút kết nối / ngắt / sửa |
| TH-01.E2 | Trường bí mật | Luôn che, chỉ 4 ký tự cuối |

## TH-02 · Kết nối / kết nối lại / ngắt

**Các bước:** "Kết nối" → nhập các trường theo danh mục → lưu: trạng thái `connected`, ghi người / lúc kết nối, xóa `last_error`.
"Ngắt kết nối" → xác nhận (liệt kê việc sẽ dừng) → `disconnected`, xóa cấu hình, tắt hết việc.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TH-02.E1 | Thiếu trường bắt buộc | `system.integration.missing_field` (tên trường) |
| TH-02.E2 | Kết nối lại, trường bí mật để trống | Giữ khóa cũ; chưa từng có khóa → E1 |
| TH-02.E3 | Khóa không có trong danh mục | `system.integration.unknown` |
| TH-02.E4 | Trường lạ ngoài danh mục | Bỏ qua (không lưu) |

## TH-03 · Bật / tắt việc

Mỗi tích hợp có danh sách việc cố định (bảng trên). Chỉ bật được khi đang `connected` (`system.integration.not_connected`); việc lạ → `system.integration.unknown_event`.
Tắt việc → module gọi nhận "kênh tắt" và bỏ qua (TH-06 chuyển kênh dự phòng nếu có).

## TH-04 · Kiểm tra kết nối

"Kiểm tra" → gọi thử dịch vụ (Zalo: lấy thông tin OA · SMS: số dư · SMTP: đăng nhập · Zoom: lấy token · VietQR: ký thử checksum) → ghi nhật ký, cập nhật `last_sync_at`.
Lỗi xác thực (401 / sai khóa) → `error` + `last_error`; lỗi tạm (timeout) → chỉ ghi nhật ký, giữ trạng thái. Chế độ thử: luôn thành công, ghi "(thử)".

## TH-05 · Nhật ký tích hợp

Ngăn chi tiết: 50 dòng mới nhất của tích hợp (lúc, thành công / lỗi, nội dung). Chỉ thêm; job hằng đêm xóa dòng cũ hơn **180 ngày** (`IntegrationLog.RetentionDays`).

## TH-06 · Gửi tin Zalo OA, dự phòng SMS

> Là **hệ thống**, tôi muốn **gửi báo vắng / nhắc học phí / đổi lịch qua Zalo, lỗi thì gửi SMS** để **phụ huynh luôn nhận được tin**.

**Chọn kênh** (`ChannelPlan` — domain): Zalo nếu `zalo-oa` connected + việc bật → gửi lỗi, hoặc Zalo không chạy (chưa kết nối / đang lỗi) →
SMS nếu `sms` connected + việc `fallback` bật → không có kênh nào → ghi nhật ký "Không có kênh gửi" (không lỗi nghiệp vụ).
Zalo đang chạy mà admin **tắt** việc đó = không muốn gửi tin này → không gửi SMS thay. OTP luôn đi SMS (việc `otp`), không qua Zalo.
Email theo việc riêng (`invoice`, `invite`, `test`) — không dự phòng chéo.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TH-06.E1 | Người nhận chưa quan tâm OA / không có Zalo | Ghi lỗi Zalo → gửi SMS (nếu bật fallback) |
| TH-06.E2 | Không có SĐT | Bỏ qua, ghi nhật ký |
| TH-06.E3 | Lỗi xác thực khi gửi | Tích hợp chuyển `error` (TH-04) + chuyển kênh dự phòng |

## TH-07 · Webhook ngân hàng

Webhook `POST /api/v1/finance/integrations/bank/transactions` (TC-10) đọc khóa ký từ cấu hình `vietqr.checksum` (thay cho `Finance:BankWebhookSecret`; còn giữ khóa cũ làm dự phòng khi
chưa cấu hình). Việc `autoMatch` tắt → giao dịch vào trạng thái chưa khớp, chờ đối soát tay. Mỗi giao dịch nhận ghi 1 dòng nhật ký `vietqr` (số tiền, mã hóa đơn khớp / không khớp).
`vietqr` chưa kết nối → webhook trả 503 `system.integration.not_connected`.

## TH-08 · Phòng học online

> Là **giáo vụ**, tôi muốn **lớp online tự có link phòng** để **không phải tạo tay trên Zoom**.

Khi tạo / sửa lớp `online = true` mà `meeting_url` trống và việc `autoRoom` bật → tạo **1 phòng định kỳ cho cả lớp** (Zoom recurring meeting không giờ cố định;
Meet: 1 space) → ghi vào `academic.classes.meeting_url`. Zoom chưa kết nối / lỗi → thử Meet → không có → để trống, lớp hiện "Chưa có link phòng".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TH-08.E1 | Giáo vụ đã nhập link tay | Không tạo phòng, giữ link tay |
| TH-08.E2 | Tạo phòng lỗi | Lớp vẫn lưu; ghi nhật ký; nút "Tạo lại link" ở chi tiết lớp |

---

## Dữ liệu / API

| Story | API (`/api/v1/system/...`) | Quyền |
|---|---|---|
| TH-01, 05 | `GET integrations` (kèm `usage` 30 ngày) · `GET integrations/{key}/logs` | `system.integrations.view` |
| TH-02 | `PUT integrations/{key}/connection` `{ config }` · `DELETE integrations/{key}/connection` | `system.integrations.update` |
| TH-03 | `PUT integrations/{key}/events/{event}` `{ on }` | `system.integrations.update` |
| TH-04 | `POST integrations/{key}/test` | `system.integrations.update` |
| TH-08 | `POST academic/classes/{id}/meeting-room` (tạo lại link) | `academic.classes.update` |

- JSON khớp `frontend/src/features/system/types.ts › Integration` — `id` = khóa (`zalo-oa`...), `fields` từ danh mục, `config` đã che.
- **Domain (giai đoạn 22 phase 1):** `IntegrationCatalog` (trường bắt buộc / bí mật / việc theo khóa), `Integration.Connect` (giữ bí mật cũ, bỏ trường lạ),
  `Disconnect` (xóa cấu hình, tắt việc), `SetEvent`, `RecordCall` (lỗi xác thực → `error`), `ChannelPlan` (Zalo → SMS, OTP chỉ SMS).
