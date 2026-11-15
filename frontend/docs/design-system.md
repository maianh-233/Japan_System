# Design System — JP Center ERP

> Mục tiêu: **nhẹ mắt khi dùng cả ngày**, hiện đại, rõ ràng, thao tác nhanh trên cả máy tính, máy tính bảng và điện thoại.
> Nguồn sự thật của màu/token trong code: `src/config/theme.ts` (AntD) và `src/assets/styles/index.css` (Tailwind `@theme`). Sửa màu → sửa **cả hai** nơi.

---

## 1. Màu sắc

### 1.1 Màu chủ đạo — Xanh dương nhẹ (Primary)

| Token | Hex | Dùng cho |
|---|---|---|
| `primary-50`  | `#F3F8FE` | Nền trang phụ, hàng hover trong bảng |
| `primary-100` | `#EAF4FF` | Nền nút phụ (secondary), nền tag, nền menu đang chọn |
| `primary-200` | `#CFE4FB` | Viền nhẹ, divider có màu |
| `primary-300` | `#A3CBF5` | Viền focus mờ, icon trang trí |
| `primary-400` | `#5FA2E8` | Hover viền input |
| `primary-500` | `#2B7FD9` | Hover nút chính, biểu đồ, viền focus — **không dùng cho chữ nhỏ trên nền trắng** |
| `primary-600` | `#1F6FC5` | **Màu chính**: nút primary, link, input focus, tab active (tương phản 5.07:1 trên trắng ✔) |
| `primary-700` | `#1A5FAD` | Nút primary khi nhấn (active), chữ trên nền `primary-100` (5.75:1 ✔) |
| `primary-800` | `#174C88` | Tiêu đề nhấn mạnh |
| `primary-900` | `#143D6B` | Hiếm dùng |

### 1.2 Màu trung tính (Neutral — tông slate hơi ngả xanh)

| Token | Hex | Dùng cho |
|---|---|---|
| `bg-page`      | `#F5F9FF` | Nền toàn trang (xanh rất nhạt, đỡ chói hơn trắng tinh) |
| `bg-surface`   | `#FFFFFF` | Card, bảng, modal, input |
| `border`       | `#E2E8F0` | Viền card, input, bảng |
| `border-strong`| `#CBD5E1` | Viền input khi hover |
| `text`         | `#1E293B` | Chữ chính (13.85:1 trên `bg-page` ✔) |
| `text-secondary`| `#475569` | Nhãn, mô tả (7.58:1 ✔) |
| `text-muted`   | `#64748B` | Gợi ý, chú thích, số trang (4.76:1 ✔) |
| `text-placeholder` | `#94A3B8` | **Chỉ** placeholder & icon mờ — không dùng cho nội dung cần đọc |
| `disabled-bg`  | `#F1F5F9` | Nền control bị vô hiệu |

### 1.3 Màu ngữ nghĩa (Semantic)

| Ý nghĩa | Chữ / icon | Nền nhạt | Dùng cho |
|---|---|---|---|
| Success | `#15803D` | `#ECFDF3` | Đã thanh toán, có mặt, đã duyệt |
| Warning | `#B45309` | `#FFF8EB` | Sắp đến hạn, chờ duyệt, nợ một phần |
| Error   | `#DC2626` | `#FEF2F2` | Lỗi, quá hạn, vắng không phép, hủy |
| Info    | `#1F6FC5` | `#EAF4FF` | Thông tin, đang diễn ra |

> Không bao giờ truyền đạt trạng thái **chỉ bằng màu** — luôn kèm icon hoặc chữ (ví dụ tag "✓ Đã thanh toán").

### 1.4 Màu trạng thái nghiệp vụ (dùng cho Tag/Badge)

| Nhóm | Trạng thái → màu |
|---|---|
| Lead | Mới = info · Đang tư vấn = primary · Hẹn test = warning · Chốt = success · Mất = neutral |
| Lớp / buổi học | Sắp mở = info · Đang học = success · Tạm dừng = warning · Đã hủy = error · Kết thúc = neutral |
| Hóa đơn | Chưa thu = warning · Thu một phần = info · Đã thu = success · Quá hạn = error · Hoàn tiền = neutral |
| Đơn từ | Chờ duyệt = warning · Đã duyệt = success · Từ chối = error · Đã rút = neutral |
| Điểm danh | Có mặt = success · Đi muộn = warning · Vắng có phép = info · Vắng không phép = error |

---

## 2. Typography

| Mục | Giá trị |
|---|---|
| Font chính | **Be Vietnam Pro** — thiết kế riêng cho tiếng Việt, dấu rõ, hiện đại |
| Font tiếng Nhật | **Noto Sans JP** — tự fallback khi gặp kana/kanji |
| Font số liệu | `font-variant-numeric: tabular-nums` cho bảng, tiền, giờ (các số thẳng cột) |
| Stack | `"Be Vietnam Pro", "Noto Sans JP", system-ui, -apple-system, "Segoe UI", sans-serif` |

| Cấp | Cỡ / line-height | Độ đậm | Dùng cho |
|---|---|---|---|
| Display | 28 / 36 | 600 | Tiêu đề dashboard |
| H1 | 24 / 32 | 600 | Tiêu đề trang |
| H2 | 20 / 28 | 600 | Tiêu đề card/section |
| H3 | 16 / 24 | 600 | Tiêu đề nhóm form |
| Body | **14 / 22** | 400 | Nội dung mặc định (mobile: 15/24 cho dễ đọc) |
| Small | 13 / 20 | 400 | Nhãn phụ, chú thích |
| Caption | 12 / 16 | 500 | Badge, metadata |

Quy tắc: tối đa 2 độ đậm trên một màn hình (400 & 600); không viết HOA cả câu; dòng văn bản dài tối đa ~75 ký tự.

---

## 3. Spacing, bo góc, đổ bóng

- **Lưới 4px**: 4 · 8 · 12 · 16 · 20 · 24 · 32 · 40 · 48.
- Padding card: 16 (mobile) / 24 (desktop). Khoảng cách giữa các field trong form: 16.
- **Bo góc**: control (button, input) `8px` · card/modal `12px` · tag `6px` · avatar tròn.
- **Đổ bóng** (nhẹ, ngả xanh):
  - `shadow-sm`: `0 1px 2px rgba(15, 40, 80, .06)` — card, input focus
  - `shadow-md`: `0 4px 12px rgba(15, 40, 80, .08)` — dropdown, popover
  - `shadow-lg`: `0 12px 32px rgba(15, 40, 80, .12)` — modal, drawer
- **Focus ring**: `0 0 0 3px rgba(43, 127, 217, .25)` — luôn hiển thị khi điều hướng bàn phím.

---

## 4. Phong cách thiết kế

1. **Sạch, thoáng, ít trang trí.** Nền xanh rất nhạt + card trắng viền mảnh; không gradient, không bóng đậm.
2. **Một hành động chính mỗi vùng.** Mỗi trang/card chỉ có tối đa 1 nút `primary`; còn lại là `secondary`/`outline`/`ghost`.
3. **Nút luôn có icon** (bắt buộc ở mức kiểu TypeScript). Icon đứng trước chữ; nút chỉ có icon bắt buộc có `aria-label` + tooltip.
4. **Icon**: `lucide-react`, nét 2px, cỡ 16 (control md), 14 (sm), 18 (lg), 20 (menu). Dùng nhất quán một bộ icon cho hành động: Thêm `Plus`, Sửa `Pencil`, Xóa `Trash2`, Lưu `Save`, Tìm `Search`, Lọc `Filter`, Xuất `Download`, Nhập `Upload`, Làm mới `RotateCcw`, Xem `Eye`.
5. **Trạng thái đầy đủ cho mọi control**: default · hover · focus (ring) · active · disabled · loading · error.
6. **Phản hồi tức thì**: lưu thành công → toast; thao tác nguy hiểm (xóa, hủy lớp) → hộp xác nhận nói rõ hậu quả.
7. **Trạng thái rỗng / đang tải / lỗi** có thiết kế riêng, không để màn hình trống.
8. **Chuyển động** 150–200ms, `ease-out`; tôn trọng `prefers-reduced-motion`.
9. **Ngôn ngữ**: tiếng Việt ngắn gọn, động từ ở đầu nút ("Lưu học viên", không "OK").

---

## 5. Responsive

| Breakpoint | Độ rộng | Thiết bị | Bố cục |
|---|---|---|---|
| `xs` | < 640 | Điện thoại | Sidebar thành drawer; form 1 cột; bảng → danh sách thẻ hoặc cuộn ngang; nút chính có thể dính đáy |
| `sm` | ≥ 640 | Điện thoại ngang | Form 1 cột; thanh tìm kiếm xếp dọc |
| `md` | ≥ 768 | Tablet | Form 2 cột; sidebar thu gọn (chỉ icon) |
| `lg` | ≥ 1024 | Laptop | Sidebar đầy đủ; bảng đầy đủ cột |
| `xl` | ≥ 1280 | Desktop | Nội dung tối đa `1440px`, căn giữa |

Quy tắc:
- **Mobile-first**: viết style cho màn nhỏ trước, mở rộng bằng `sm:` `md:` `lg:`.
- **Vùng chạm tối thiểu 40px** (mục tiêu 44px) trên mobile — control `md` cao 40px, `lg` cao 44px.
- Lề trang: 16px (mobile) · 24px (tablet+). Không bao giờ để trang cuộn ngang.
- Input font ≥ 16px trên iOS để tránh tự zoom (đã xử lý trong CSS).
- Nút có thể `collapseOnMobile`: chỉ còn icon trên màn < 640px (tự thêm tooltip + aria-label).
- Phân trang chuyển sang dạng rút gọn `‹ 3 / 12 ›` trên mobile.

---

## 6. Bộ component dùng chung (`src/components/common`)

Import tất cả từ một chỗ: `import { Button, DataTable, ... } from '@/components/common';`
Xem thử trực tiếp: chạy `npm run dev` → trang demo (có cả **trang mẫu "Quản lý học viên"** ghép đủ component).

| Nhóm | Component | Ghi chú |
|---|---|---|
| Hành động | `Button` | `action` → màu/icon/nhãn theo chức năng; icon bắt buộc; `collapseOnMobile` |
| Nhập liệu | `TextInput` `NumberInput` `TextArea` | Dùng độc lập (label/hint/error) hoặc trong `Form.Item` |
| | `Select` | Tìm không phân biệt dấu, chọn nhiều |
| | `DateInput` `DateRangeInput` `TimeInput` `TimeRangeInput` | dd/mm/yyyy, HH:mm; khoảng ngày tách 2 ô trên mobile |
| | `ImagePicker` | Ảnh đại diện / logo / ảnh bìa |
| | `FileUpload` | Tài liệu, bảng tính, audio (nghe thử), ảnh, video; tiến độ upload |
| Dữ liệu | `DataTable` | Tìm kiếm, lọc, sắp xếp, phân trang, chọn nhiều, thao tác dòng; **kéo đổi độ rộng cột**, nội dung dài tự `…` + tooltip; mobile → thẻ |
| | `SearchBar` `Pagination` | Dùng riêng khi không cần bảng |
| | `StatusTag` | `group` + `status` theo bảng 1.4, hoặc tự chọn `tone` + `icon` |
| | `Avatar` `UserCell` `StatCard` | Ảnh/tên người, ô số liệu dashboard có xu hướng |
| | `Timetable` | Thời khóa biểu tuần; mobile → chọn ngày + danh sách |
| Bố cục | `PageHeader` | Breadcrumb, tiêu đề, tag, nút hành động |
| Phản hồi | `useNotify()` | `success` `info` `warning` `error` `loading` `confirm` `promise` `toast` |
| | `useConfirm()` / `ConfirmDialog` | Nêu hậu quả, bắt nhập lý do, gõ để xác nhận |
| | `ModalForm` / `DrawerForm` | Form thêm/sửa; hỏi lại khi đóng mà chưa lưu; mobile → drawer đáy |
| | `EmptyState` `NoResults` `ErrorState` `LoadingSkeleton` | Rỗng, không có kết quả, lỗi, đang tải |

### Công thức dựng một trang danh sách

```tsx
<PageHeader title="..." breadcrumb={[...]} actions={<Button action="create" collapseOnMobile />} />
<div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{/* StatCard (nếu cần) */}</div>
<DataTable columns={...} data={...} rowKey="id" storageKey="ten-trang"
  search={{ placeholder, filters, onReset, actions }} rowActions={(r) => <>...</>} />
<ModalForm /> hoặc <DrawerForm />   {/* thêm / sửa */}
{confirmDialog}                    {/* từ useConfirm() — xóa / hủy / từ chối */}
```

### Khi nào dùng thông báo nào

| Tình huống | Dùng |
|---|---|
| Lưu/xóa xong, thao tác nhỏ | `notify.toast.success()` hoặc `notify.success()` |
| Thông tin cần đọc (đổi lịch, nhắc việc) | `notify.info()` |
| Sắp có vấn đề (sắp đủ sĩ số, sắp hết hạn) | `notify.warning()` — hiển thị 6 giây |
| Lỗi thao tác | `notify.error()` — hiển thị 8 giây |
| Việc chạy lâu (gửi hàng loạt, xuất file) | `notify.promise()` — loading → success/error |
| Gợi ý hành động nhanh, không chặn màn hình | `notify.confirm()` |
| Thao tác nguy hiểm, cần chặn lại | `useConfirm()` (hộp thoại giữa màn hình) |

### 6.1 Màu nút theo chức năng (`<Button action="..." />`)

| Màu | Action | Ý nghĩa |
|---|---|---|
| Xanh dương (primary) | `create` `save` `send` `search` `view` | Hành động chính, tích cực |
| Xanh lá (success) | `approve` `export` `import` `checkin` | Hoàn tất, dữ liệu vào/ra |
| Cam (warning) | `edit` `checkout` | Thay đổi dữ liệu đang có |
| Đỏ (danger) | `delete` `reject` | Không hoàn tác được — luôn kèm hộp xác nhận |
| Xám (neutral) | `cancel` `back` `filter` `reset` `refresh` `print` | Điều hướng, phụ trợ |

Danh sách đầy đủ (icon + nhãn mặc định): `src/components/common/Button/buttonActions.ts`.

### 6.2 Component nên làm tiếp

| Ưu tiên | Component | Vì sao cần |
|---|---|---|
| 1 | `Can` / `PermissionGate` | Ẩn/hiện theo permission |
| 3 | `BranchSelect` / `StudentPicker` / `TeacherPicker` | Chọn có tìm kiếm từ API |
| 3 | `Timetable` | Thời khóa biểu tuần/ngày (GV, HV, phòng) |
| 3 | `Timeline` | Lịch sử tư vấn lead, dòng thời gian học viên |
| 3 | `RichTextEditor` | Soạn bài tập, đề thi, thông báo (hỗ trợ furigana) |
| 3 | `MoneyText` / `DateText` | Hiển thị tiền, ngày giờ thống nhất |
