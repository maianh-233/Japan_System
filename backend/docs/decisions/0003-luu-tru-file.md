# ADR 0003 — Lưu trữ file: kho S3-compatible (local SeaweedFS, thật Cloudflare R2)

- Ngày: 2026-11-17
- Trạng thái: **Chấp nhận**

## Bối cảnh

Trung tâm tải lên tài liệu giảng dạy (lộ trình — user story 09), sau này còn bài nộp, ảnh hồ sơ, giấy tờ JLPT / du học.
Yêu cầu: **miễn phí** ở quy mô dự án cá nhân, file riêng tư (chỉ người có quyền xem), không tốn băng thông server, đổi nhà cung cấp không phải sửa code.

CLAUDE.md ban đầu ghi "MinIO local". MinIO đã **ngừng phát hành image Docker cho bản cộng đồng** (repo `minio/minio` trên Docker Hub không còn tag) → cần thay.

## Các phương án

| Phương án | Miễn phí | Ưu | Nhược |
|---|---|---|---|
| **Cloudflare R2** | 10 GB lưu trữ / tháng, 1 triệu thao tác ghi, 10 triệu thao tác đọc, **không tính phí tải xuống** | S3-compatible, nhanh ở VN (mạng Cloudflare), tải xuống bao nhiêu cũng 0đ | Cần tài khoản Cloudflare (có thể phải thêm thẻ khi bật R2) |
| Backblaze B2 | 10 GB, tải xuống miễn phí tới 3× dung lượng lưu / tháng | S3-compatible, rẻ khi lớn | Giới hạn tải xuống; máy chủ ở Mỹ / EU → chậm hơn |
| Supabase Storage | 1 GB | Có sẵn nếu dùng Supabase | Quá ít; API riêng |
| Google Drive API | 15 GB (chung tài khoản) | Quen thuộc | Không phải kho file cho ứng dụng: hạn mức API, không có link ký sẵn, quyền rối |
| Lưu đĩa server | 0đ | Đơn giản | Mất file khi đổi / hỏng server, tự viết link tạm, tốn băng thông server |

## Quyết định

1. **Một interface** `IFileStorage` (Application.Common/Files) cho mọi module; **một bản chạy** `S3FileStorage` (Infrastructure.Common/Files)
   dùng giao thức S3 → chạy được với R2, B2, AWS S3, SeaweedFS chỉ bằng đổi cấu hình `Storage:*`.
2. **Môi trường thật: Cloudflare R2** (bucket riêng tư). **Local / test: SeaweedFS** `chrislusf/seaweedfs:4.51` trong docker-compose và Testcontainers.
3. **Bucket riêng tư + link ký sẵn có hạn** (mặc định 15 phút): server kiểm quyền rồi cấp link; trình duyệt lấy file thẳng từ kho (không tốn băng thông API).
4. **Tải lên đi qua API** (không cho trình duyệt PUT thẳng lên kho) — chạy 1 instance, file ≤ 50 MB, đổi lại kiểm được **magic bytes**
   (chặn đổi đuôi) trước khi lưu. Tên lưu ngẫu nhiên `{module}/{thư mục}/{yyyy}/{MM}/{guid v7}.{đuôi}`, tên gốc chỉ để hiển thị.
5. **Video không tải lên** — dán link (YouTube không công khai / Google Drive); tiết kiệm 10 GB cho tài liệu. Đã đúng với giao diện (`MaterialType.video` là loại link).
6. Quy định từng nhu cầu = `FilePolicy` (loại file + dung lượng): `TeachingMaterials` 50 MB · `Submissions` 20 MB · `Documents` 10 MB · `Images` 5 MB.

## Thư viện thêm

| Gói | Dùng cho | License |
|---|---|---|
| AWSSDK.S3 4.0.104.1 | Gọi API S3 (R2 / SeaweedFS) | Apache-2.0 |

Testcontainers dùng `ContainerBuilder` chung (không thêm gói `Testcontainers.Minio`).

## Hệ quả

- Cấu hình R2 cho SDK v4: `ForcePathStyle`, `Region=auto`, checksum `WHEN_REQUIRED`, tắt chunk encoding (đã nằm trong `S3FileStorage`).
- Hết 10 GB: R2 tính ~0,015 USD / GB / tháng (giá tham khảo 11/2026 — kiểm lại trang giá Cloudflare).
- Muốn đổi sang B2 / S3: chỉ đổi `Storage__ServiceUrl`, khóa, bucket. Chuyển dữ liệu cũ bằng `rclone`.
- Xem PDF bằng thư viện JS (pdf.js) phía frontend cần bật CORS cho bucket R2 (`GET` từ domain frontend) — làm khi dựng trình xem.
- Xóa file: chỉ khi **xóa cứng** entity (dọn Thùng rác); xóa mềm giữ file để khôi phục.
