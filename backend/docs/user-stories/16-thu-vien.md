# 16 · Thư viện tài liệu điện tử

Module: **Library** · Bảng: `library.documents`, `library.document_views` ([database.md](../database.md)) · Giai đoạn 23
Frontend: `pages/staff/library/documents/LibraryPage.tsx`, `features/library/{types,constants,libraryStore}.ts`,
`features/library/{DocDetailDrawer,DocFormFields,DocViewer}.tsx`, `pages/student/documents/StudentDocumentsPage.tsx` (tab Thư viện)

## Mô hình (đọc trước)

```
LibraryDocument (sách, đề JLPT các năm, tự học, audio / video, link) — KHÔNG gắn lộ trình
 ├─ phân loại: category · fileType · levels[] · skills[] · tags[]
 ├─ ai xem: audience[] (student / teacher / staff) × branchIds[] (rỗng = mọi chi nhánh) × status (published / hidden)
 ├─ bảo vệ: watermark (mặc định BẬT) · allowDownload (mặc định TẮT)
 └─ DocumentView[]  mỗi lần mở trình xem = 1 lượt (chỉ thêm) ──▶ số liệu tổng hợp trên documents
```

**Quy tắc vàng:**
1. Thư viện ≠ tài liệu giảng dạy (Lộ trình › Chapter, story 09) ≠ tài liệu GV gửi lớp (story 12, ND-10).
2. Mặc định **có watermark, chặn tải**. Watermark in **tên + email / mã người xem + thời điểm mở** — ảnh chụp lộ ra ngoài truy được nguồn.
3. **Chặn tải là ở server**: tài liệu `allowDownload = false` chỉ trả link xem ngắn hạn (inline, không `attachment`), không có endpoint tải bản gốc.
4. Cổng chỉ thấy tài liệu `published`, đúng đối tượng (HV / PH → `student`, GV → `teacher`) và đúng chi nhánh của mình.
5. Số liệu (lượt xem, người đọc, phút đọc TB, 14 ngày, người đọc nhiều nhất) tính từ `document_views` — không sửa tay.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| TV-01 | Xem & lọc thư viện | Nhân viên có quyền | Cao |
| TV-02 | Thêm tài liệu (tải file / dán link) | Giáo vụ, quản lý | Cao |
| TV-03 | Sửa thông tin, ẩn / hiện, xóa tài liệu | Giáo vụ, quản lý | Cao |
| TV-04 | Đặt đối tượng xem & bảo vệ (watermark, chặn tải) | Giáo vụ, quản lý | Cao |
| TV-05 | Xem trực tuyến có watermark | Mọi người được xem | Cao |
| TV-06 | Thống kê lượt đọc | Giáo vụ, quản lý | TB |
| TV-07 | Thư viện trên cổng HV / PH | Học viên, phụ huynh | Cao |
| TV-08 | Thư viện trên cổng GV 🆕 | Giáo viên | TB |

**Quyền:** `library.documents` — `view` (TV-01, 05, 06), `create` (TV-02), `update` (TV-03, 04), `delete` (TV-03 xóa).
Phạm vi `branch` = tài liệu dùng cho mọi chi nhánh + tài liệu có chi nhánh của mình; `all` = tất cả. Cổng không cần mã quyền (lọc theo chủ tài khoản).

---

## TV-01 · Xem & lọc thư viện

> Là **nhân viên**, tôi muốn **tìm tài liệu theo cấp độ, kỹ năng, dạng tệp, đối tượng, từ khóa** để **giới thiệu đúng tài liệu cho HV / GV**.

**Nguồn:** Thư viện › Thư viện tài liệu (`LibraryPage.tsx`) · **Quyền cần:** `library.documents.view`

**Các bước:**
1. Lưới bìa hoặc bảng: tên, phụ đề tiếng Nhật, nhóm, dạng tệp, cấp độ, đối tượng, bảo vệ, lượt xem.
2. Lọc: chi nhánh, nhóm, cấp độ, kỹ năng, dạng tệp, đối tượng, từ khóa (tên, phụ đề, thẻ, NXB), "hiện cả tài liệu ẩn".
3. Thẻ tổng: số tài liệu đang hiển thị, đang chặn tải, tổng lượt xem 30 ngày.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TV-01.E1 | Phạm vi `branch` | Không thấy tài liệu chỉ dành cho chi nhánh khác |
| TV-01.E2 | Tài liệu ẩn | Chỉ hiện khi bật "hiện cả tài liệu ẩn" |

---

## TV-02 · Thêm tài liệu

> Là **giáo vụ**, tôi muốn **tải lên sách / đề / file nghe hoặc dán link** để **đưa vào thư viện dùng chung**.

**Nguồn:** `LibraryPage.tsx` › Thêm tài liệu (`DocFormFields.tsx`) · **Quyền cần:** `library.documents.create`

**Các bước:**
1. Tải tệp (PDF ≤ 100 MB, audio / video ≤ 500 MB — kho file S3, thư mục `library/documents`) **hoặc** dán link (`https://`).
2. Nhập: tên*, phụ đề, mô tả, nhóm*, cấp độ, kỹ năng, thẻ, NXB, năm, số trang / số phút, đối tượng*, chi nhánh (rỗng = tất cả).
3. Mặc định `watermark = true`, `allowDownload = false`, `published`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TV-02.E1 | Thiếu tên / tên > 200 ký tự | `library.document.title_required` / `library.document.title_too_long` |
| TV-02.E2 | Chưa chọn đối tượng | `library.document.audience_required` |
| TV-02.E3 | Dạng `link` mà không phải `http(s)://`; dạng tệp mà chưa tải tệp | `library.document.invalid_link` / `library.document.file_required` |
| TV-02.E4 | Tệp sai định dạng so với dạng đã chọn (PDF / audio / video) | `library.document.file_type_mismatch` |
| TV-02.E5 | Năm ngoài 1950 → năm sau, số trang / phút ≤ 0 | `library.document.invalid_meta` |
| TV-02.E6 | Thẻ | Bỏ trùng, cắt khoảng trắng, tối đa 20 thẻ × 30 ký tự |

---

## TV-03 · Sửa, ẩn / hiện, xóa

> Là **giáo vụ**, tôi muốn **sửa thông tin, tạm ẩn hoặc xóa tài liệu** để **thư viện luôn đúng và gọn**.

**Quyền cần:** `library.documents.update` (sửa, ẩn / hiện) · `library.documents.delete` (xóa)

**Các bước:** Sửa giống TV-02 (đổi tệp = tải tệp mới; số liệu đọc giữ nguyên). Ẩn → cổng không thấy, ai đang mở link ngắn hạn thì hết hạn tự nhiên.
Xóa → **xóa mềm** vào Thùng rác (story 10, TR-xx); dọn Thùng rác mới xóa tệp trong kho.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TV-03.E1 | Ẩn tài liệu đã ẩn / hiện tài liệu đang hiện | Không lỗi, không đổi gì |
| TV-03.E2 | Sửa đè (2 người cùng sửa) | `concurrency` (xmin) |

---

## TV-04 · Đối tượng xem & bảo vệ

> Là **quản lý**, tôi muốn **chọn ai xem được và bật / tắt watermark, cho / chặn tải** để **giữ bản quyền tài liệu mua**.

**Nguồn:** `DocDetailDrawer.tsx` › tab Quyền truy cập & bảo vệ · **Quyền cần:** `library.documents.update`

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TV-04.E1 | Bỏ hết đối tượng | `library.document.audience_required` |
| TV-04.E2 | Bật "Cho tải về" | Cho, cảnh báo "Người xem tải được bản gốc — không còn watermark trên file tải về"; ghi nhật ký `warning` |
| TV-04.E3 | Dạng `link` | Watermark / chặn tải không áp dụng (trang ngoài) — giữ giá trị nhưng giao diện ghi rõ |

---

## TV-05 · Xem trực tuyến có watermark

> Là **người được xem**, tôi muốn **đọc / nghe tài liệu ngay trên trình duyệt** để **không phải tải về**.

**Nguồn:** `DocViewer.tsx` (trong `DocDetailDrawer`, cổng HV) · **Quyền cần:** `library.documents.view` hoặc tài khoản cổng đúng đối tượng

**Các bước:**
1. Mở → server kiểm quyền xem (TV-07, TV-08 với cổng) → ghi `document_views` (bắt đầu) → trả `{ viewId, url (ký 10 phút, inline), watermark: "<tên> · <email / mã> · <dd/MM/yyyy HH:mm>", allowDownload }`.
2. Đóng trình xem → `POST views/{viewId}/end` → lưu số phút đọc (giới hạn 0 → 240 phút / lượt).
3. `allowDownload = true` → thêm `downloadUrl` (ký 10 phút, `attachment`).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TV-05.E1 | Tài liệu ẩn / không đúng đối tượng / khác chi nhánh | `library.document.not_found` (không lộ là có tài liệu) |
| TV-05.E2 | Mở lại trong cùng 1 phút | Vẫn ghi lượt mới (frontend tự chặn đếm đôi) |
| TV-05.E3 | Kết thúc lượt xem 2 lần / lượt của người khác | Lần sau bỏ qua / `library.view.not_found` |
| TV-05.E4 | Không gọi kết thúc (đóng tab) | Lượt giữ 0 phút, không tính vào phút đọc TB |

---

## TV-06 · Thống kê lượt đọc

> Là **quản lý**, tôi muốn **biết tài liệu nào được đọc nhiều, ai đọc** để **mua thêm / bỏ bớt tài liệu**.

**Nguồn:** `DocDetailDrawer.tsx` › tab Thống kê · **Quyền cần:** `library.documents.view`

- Lượt xem, số người đọc (khác nhau theo loại + id người xem), phút đọc TB / lượt (chỉ lượt có kết thúc), lần xem cuối.
- Biểu đồ **14 ngày** gần nhất (giờ Việt Nam, cũ → mới). Top **6** người đọc nhiều nhất (tên, vai trò, số lượt).
- Cột tổng hợp trên `documents` cập nhật mỗi khi bắt đầu / kết thúc lượt xem (cùng transaction).

---

## TV-07 · Thư viện trên cổng HV / PH

> Là **học viên**, tôi muốn **xem thư viện của trung tâm, tài liệu đúng trình độ lên trước** để **tự học thêm**.

**Nguồn:** Cổng HV › Tài liệu › tab Thư viện (`StudentDocumentsPage.tsx`)

- Chỉ tài liệu `published`, có đối tượng `student`, dành cho mọi chi nhánh hoặc chi nhánh của **lớp đang học** (ghi danh có hiệu lực — qua `Students.Contracts`).
- Sắp xếp: trùng cấp độ lớp đang học lên trước, rồi mới nhất. Lọc nhóm, từ khóa.
- Phụ huynh xem thay con (chọn con) — lượt xem ghi `viewer_type = guardian`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TV-07.E1 | HV không còn lớp nào đang học | Chỉ thấy tài liệu dành cho mọi chi nhánh |
| TV-07.E2 | Không trả số liệu thống kê, người tải lên, đối tượng, chi nhánh | Ẩn thông tin nội bộ ở cổng |

---

## TV-08 · Thư viện trên cổng GV 🆕

> Là **giáo viên**, tôi muốn **xem tài liệu dành cho GV** để **soạn bài**.

**Nguồn:** Cổng GV › Tài liệu của tôi › thêm tab "Thư viện trung tâm" (dựng ở giai đoạn 23 phase 4)

- Tài liệu `published`, đối tượng `teacher`, chi nhánh của lớp GV đang dạy hoặc mọi chi nhánh. Lượt xem `viewer_type = staff`.

---

## Dữ liệu / API

| Story | API (`/api/v1/library/...`) | Quyền |
|---|---|---|
| TV-01 | `GET documents?branchId=&category=&level=&skill=&fileType=&audience=&q=&includeHidden=` | `library.documents.view` |
| TV-02 | `POST documents/files` (form-data `file`) → `{ fileKey, fileName, size }` · `POST documents` | `library.documents.create` |
| TV-03 | `PUT documents/{id}` `{ …, version }` · `POST documents/{id}/hide\|publish` · `DELETE documents/{id}` | `update` / `delete` |
| TV-04 | `PUT documents/{id}/access` `{ audience, branchIds, watermark, allowDownload, version }` | `library.documents.update` |
| TV-05 | `POST documents/{id}/views` → `{ viewId, url, downloadUrl?, watermark }` · `POST views/{viewId}/end` | `library.documents.view` |
| TV-06 | `GET documents/{id}/stats` → `{ views, readers, avgMinutes, lastViewedAt, daily[14], top[] }` | `library.documents.view` |
| TV-07 | `GET portal/learning/library?studentId=` · `POST portal/learning/library/{id}/views?studentId=` · `POST portal/learning/library/views/{viewId}/end` | cổng HV / PH |
| TV-08 | `GET portal/teaching/library` · `POST portal/teaching/library/{id}/views` · `POST portal/teaching/library/views/{viewId}/end` | cổng GV |

- JSON khớp `frontend/src/features/library/types.ts` (`LibraryDoc`, `stats`); `branchIds: 'all'` ở frontend ↔ mảng rỗng ở API.
- **Mã lỗi:** `library.document.{not_found, title_required, title_too_long, audience_required, invalid_link, file_required, file_type_mismatch, invalid_meta}`, `library.view.not_found`.
- **Cần từ module khác:** `Students.Contracts` (chi nhánh + cấp độ lớp đang học của HV), `Academic.Contracts` (chi nhánh lớp GV dạy).

## Phạm vi giai đoạn 23 (chốt 2026-11-24)

- **Phase 1:** story này + chỉnh HV-15 → 17 ([06](06-hoc-vien.md)); domain `LibraryDocument` (tạo / sửa / bảo vệ / ai xem được), `DocumentView` (bắt đầu / kết thúc, giới hạn phút),
  `LibraryStats` (tổng hợp, 14 ngày, top 6), `Watermark`; Students: điều kiện + mã chứng nhận, quy chế điểm JLPT, bước hồ sơ du học.
- **Phase 2:** cấu hình EF + migration `library.documents`, `document_views`, các bảng chứng nhận / JLPT / du học + dữ liệu mẫu từ mock.
- **Phase 3:** API trên + job không cần (số liệu cập nhật khi ghi lượt xem). **Phase 4:** nối frontend, dọn mock.
