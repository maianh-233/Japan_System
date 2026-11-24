# 17 · Báo cáo & bảng điều khiển

Module: **Reporting** (chỉ đọc, không có bảng nghiệp vụ — [ADR 0004](../decisions/0004-bao-cao-doc-qua-contracts.md)) · Giai đoạn 24
Frontend: `pages/staff/dashboard/StaffDashboard.tsx`, `features/dashboard/{TodayWork.tsx,useTodayWork.ts}`,
`pages/staff/reports/ReportsPage.tsx`, `features/reports/{useReports.ts,charts.tsx}`

## Mô hình (đọc trước)

```
Module sở hữu dữ liệu (Crm, Students, Academic, Finance, System)
 └─ I…ReportSource trong <Module>.Contracts — tự GROUP BY trong DbContext của mình, trả số liệu thô theo chi nhánh × tháng
        │  (đếm, tổng tiền, phút — KHÔNG trả tỷ lệ)
        ▼
Reporting  ── ReportPeriod (3 / 6 / 12 tháng, giờ VN) ── ghép nguồn ── tính tỷ lệ, tăng trưởng, so sánh chi nhánh, mức độ việc
        ▼
GET /api/v1/reporting/…  →  frontend vẽ biểu đồ, ghi file .xlsx (backend không thêm thư viện Excel)
```

**Quy tắc vàng:**
1. **Chỉ đọc.** Reporting không sửa dữ liệu module khác, không giữ bản sao. Số liệu luôn là số hiện tại lúc mở trang.
2. **Không tự đếm lại nghiệp vụ.** Công nợ, tuổi nợ, SLA lead, trạng thái ghi danh… dùng đúng quy tắc của module sở hữu (Finance `DebtAging`,
   Crm SLA theo quy tắc chia lead). Reporting chỉ cộng / chia / xếp hạng.
3. **Phạm vi chi nhánh theo quyền**: `all` chọn được "Toàn hệ thống" hoặc 1 chi nhánh; `branch` chỉ chi nhánh của mình — chọn chi nhánh khác
   coi như chi nhánh của mình (không báo lỗi, không lộ số liệu). Phạm vi `own` trên `dashboard.overview` = số liệu chi nhánh của mình.
4. **Tháng tính theo giờ Việt Nam.** Kỳ báo cáo N tháng = tháng hiện tại + N − 1 tháng trước, tính từ ngày 1.
5. Tỷ lệ phần trăm làm tròn tới số nguyên (0,5 làm tròn lên); mẫu số 0 → **không có số** (hiện "—"), không phải 0%.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| BC-01 | Bảng điều khiển: chỉ số chính + việc cần xử lý | Mọi nhân viên có `dashboard.overview` | Cao |
| BC-02 | Việc hôm nay theo vai trò (Sale / Giáo vụ / Kế toán) | Theo quyền nghiệp vụ | Cao |
| BC-03 | Báo cáo tuyển sinh (chuyển đổi theo nguồn, Sale, chiến dịch) | Quản lý, Marketing | Cao |
| BC-04 | Báo cáo doanh thu & công nợ | Quản lý, Kế toán | Cao |
| BC-05 | Báo cáo đào tạo (sĩ số, chuyên cần, lấp đầy lớp, công suất phòng) | Quản lý, Giáo vụ | Cao |
| BC-06 | So sánh chi nhánh | Quản lý nhiều chi nhánh | TB |
| BC-07 | Xuất Excel báo cáo | Người có `dashboard.reports.export` | TB |

**Quyền:** `dashboard.overview` — `view` (BC-01, BC-02). `dashboard.reports` — `view` (BC-03 → 06), `export` (BC-07).
Từng nhóm việc ở BC-02 còn cần quyền nghiệp vụ của nhóm đó (vd `crm.leads`) — suy ra từ **quyền**, không theo tên vai trò.

---

## BC-01 · Bảng điều khiển

> Là **nhân viên**, tôi muốn **mở app là thấy ngay các con số chính và việc đang tồn đọng** để **biết hôm nay cần ưu tiên gì**.

**Nguồn:** Tổng quan › Bảng điều khiển (`StaffDashboard.tsx`) · **Quyền cần:** `dashboard.overview.view`

**Các bước:**
1. 4 thẻ chỉ số theo chi nhánh đang chọn:
   - **Học viên đang học** (đếm HV khác nhau có ghi danh `studying`) + số lớp đang học.
   - **Lead mới tháng này** + % so với tháng trước.
   - **Thực thu tháng này** (tiền thu − tiền hoàn trong tháng) + % so với tháng trước (1 chữ số thập phân).
   - **Công nợ quá hạn** + tổng còn phải thu.
2. Khối **Việc hôm nay** (BC-02).
3. **Buổi học hôm nay**, **Đơn chờ duyệt** — dùng API sẵn có của Academic / Hr (không qua Reporting).
4. **Cần xử lý** — danh sách cảnh báo, mỗi dòng bấm mở đúng trang:

| Cảnh báo | Điều kiện | Mức | Mở |
|---|---|---|---|
| Lead quá SLA | Lead `new`, chưa liên hệ lần nào, đã quá thời hạn SLA | Đỏ | Tuyển sinh › Quy tắc chia lead |
| Buổi chưa xác nhận | Buổi `completed` trong **14 ngày** qua chưa được nhân viên xác nhận (ảnh hưởng lương GV) | Vàng | Buổi học |
| Buổi chưa có nhật ký | Buổi `completed` trong **7 ngày** qua GV chưa ghi báo cáo buổi | Vàng | Buổi học |
| Lớp vắng | Lớp đang học / đang tuyển sinh lấp đầy **dưới 50%** — kèm tối đa 3 mã lớp | Vàng | Lớp học |
| Tích hợp lỗi | Mỗi tích hợp đang `error` — 1 dòng | Đỏ | Hệ thống › Tích hợp |

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BC-01.E1 | Tháng trước = 0 | Không hiện % tăng trưởng (không chia cho 0) |
| BC-01.E2 | Không có cảnh báo nào | "Mọi thứ đều ổn" |
| BC-01.E3 | Không có quyền xem Tích hợp (`system.integrations`) | Không có dòng tích hợp lỗi |

---

## BC-02 · Việc hôm nay theo vai trò

> Là **Sale / Giáo vụ / Kế toán**, tôi muốn **danh sách việc cụ thể, việc gấp lên đầu, bấm vào là mở đúng bản ghi** để **không bỏ sót**.

**Nguồn:** `TodayWork.tsx`, `useTodayWork.ts` · **Quyền cần:** `dashboard.overview.view` + quyền nghiệp vụ từng nhóm

**Các nhóm (mỗi vai trò 1 tab, số trên tab = tổng việc):**

| Vai trò (có quyền) | Nhóm | Lấy gì | Mức |
|---|---|---|---|
| Sale (`crm.leads`) | Lead đã / sắp quá SLA | Lead `new` chưa liên hệ, thời gian còn lại ≤ **30%** hạn SLA | Quá hạn: đỏ "quá X" · sắp: vàng "còn X" |
| | Lead cần gọi lại hôm nay | Lead đang mở có hẹn gọi lại trước cuối ngày hôm nay | Hẹn trước hôm nay: đỏ "trễ hẹn N ngày" · đã qua giờ hẹn: vàng · chưa tới: xanh "hẹn HH:mm" |
| Giáo vụ (`academic.sessions`) | Buổi học chưa điểm danh | Buổi không hủy, đã bắt đầu, trong **7 ngày** qua, chưa điểm danh | Hôm nay: vàng · ngày trước: đỏ "N ngày trước" |
| | Đơn chờ duyệt | Đơn (hủy buổi, xin nghỉ, đơn từ Hr) người dùng được quyền quyết | Buổi học liên quan < 24 giờ nữa: đỏ · còn lại: vàng |
| Kế toán (`finance.invoices`) | Hóa đơn đến hạn & quá hạn | Hóa đơn chưa hủy, còn nợ, hạn ≤ hôm nay + **3 ngày** | Quá hạn: đỏ · hạn hôm nay: vàng · sắp tới: xanh |
| (`finance.reconciliation`) | Giao dịch VietQR chưa khớp | Giao dịch ngân hàng `unmatched` | ≥ 2 ngày: đỏ · còn lại: vàng |

Thời lượng hiển thị: < 60 phút → "N phút", < 24 giờ → "N giờ", còn lại "N ngày" (làm tròn xuống).
Trong mỗi nhóm: đỏ → vàng → xanh, cùng mức thì cũ trước. Mỗi nhóm có "Xem tất cả" và câu khi rỗng.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BC-02.E1 | Sale phạm vi `own` trên `crm.leads` | Chỉ lead được giao cho mình; quản lý (phạm vi `branch` / `all`) thấy cả chi nhánh kèm tên Sale |
| BC-02.E2 | Admin / QL chi nhánh có đủ quyền | Thấy cả 3 tab |
| BC-02.E3 | Không có quyền `finance.reconciliation` | Tab Kế toán không có nhóm giao dịch chưa khớp |
| BC-02.E4 | Không có quyền nghiệp vụ nào | Không hiện khối Việc hôm nay |

---

## BC-03 · Báo cáo tuyển sinh

> Là **quản lý / Marketing**, tôi muốn **xem lead mới, tỷ lệ chốt theo nguồn, theo Sale, chi phí mỗi HV chốt theo chiến dịch** để **dồn ngân sách vào kênh hiệu quả**.

**Nguồn:** Báo cáo › tab Tuyển sinh · **Quyền cần:** `dashboard.reports.view`

**Các bước:** chọn kỳ **3 / 6 / 12 tháng** (mặc định 6) và chi nhánh ở thanh trên cùng.
1. Thẻ: lead mới trong kỳ, đã chốt (`won`), tỷ lệ chuyển đổi, đang theo đuổi (mới + tư vấn + hẹn test).
2. Lead mới theo tháng (cột, tháng hiện tại tô đậm).
3. Phễu theo trạng thái **hiện tại** của lead tạo trong kỳ: mới, đang tư vấn, hẹn test, đã chốt, đã mất.
4. Tỷ lệ chốt theo nguồn (bỏ nguồn không có lead, nhiều lead lên trước).
5. Theo Sale: lead được giao, đang mở, chốt, tỷ lệ — chốt nhiều lên trước. Danh sách Sale = nhân viên phòng Sale của chi nhánh (kể cả 0 lead).
6. Chiến dịch: chi phí, lead, chốt, **chi phí / lead**, **chi phí / HV chốt** — rẻ nhất lên đầu, chiến dịch chưa có chốt xếp cuối.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BC-03.E1 | Lead đã ẩn (gộp trùng / xóa mềm) | Không tính |
| BC-03.E2 | Chiến dịch chạy nhiều chi nhánh | Hiện khi chi nhánh đang chọn nằm trong danh sách chi nhánh của chiến dịch; lead đếm mọi lúc (không giới hạn kỳ) như trang Chiến dịch |
| BC-03.E3 | Kỳ không thuộc 3 / 6 / 12 | `reporting.period.invalid` |

---

## BC-04 · Báo cáo doanh thu & công nợ

> Là **quản lý / Kế toán**, tôi muốn **thấy thực thu từng tháng, công nợ và tuổi nợ** để **theo dõi dòng tiền và đôn đốc thu**.

**Nguồn:** Báo cáo › tab Doanh thu & công nợ · **Quyền cần:** `dashboard.reports.view`

1. Thẻ: thực thu tháng này (+ % so với tháng trước), thực thu cả kỳ (kèm **doanh số ký** = tổng hóa đơn lập trong kỳ), công nợ, quá hạn.
2. Thực thu theo tháng = tiền thu (theo ngày thu) − tiền hoàn (theo ngày hoàn) trong tháng.
3. Tuổi nợ theo 4 nhóm của Finance: chưa đến hạn · quá hạn 1–30 · 31–60 · > 60 ngày (số tiền + số hóa đơn).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BC-04.E1 | Hóa đơn đã hủy / vô hiệu | Không tính công nợ, không tính doanh số ký; tiền đã thu / hoàn trước đó vẫn tính thực thu |
| BC-04.E2 | Tháng hoàn nhiều hơn thu | Thực thu tháng âm — hiện đúng số âm |

---

## BC-05 · Báo cáo đào tạo

> Là **quản lý / Giáo vụ**, tôi muốn **xem sĩ số, chuyên cần, lớp nào vắng, phòng nào dư** để **gộp lớp, mở lớp, xếp phòng hợp lý**.

**Nguồn:** Báo cáo › tab Đào tạo · **Quyền cần:** `dashboard.reports.view`

1. Thẻ: HV đang học, lớp đang học (+ số lớp chờ khai giảng), chuyên cần tháng này, công suất phòng TB tuần này.
2. **Ghi danh mới theo tháng** (theo ngày ghi danh, chi nhánh của lớp).
3. **Chuyên cần theo tháng** = lượt có mặt + đi muộn / tổng lượt đã điểm danh của buổi trong tháng.
4. **Lấp đầy lớp** (lớp đang học + đang tuyển sinh) = HV đang học + bảo lưu / sĩ số tối đa — thấp nhất lên đầu.
5. **Công suất phòng tuần này** (T2 → CN) = giờ có buổi học không hủy trong phòng / **81 giờ** (T2–T7, 8:00–21:30) — cao nhất lên đầu.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BC-05.E1 | Tháng chưa có buổi nào điểm danh | Chuyên cần tháng đó = không có số (cột trống), không tính là 0% |
| BC-05.E2 | Lớp online | Không chiếm phòng, không tính công suất phòng |
| BC-05.E3 | Buổi đổi phòng riêng | Tính vào phòng của buổi, không phải phòng mặc định của lớp |

---

## BC-06 · So sánh chi nhánh

> Là **quản lý nhiều chi nhánh**, tôi muốn **đặt các chi nhánh cạnh nhau** để **thấy chi nhánh nào cần hỗ trợ**.

**Nguồn:** Báo cáo › tab So sánh chi nhánh · **Quyền cần:** `dashboard.reports.view`

Mỗi chi nhánh (đang hoạt động, trong phạm vi): lead trong kỳ, tỷ lệ chốt, HV đang học, số lớp đang học / tuyển sinh, lấp đầy lớp,
chuyên cần cả kỳ, thực thu cả kỳ, thực thu tháng này, công nợ, quá hạn. Biểu đồ thực thu và HV đang học theo chi nhánh.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BC-06.E1 | Chuyên cần < **85%** hoặc còn nợ quá hạn | Đánh dấu cần chú ý (tô đỏ ô tương ứng) |
| BC-06.E2 | Phạm vi `branch` | Chỉ 1 dòng — chi nhánh của mình |

---

## BC-07 · Xuất Excel báo cáo

> Là **quản lý / Kế toán / Marketing**, tôi muốn **tải báo cáo đang xem ra Excel** để **gửi ban giám đốc, làm báo cáo tháng**.

**Nguồn:** nút Xuất trên trang Báo cáo · **Quyền cần:** `dashboard.reports.export`

1. Backend trả **toàn bộ số liệu** của kỳ + chi nhánh đang chọn (giống BC-03 → 06), frontend ghi 1 file `.xlsx`, mỗi tab 1 sheet
   (`lib/spreadsheet.ts` — giống xuất nhật ký hệ thống, backend không thêm thư viện Excel).
2. Mỗi lần xuất **ghi nhật ký hệ thống** (nhóm `export`, mức `warning`): "Xuất báo cáo" — "Báo cáo N tháng · <chi nhánh / Toàn hệ thống>".
3. Tên file: `bao-cao_<chi-nhanh|toan-he-thong>_<YYYY-MM>_<N>-thang.xlsx`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BC-07.E1 | Chỉ có `view`, không có `export` | 403, nút Xuất ẩn |

---

## Dữ liệu / API

**Bảng:** không có (schema `reporting` để trống). Nguồn đọc qua Contracts (chi nhánh `null` = mọi chi nhánh, người gọi đã lọc theo quyền):

| Interface | Module | Dùng cho |
|---|---|---|
| `ILeadReportSource` — lead tạo trong kỳ, chiến dịch + lead / chốt, lead đang mở (SLA, hẹn gọi lại) | Crm | BC-01, 02, 03, 06 |
| `IEnrollmentReportSource` — ghi danh đang học / bảo lưu + ghi danh mới | Students | BC-01, 05, 06 |
| `IAcademicReportSource` — lớp mở, điểm danh theo tháng, phút dùng phòng, buổi chưa xác nhận / chưa nhật ký / chưa điểm danh, đơn chờ duyệt | Academic | BC-01, 02, 05, 06 |
| `IFinanceReportSource` — thực thu theo tháng, doanh số ký, hóa đơn còn nợ (tuổi nợ), giao dịch chưa khớp | Finance | BC-01, 02, 04, 06 |
| `IHrRequestInbox` — đơn từ đến lượt người đang đăng nhập duyệt | Hr | BC-02 |
| `IIntegrationHealth` — tích hợp đang lỗi | System | BC-01 |

| Endpoint | Story | Quyền |
|---|---|---|
| `GET /api/v1/reporting/dashboard?branchId=` | BC-01 (thẻ + cần xử lý) | `dashboard.overview.view` |
| `GET /api/v1/reporting/today-work?branchId=` | BC-02 | `dashboard.overview.view` |
| `GET /api/v1/reporting/reports?branchId=&months=` (mặc định 6) | BC-03 → 06 (cùng hình dạng `ReportData` của `useReports.ts`; nhãn nguồn / trạng thái frontend tự gắn) | `dashboard.reports.view` |
| `POST /api/v1/reporting/reports/export` `{ branchId, months }` | BC-07 | `dashboard.reports.export` |

**Mã lỗi:** `reporting.period.invalid`.
