# 08 · Học phí, hóa đơn & tài chính

Module: **Finance** · Bảng: `finance.invoices`, `payments`, `refunds`, `branch_prices`, `discounts`, `installment_plans`, `bank_transactions`,
`payment_reminders`, `pay_adjustments`, `payroll_locks`, `commission_tiers`, `sale_targets` ([database.md](../database.md)) · Liên quan: `system.approval_requests` (duyệt 2 người)
Frontend: `pages/staff/finance/**`, `features/finance/{financeStore,financeUtils,types}.ts`, `features/finance/{InvoiceDrawer,PayrollBar}.tsx`,
`features/student/studentStore.ts › summarizeInvoice / addPayment / addRefund / voidInvoice`, `features/system/approvals.ts`, cổng `features/portal/components/TuitionView.tsx`

## Mô hình (đọc trước)

```
Enrollment (Students) ──1:1──▶ Invoice  HD-{CN}-{YY}{MM}-{SO:4}   transfer_content riêng (VietQR)
                                ├─ amount (giá lớp / giá chi nhánh) − discount (tay / mã ưu đãi / khấu trừ chuyển lớp) = PHẢI THU
                                ├─ Payment[]  tiền mặt · chuyển khoản · VietQR · thẻ   ── (VietQR) ◀── BankTransaction (đối soát)
                                ├─ Refund[]   ≥ ngưỡng cấu hình → duyệt 2 người
                                ├─ cancelled  hủy PHẦN CÒN LẠI (HV nghỉ / chuyển lớp / lớp hủy) → hết công nợ, tiền đã thu giữ nguyên
                                └─ voided     hủy HÓA ĐƠN lập sai → luôn duyệt 2 người, vào Thùng rác

Trạng thái (TÍNH, không lưu tay):  refunded (có hoàn) › cancelled › paid (còn nợ = 0) › overdue (quá hạn) › partial (đã thu 1 phần) › unpaid
  đã thu = Σ payments − Σ refunds   ·   còn nợ = cancelled ? 0 : max(0, phải thu − Σ payments)

Lương:  GV = Σ buổi ĐÃ XÁC NHẬN × (phút thực dạy ÷ 60) × đơn giá offline/online + thưởng dạy thay − phạt muộn ± điều chỉnh (+ lương cứng nếu có)
        NV = lương cứng × công ÷ công chuẩn + phụ cấp + hoa hồng ± điều chỉnh − BHXH − thuế TNCN
        Chốt tháng = snapshot `payroll_locks` theo (loại, tháng, chi nhánh)
```

**Quy tắc vàng:**
1. Tiền là `Money` (VND, số nguyên). Không lưu số "đã thu / còn nợ" — luôn tính từ `payments` / `refunds`.
2. Hóa đơn **chỉ sinh từ ghi danh** (CRM-07, HV-06, HV-09, CRM-19) — không có nút "tạo hóa đơn rời" 🆕 (phiếu thu khác ⏳ để sau).
3. **Maker–checker**: hoàn tiền từ ngưỡng `safety.refundApprovalMin` và mọi lần **hủy hóa đơn** phải có người thứ 2 duyệt; người tạo không tự duyệt.
4. Mã hóa đơn liên tục theo chi nhánh + tháng, không tái sử dụng số đã phát hành (kể cả hóa đơn bị hủy).
5. Học phí **không** trả về cổng GV. Cổng HV / PH chỉ thấy hóa đơn của mình / các con (người nhận hóa đơn — `billing`).
6. Mọi thao tác tiền ghi nhật ký `category = finance`; tiền mặt ghi `collector_id` để vào bàn giao ca.

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| TC-01 | Bảng giá theo chi nhánh | Kế toán trưởng | TB |
| TC-02 | Chính sách đóng nhiều đợt | Kế toán trưởng | Thấp |
| TC-03 | Ưu đãi học phí (%, số tiền, combo) | Kế toán, marketing | TB |
| TC-04 | Sinh hóa đơn từ ghi danh | Hệ thống | Cao |
| TC-05 | Xem danh sách & chi tiết hóa đơn | Kế toán, Sale, giáo vụ | Cao |
| TC-06 | Thu tiền (nhiều đợt) | Kế toán, lễ tân, Sale | Cao |
| TC-07 | Hoàn tiền (duyệt 2 người từ ngưỡng) | Kế toán | Cao |
| TC-08 | Hủy hóa đơn lập sai (duyệt 2 người) | Kế toán | TB |
| TC-09 | HV / PH xem & đóng học phí qua VietQR | HV, PH | Cao |
| TC-10 | Đối soát giao dịch VietQR | Kế toán | Cao |
| TC-11 | Công nợ & nhắc học phí | Kế toán, giáo vụ | Cao |
| TC-12 | Hoa hồng & KPI Sale | Kế toán, quản lý Sale | TB |
| TC-13 | Lương giáo viên | Kế toán | TB |
| TC-14 | Lương nhân viên | Kế toán | TB |

**Quyền:** `finance.tuition` (giá, ưu đãi, chính sách đợt) · `finance.invoices` (`approve` = duyệt hoàn / hủy) · `finance.reconciliation` · `finance.debts` ·
`finance.teacher-payroll` · `finance.staff-payroll` · `finance.commissions`. Cấp quyền tài chính cho người khác cần duyệt 2 người (PQ-09).

---

## TC-01 · Bảng giá theo chi nhánh

> Là **kế toán trưởng**, tôi muốn **đặt giá riêng của từng khóa ở từng chi nhánh** để **chi nhánh trung tâm thành phố và ngoại thành có giá khác nhau**.

**Nguồn:** Tài chính › Học phí & bảng giá › tab Bảng giá (`TuitionPage.tsx`) · **Quyền cần:** `finance.tuition.view / update`

**Các bước:** Ma trận khóa × chi nhánh: ô trống = giá niêm yết của khóa; sửa ô → nhập giá* (nhóm: trọn khóa; 1-1: mỗi buổi) → `branch_prices`; xóa giá → về niêm yết.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-01.E1 | Giá ≤ 0 | `finance.invoice.invalid_amount` (dùng chung lỗi số tiền) |
| TC-01.E2 | Đổi giá khi đã có lớp / hóa đơn | Chỉ áp cho **lớp mở mới** (học phí lớp chốt lúc mở — DT-02); không sửa hóa đơn cũ |
| TC-01.E3 | Khóa không bán ở chi nhánh | Ô khóa (không cho đặt giá) |

---

## TC-02 · Chính sách đóng nhiều đợt

**Quyền cần:** `finance.tuition.update` · tab Chính sách đợt

**Các bước:** Tên*, các đợt `{% , số ngày so với khai giảng (âm = trước)}` (tổng **100%**), áp cho khóa có học phí từ X, phụ thu %, bật / tắt.
Khi ghi danh lớp đủ điều kiện → chọn chính sách → hóa đơn có `installment_plan_id`, phụ thu cộng vào `amount`, mỗi đợt có hạn riêng 🆕 (hạn của hóa đơn = hạn đợt gần nhất chưa đóng đủ).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-02.E1 | Tổng các đợt ≠ 100% | "Tổng các đợt phải bằng 100% (hiện 90%)" — `finance.installment.total` |
| TC-02.E2 | < 2 đợt hoặc % ≤ 0 | `finance.installment.parts` |
| TC-02.E3 | Quá hạn 1 đợt | Hóa đơn `overdue` theo đợt đó (TC-11) |

---

## TC-03 · Ưu đãi học phí

> Là **kế toán / marketing**, tôi muốn **tạo mã giảm %, giảm tiền, combo nhiều khóa có thời hạn và giới hạn lượt** để **chạy khuyến mãi mà không ai tự ý giảm quá tay**.

**Nguồn:** tab Ưu đãi · **Quyền cần:** `finance.tuition.create / update`

**Các bước:**
1. Mã* (3–15 chữ / số), tên, loại (`percent` · `amount` · `combo`), mức giảm*, khóa combo (≥ 2 khóa), chi nhánh*, từ – đến, giới hạn lượt, ghi chú, bật / tắt.
2. Áp mã khi ghi danh (CRM-07 / HV-06): kiểm hợp lệ → `discount` = mức giảm (percent tính trên học phí, làm tròn 1.000đ), `discount_id`, `used + 1` (cùng transaction).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-03.E1 | Giảm % > 50 hoặc ≤ 0 | "Giảm tối đa 50%" — `finance.discount.invalid_percent` |
| TC-03.E2 | Combo < 2 khóa | `finance.discount.combo_courses` |
| TC-03.E3 | Ngày kết thúc trước ngày bắt đầu | `finance.discount.invalid_dates` |
| TC-03.E4 | Mã hết lượt | `finance.discount.limit_reached` (kiểm có khóa — 2 người dùng lượt cuối cùng lúc chỉ 1 người được) |
| TC-03.E5 | Mã tắt / ngoài thời gian / sai chi nhánh / sai khóa / combo thiếu khóa đi kèm | `finance.discount.not_available` |
| TC-03.E6 | Giảm tay (không mã) > học phí | `finance.invoice.discount_too_large` |
| TC-03.E7 | Hóa đơn dùng mã bị hủy (TC-08) | Trả lại 1 lượt (`used − 1`) |

---

## TC-04 · Sinh hóa đơn từ ghi danh

> Là **hệ thống**, tôi **lập hóa đơn ngay khi HV được ghi danh** để **công nợ luôn khớp với danh sách lớp**.

**Kích hoạt:** event `StudentEnrolled` / `EnrollmentTransferred` (Students) · hoặc gọi đồng bộ trong CRM-07 (cùng transaction qua Outbox)

**Các bước:**
1. `amount` = học phí lớp (đã chốt ở lớp), `discount` = giảm tay / mã ưu đãi / khấu trừ chuyển lớp, `due_date` (mặc định +7 ngày), `issued_at`.
2. Mã `HD-{tiền tố CN}-{YY}{MM}-{SO:4}` (mẫu ở Cài đặt chung; số tăng dần theo chi nhánh + tháng — bộ đếm có khóa).
3. `transfer_content` duy nhất (vd `HD-CG-2611-0003 NGUYEN VAN AN`) để đối soát tự động.
4. Thông báo người nhận hóa đơn (CRM-08 `billing`) kèm link cổng / VietQR.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-04.E1 | Nhận event 2 lần | Idempotent theo `enrollment_id` (UQ) — không sinh hóa đơn thứ 2 |
| TC-04.E2 | Chi nhánh chưa có tài khoản ngân hàng | Hóa đơn vẫn lập; không có VietQR (CN-01.E2) |
| TC-04.E3 | Phải thu = 0 (khấu trừ đủ) | Hóa đơn `paid` ngay |
| TC-04.E4 | Hai hóa đơn cùng lúc ở 1 chi nhánh | Bộ đếm số hóa đơn khóa dòng / sequence riêng — không trùng số |
| TC-04.E5 | Doanh số Sale | Ghi nhận cho: Sale được kế toán gán tay › Sale phụ trách lead liên kết (lead đã chốt ưu tiên, rồi lead mới nhất) › chia đều Sale chi nhánh (TC-12) |

---

## TC-05 · Xem danh sách & chi tiết hóa đơn

> Là **kế toán**, tôi muốn **lọc hóa đơn theo trạng thái, lớp, chi nhánh, hạn** để **biết hôm nay phải thu ai**.

**Nguồn:** Tài chính › Hóa đơn & thu tiền (`InvoicesPage.tsx`, `InvoiceDrawer.tsx`) · **Quyền cần:** `finance.invoices.view`

**Các bước:**
1. Bảng: mã, HV, lớp, phải thu, đã thu, còn nợ, hạn, trạng thái, Sale. Lọc trạng thái / chi nhánh / lớp / khoảng ngày lập / "quá hạn"; tìm mã, tên HV.
2. Chi tiết: thông tin, VietQR + nội dung CK, dòng thời gian (lập, các lần thu, hoàn, hủy, nhắc), yêu cầu đang chờ duyệt, nút Thu tiền / Hoàn tiền / Hủy hóa đơn / Nhắc.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-05.E1 | Hóa đơn đã hủy (`voided`) | Chỉ hiện khi lọc "Đã hủy"; ghi "Yêu cầu: A · duyệt: B · lúc" |
| TC-05.E2 | Có yêu cầu hoàn / hủy đang chờ | Nhãn "Chờ duyệt"; số hoàn tối đa trừ khoản đang chờ |
| TC-05.E3 | Xuất Excel | `finance.invoices.export`; nhật ký `export` |

---

## TC-06 · Thu tiền (nhiều đợt)

> Là **kế toán / lễ tân**, tôi muốn **ghi nhận từng lần HV đóng tiền** để **công nợ giảm ngay và HV / PH nhận xác nhận**.

**Nguồn:** `InvoiceDrawer.tsx › Thu tiền` · **Quyền cần:** `finance.invoices.update` (🆕 Sale thu tại CRM-07 cần thêm quyền này)

**Các bước:** Số tiền* (≤ còn nợ), hình thức* (tiền mặt / chuyển khoản / VietQR / thẻ), thời điểm (mặc định bây giờ), ghi chú → `payments` (`collector_id` = người thu)
→ phát **`InvoicePaid`** / `PaymentRecorded` → thông báo HV + PH ("Đã ghi nhận học phí … ₫") → phiếu thu PDF 🆕.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-06.E1 | Số tiền ≤ 0 | `finance.invoice.invalid_amount` |
| TC-06.E2 | Vượt số còn nợ | "Vượt quá số còn nợ" — `finance.invoice.overpay` |
| TC-06.E3 | Hóa đơn đã hủy / hủy phần còn lại | `finance.invoice.closed` |
| TC-06.E4 | Bấm 2 lần | `Idempotency-Key` — không ghi 2 khoản |
| TC-06.E5 | Tiền mặt | Cộng vào tiền mặt ca của người thu (bàn giao ca — module Công việc) |
| TC-06.E6 | Thu sai | Không xóa khoản thu — dùng hoàn tiền (TC-07) để bù trừ, có lý do |

---

## TC-07 · Hoàn tiền

> Là **kế toán**, tôi muốn **hoàn tiền cho HV nghỉ / lớp hủy, với khoản lớn phải có người duyệt** để **tránh hoàn sai hoặc gian lận**.

**Nguồn:** `InvoiceDrawer.tsx › Hoàn tiền`, `features/system/approvals.ts` · **Quyền cần:** `finance.invoices.update` (tạo) · `finance.invoices.approve` (duyệt)

**Các bước:**
1. Form hiện: HV, đã đóng, **hoàn tối đa** (= đã thu − khoản đang chờ duyệt), ngưỡng cần duyệt. Nhập số tiền*, hình thức*, lý do*.
2. Số tiền **< ngưỡng** `safety.refundApprovalMin` → ghi `refunds` ngay. **≥ ngưỡng** (mặc định 0 = mọi khoản) → tạo `approval_requests` (`refund`) → người thứ 2 duyệt / từ chối.
3. Duyệt → ghi `refunds` (`by` = người tạo, `approved_by` = người duyệt) → hóa đơn `refunded` → thông báo HV / PH.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-07.E1 | Số tiền ≤ 0 hoặc > hoàn tối đa | `finance.invoice.invalid_refund` ("Vượt quá số còn hoàn được (đã trừ khoản đang chờ duyệt)") |
| TC-07.E2 | Người tạo tự duyệt | `finance.invoice.self_approval` |
| TC-07.E3 | Người tạo rút lại yêu cầu | Yêu cầu `withdrawn`, không ghi hoàn |
| TC-07.E4 | Hoàn khi HV vẫn đang học 🆕 | Cảnh báo "HV còn đang học — hoàn một phần?"; vẫn cho |
| TC-07.E5 | Hoàn khi hóa đơn `voided` | `finance.invoice.closed` |

---

## TC-08 · Hủy hóa đơn lập sai

> Là **kế toán**, tôi muốn **hủy hóa đơn lập trùng / sai, có người duyệt** để **công nợ đúng mà vẫn truy vết được**.

**Quyền cần:** `finance.invoices.update` (yêu cầu) · `finance.invoices.approve` (duyệt)

**Các bước:** "Hủy hóa đơn" → hộp nêu: HV không còn công nợ X của hóa đơn này; nhật ký ghi người yêu cầu + người duyệt. **Lý do ≥ 5 ký tự** →
`approval_requests` (`void_invoice`) → duyệt → `voided_at`, `void_reason`, `void_requested_by`, `void_approved_by` → vào **Thùng rác** (khôi phục được trong `safety.trashDays`).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-08.E1 | Hóa đơn đã có khoản thu chưa hoàn hết 🆕 | Chặn `finance.invoice.has_payments` — hoàn hết trước rồi mới hủy |
| TC-08.E2 | Người yêu cầu tự duyệt | `finance.invoice.self_approval` |
| TC-08.E3 | Đã hủy rồi | `finance.invoice.closed` |
| TC-08.E4 | Khôi phục từ Thùng rác | Bỏ `voided_*`, trạng thái tính lại; số hóa đơn giữ nguyên |
| TC-08.E5 | Ghi danh của hóa đơn bị hủy | **Không** tự đổi — kế toán báo giáo vụ xử lý ghi danh nếu cần |

---

## TC-09 · Học viên / phụ huynh xem & đóng học phí qua VietQR

> Là **phụ huynh**, tôi muốn **xem học phí các con và quét mã QR để chuyển khoản** để **đóng tiền không cần đến trung tâm**.

**Nguồn:** cổng HV `StudentTuitionPage.tsx`, cổng PH `ParentTuitionPage.tsx`, `TuitionView.tsx` · **Quyền:** chủ tài khoản

**Các bước:** Danh sách hóa đơn (theo con nếu là PH): mã, lớp, phải thu, đã thu, còn nợ, hạn, trạng thái, lịch sử đóng. Bấm "Đóng học phí" → VietQR
(tài khoản ngân hàng của chi nhánh, số tiền = còn nợ, nội dung = `transfer_content`) → sau khi tiền về, TC-10 tự khớp → thông báo "Đã ghi nhận học phí".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-09.E1 | PH không có cờ "nhận hóa đơn" với con đó 🆕 | Vẫn xem được học phí (là PH liên kết) nhưng không nhận nhắc học phí |
| TC-09.E2 | Chi nhánh chưa khai báo tài khoản | Ẩn QR, hiện "Vui lòng đóng tại quầy / liên hệ trung tâm" |
| TC-09.E3 | Dữ liệu trả về cổng | Không trả ghi chú nội bộ, người thu, Sale |
| TC-09.E4 | Đóng thiếu / thừa | TC-10 khớp số thực nhận; thừa → giao dịch ở "chưa khớp" để kế toán xử lý |

---

## TC-10 · Đối soát giao dịch VietQR

> Là **kế toán**, tôi muốn **giao dịch ngân hàng tự khớp vào hóa đơn, chỉ xử lý tay những cái lệch** để **không phải dò sao kê**.

**Nguồn:** Tài chính › Đối soát VietQR (`ReconciliationPage.tsx`, `financeUtils.ts › suggestMatches / autoMatchable`) · **Quyền cần:** `finance.reconciliation.view / update`

**Các bước:**
1. Webhook ngân hàng (Tích hợp) → `bank_transactions` (`ref` duy nhất, thời điểm, số tiền, nội dung, người chuyển, tài khoản nhận → chi nhánh) `unmatched`.
2. **Tự khớp** chỉ khi nội dung có **mã hóa đơn hợp lệ** và số tiền ≤ còn nợ → tạo `payment` (method `viet_qr`, `bank_transaction_id`) → `auto`.
3. Còn lại → danh sách "Chưa khớp" kèm **gợi ý** (điểm: đúng mã HĐ +100, trùng tên HV +40 / có tên +10, có mã lớp +20, đúng số còn nợ +30, cùng chi nhánh +5; vượt nợ −50; lấy ≥ 30, tối đa 5).
4. Kế toán **khớp tay** (xác nhận "Khớp X ₫ vào HD-…?") → `matched`, `matched_by/at` · **Bỏ qua** (không phải học phí: hoàn ứng, chuyển nội bộ… — ghi chú bắt buộc) → `ignored` ·
   **Bỏ khớp** (khớp nhầm) → xóa payment tương ứng, về `unmatched`.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-10.E1 | Webhook gửi lại cùng giao dịch | Idempotent theo `ref` (UQ) |
| TC-10.E2 | Khớp giao dịch đã khớp | `finance.bank_txn.already_matched` |
| TC-10.E3 | Số tiền > còn nợ | Không tự khớp; khớp tay vào 1 hóa đơn → `finance.invoice.overpay` 🆕 cho **tách** vào nhiều hóa đơn của cùng HV / PH |
| TC-10.E4 | Khớp nhiều giao dịch liên tiếp | Công nợ trừ dần trong cùng lượt (không gợi ý lại hóa đơn đã đủ) |
| TC-10.E5 | Bỏ khớp khi tháng đã chốt sổ 🆕 | Cảnh báo, ghi nhật ký `warning` |
| TC-10.E6 | Chữ ký webhook sai | Từ chối 401, ghi `integration_logs` |

---

## TC-11 · Công nợ & nhắc học phí

> Là **kế toán**, tôi muốn **xem nợ theo tuổi nợ, gom theo người trả, và nhắc hàng loạt qua Zalo / email** để **thu nợ đúng người, đúng lúc**.

**Nguồn:** Tài chính › Công nợ (`DebtsPage.tsx`, `financeStore.ts › remind`) · **Quyền cần:** `finance.debts.view` · `.update` (nhắc)

**Các bước:**
1. Thẻ tổng + thanh tuổi nợ: **chưa tới hạn** · **1–30 ngày** · **31–60 ngày** · **> 60 ngày** (tính từ `due_date`, chỉ hóa đơn còn nợ).
2. Gom theo **người trả** (PH nhận hóa đơn — gom các con; không có → chính HV) / lớp / chi nhánh. Mỗi nhóm: SĐT (gọi nhanh), tổng nợ, các hóa đơn, lần nhắc gần nhất.
3. Chọn nhiều → "Nhắc học phí": kênh (Zalo / email / SMS / gọi điện), ghi chú → gửi theo mẫu `tuition` (`{ten}`, `{so_tien}`, link VietQR) → `payment_reminders` (1 request cho cả lô).
4. **Tự nhắc** 🆕 (job hằng ngày): trước hạn 3 ngày, đúng hạn, quá hạn 7 ngày — theo Cài đặt chung, mỗi mốc 1 lần.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-11.E1 | Người trả không có kênh đã chọn | Bỏ qua, báo "N người không có SĐT / email" |
| TC-11.E2 | Nhắc 2 lần trong ngày cho cùng hóa đơn 🆕 | Cảnh báo trước khi gửi |
| TC-11.E3 | Hóa đơn chuyển `overdue` | Tính khi đọc (không job đổi trạng thái); phát `InvoiceOverdue` 🆕 cho Students (cảnh báo bỏ học — HV-14) bằng job hằng ngày |
| TC-11.E4 | Phạm vi `branch` | Chỉ nợ của chi nhánh được gán |

---

## TC-12 · Hoa hồng & KPI Sale

> Là **quản lý Sale / kế toán**, tôi muốn **tính hoa hồng theo bậc doanh thu thực thu và so với chỉ tiêu** để **trả thưởng đúng và có bảng xếp hạng**.

**Nguồn:** Tài chính › Hoa hồng & KPI Sale (`CommissionsPage.tsx`, `financeUtils.ts › saleResults`) · **Quyền cần:** `finance.commissions.*`

**Các bước:**
1. Bậc hoa hồng: các mốc `doanh thu từ → %` (bắt buộc có mốc 0đ, không trùng mốc).
2. Chỉ tiêu tháng từng Sale (`sale_targets`).
3. Kết quả tháng: **thực thu** = Σ payments − refunds trong tháng của hóa đơn được ghi nhận cho Sale (TC-04.E5) → bậc → hoa hồng; % chỉ tiêu; xếp hạng.
4. Kế toán **gán lại Sale** cho 1 hóa đơn (ghi đè) có nhật ký. Hoa hồng tự đưa vào lương NV (TC-14).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-12.E1 | Thiếu mốc 0đ / mốc trùng | "Cần có bậc bắt đầu từ 0đ" / "Các mốc không được trùng nhau" |
| TC-12.E2 | % < 0 hoặc > 100 | `finance.commission.invalid_percent` |
| TC-12.E3 | Hoàn tiền tháng sau | Trừ vào thực thu **tháng hoàn** (không sửa tháng đã chốt) |
| TC-12.E4 | Phạm vi `own` | Sale chỉ xem kết quả của mình |

---

## TC-13 · Lương giáo viên

> Là **kế toán**, tôi muốn **tính lương GV từ buổi đã xác nhận × đơn giá, cộng dạy thay, trừ đi muộn, rồi chốt tháng** để **trả lương đúng, không tranh cãi**.

**Nguồn:** Tài chính › Lương giáo viên (`TeacherPayrollPage.tsx`, `financeUtils.ts › teacherPayroll`, `PayrollBar.tsx`) · **Quyền cần:** `finance.teacher-payroll.*`

**Các bước:**
1. Chọn tháng + chi nhánh → mỗi GV / trợ giảng (chưa nghỉ việc): buổi **đã xác nhận** (DT-15) có check-in + check-out →
   tiền buổi = phút thực dạy ÷ 60 × đơn giá (offline / online theo lớp); vai trò dạy thay + thưởng dạy thay; đi muộn − phạt; ± điều chỉnh tháng; + lương cứng (nếu hợp đồng có).
2. Hiện riêng: buổi **chờ xác nhận** (chưa tính), buổi bị hủy (không tính), tổng giờ.
3. **Điều chỉnh** (thưởng / phạt / phụ cấp): số tiền ≠ 0*, lý do* → `pay_adjustments`.
4. **Chốt lương**: cảnh báo nếu còn buổi chờ xác nhận → snapshot `payroll_locks` (`lines` từng người, `total`) → gửi phiếu lương (email / cổng GV) → "Đã chi lương" (`paid_at`).
5. Hạn chốt: ngày `payroll.lockDay` tháng sau (Cài đặt) — quá hạn chưa chốt → nhãn cảnh báo.
6. Cổng GV: "Chấm công" xem buổi đã dạy + **lương tạm tính** của mình.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-13.E1 | Điều chỉnh = 0 / thiếu lý do | `finance.pay_adjust.invalid_amount` / `finance.pay_adjust.reason_required` |
| TC-13.E2 | Sửa / xác nhận buổi, điều chỉnh khi tháng đã chốt | 🆕 `finance.payroll.locked` — phải **mở khóa** trước (nhật ký `warning`) |
| TC-13.E3 | Mở khóa tháng đã "chi lương" 🆕 | Chặn `finance.payroll.already_paid` |
| TC-13.E4 | Chốt 2 lần | UQ (kind, month, branch) — chốt lại = ghi đè snapshot khi đã mở khóa |
| TC-13.E5 | GV dạy ở nhiều chi nhánh | Buổi tính vào chi nhánh **của lớp** |
| TC-13.E6 | Buổi kết thúc sớm do sự cố (DT-13) | Tính theo phút thực dạy (check-out sớm) |

---

## TC-14 · Lương nhân viên

> Là **kế toán**, tôi muốn **tính lương nhân viên từ công thực tế, phụ cấp, hoa hồng, khấu trừ** để **chốt bảng lương tháng một lần**.

**Nguồn:** Tài chính › Lương nhân viên (`StaffPayrollPage.tsx`, `financeUtils.ts › staffPayroll`) · **Quyền cần:** `finance.staff-payroll.*`

**Các bước:** Mỗi nhân viên `kind = staff` chưa nghỉ: công thực tế (Chấm công / Xếp ca — module Công việc / Nhân sự ⏳), công tính lương ≤ công chuẩn,
lương = lương cứng × công ÷ công chuẩn + phụ cấp + hoa hồng (TC-12) ± điều chỉnh − BHXH (theo lương cứng) − thuế TNCN lũy tiến → chốt như TC-13.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TC-14.E1 | Nhân viên vào / nghỉ giữa tháng | Công tính theo ngày có hiệu lực hợp đồng |
| TC-14.E2 | Chưa có dữ liệu chấm công ⏳ | Tạm lấy công chuẩn, nhãn "Chưa có chấm công" |
| TC-14.E3 | Phiếu lương | Chỉ người đó (cổng nhân viên — Cá nhân) và người có `finance.staff-payroll.view` xem; **không** trả ở NS-11 |

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/finance/...`) | Quyền |
|---|---|---|
| TC-01 | `GET branch-prices` · `PUT branch-prices` `{ courseId, branchId, fee \| null }` | `finance.tuition.*` |
| TC-02 | `GET/POST/PUT installment-plans` | `finance.tuition.*` |
| TC-03 | `GET/POST/PUT discounts` · `POST discounts/{id}/toggle` · `GET discounts/check?code=&courseId=&branchId=` | `finance.tuition.*` (check: người ghi danh) |
| TC-05 | `GET invoices?status=&branchId=&classId=&from=&to=&q=&page=` · `GET invoices/{id}` | `finance.invoices.view` |
| TC-06 | `POST invoices/{id}/payments` + `Idempotency-Key` | `finance.invoices.update` |
| TC-07 | `POST invoices/{id}/refunds` → `{ refund }` hoặc `{ approvalRequestId }` | `.update` (tạo) · duyệt qua `system/approvals/{id}/approve` (`finance.invoices.approve`) |
| TC-08 | `POST invoices/{id}/void-requests` `{ reason }` · khôi phục qua Thùng rác | như trên |
| TC-09 | cổng `GET portal/invoices` · `GET portal/invoices/{id}/qr` | chủ tài khoản |
| TC-10 | webhook `POST integrations/bank/transactions` · `GET bank-transactions?status=` · `GET bank-transactions/{id}/suggestions` · `POST bank-transactions/{id}/match|ignore|unmatch` | `finance.reconciliation.*` |
| TC-11 | `GET debts?groupBy=payer|class|branch&aging=` · `POST reminders` `{ invoiceIds, channel, note }` | `finance.debts.*` |
| TC-12 | `GET/PUT commission-tiers` · `PUT sale-targets` · `GET commissions?month=` · `PUT invoices/{id}/sale` | `finance.commissions.*` |
| TC-13, 14 | `GET payroll/{teacher|staff}?month=&branchId=` · `POST/DELETE pay-adjustments` · `POST payroll/{kind}/{month}/lock|unlock|paid` `{ branchIds }` · cổng GV `GET portal/payroll?month=` | `finance.teacher-payroll.*` / `finance.staff-payroll.*` |

- JSON khớp `frontend/src/features/finance/types.ts` và `features/student/types.ts › Invoice / Payment / Refund`. **Chốt:** hình thức thu dùng `vietqr` ở cả DB và JSON.
- **Chốt — trạng thái hóa đơn** tính khi đọc (`InvoiceSummary`: `total, collected, refunded, paid, remaining, refundable, status`); cột `status` trong DB chỉ lưu
  `open` / `cancelled` / `voided` (database.md đã sửa; Domain `InvoiceStatus` sửa theo khi code module Tài chính).
- **Integration event (Finance.Contracts)** 🆕: `InvoiceIssued`, `PaymentRecorded`, `InvoicePaid`, `InvoiceOverdue`, `RefundIssued`, `InvoiceVoided`, `PayrollLocked`.
- **Interface cho module khác** 🆕: `ITuitionQuery` (còn nợ / tình trạng học phí theo HV, theo ghi danh — Students HV-01, HV-05, HV-14, HV-15; System CN-01).
- **Nghe event:** `StudentEnrolled`, `EnrollmentTransferred`, `EnrollmentDropped`, `ClassCancelled` (Students / Academic) → TC-04 / hủy phần còn lại ·
  `SessionConfirmed` / `SessionUnconfirmed` (Academic) → lương GV tạm tính · `BranchStatusChanged` (System).
- **Job:** nhắc học phí tự động (hằng ngày) · phát `InvoiceOverdue` (hằng ngày) · nhắc hạn chốt lương.
- **Domain (giai đoạn 19 phase 1):** `Invoice` tính tình trạng khi đọc (`Summarize`), còn nợ = phải thu − Σ thu (hoàn không làm tăng nợ), lịch đóng đợt `schedule`,
  yêu cầu chờ duyệt `InvoiceRequest` (hoàn từ ngưỡng / hủy hóa đơn — người tạo không tự duyệt, rút lại được), khôi phục hóa đơn đã hủy; `Discount` (mã, combo,
  giới hạn khóa / chi nhánh / lượt, trả lượt), `InstallmentPlan.ScheduleFor` (làm tròn 1.000đ, đợt cuối nhận phần lẻ), `MatchScorer` (tự khớp + gợi ý),
  `ReminderPlan` (mốc tự nhắc, nhắc bù khi job lỡ ngày), `DebtAgingRule`. Đã có mã lỗi `finance.invoice.pending_approval`, `has_payments`;
  ⏳ `finance.payroll.*` làm cùng lương (giai đoạn 22).
- **API (giai đoạn 19 phase 3):** đã làm TC-01 → TC-11 theo bảng trên, khác đề xuất ở các điểm:
  - Duyệt hoàn / hủy ở hộp duyệt chung: Finance mở phiếu qua `System.Contracts.IApprovalInbox`; System duyệt / từ chối / rút lại thì gọi
    `Finance.Contracts.IInvoiceApprovals` trong cùng request (Finance kiểm lại nghiệp vụ, lỗi → phiếu giữ nguyên). Quyền duyệt = `finance.invoices.approve` tại chi nhánh của hóa đơn.
  - Khớp tay: `POST bank-transactions/{id}/match` nhận `{ invoiceId }` hoặc tách `{ allocations: [{ invoiceId, amount }] }` (tổng = số tiền giao dịch).
  - Webhook `POST integrations/bank/transactions`: header `X-Signature: sha256=<hex HMAC-SHA256(body)>`, khóa `Finance:BankWebhookSecret`; tài khoản nhận → chi nhánh theo `bankAccount`.
  - Cổng HV / PH: `GET portal/invoices?studentId=` trả luôn thông tin VietQR (`qr`), không có endpoint `/qr` riêng.
  - Công nợ: `GET debts?branchId=&classId=` trả từng hóa đơn còn nợ + người trả + tổng theo tuổi nợ (frontend tự gom theo người trả / lớp / chi nhánh).
  - Tự nhắc: job Quartz 08:00 hằng ngày (`POST reminders/auto` để chạy tay). Nợ quá hạn → Students đọc qua `Finance.Contracts.ITuitionQuery` khi tính cảnh báo bỏ học (thay cho event `InvoiceOverdue`).
  - ⏳ Còn lại: áp mã ưu đãi / chính sách đóng đợt khi ghi danh (sự kiện `StudentEnrolled` chưa mang mã / chính sách — làm cùng phase 4),
    gửi nhắc thật qua Zalo / SMS / email (module Thông báo), khôi phục hóa đơn đã hủy từ Thùng rác, `integration_logs` cho webhook.
- **Frontend (giai đoạn 19 phase 4):** `features/finance/financeApi.ts` nạp hóa đơn / bảng giá / giao dịch / học phí cổng vào store cũ; thu tiền, hoàn / hủy (phiếu ở
  Hệ thống › Duyệt thao tác — mọi loại phiếu duyệt / từ chối / rút lại qua server), khớp / bỏ qua / gỡ khớp, nhắc học phí, giá / ưu đãi / đóng đợt gọi API.
  Ghi danh / cho nghỉ / chuyển lớp / lên lớp / chốt lead không còn tạo hóa đơn giả — tải lại hóa đơn sau khi Finance lập ở nền; chốt lead "thu ngay" chờ hóa đơn rồi ghi khoản thu.
  Đối soát: bỏ nút mô phỏng giao dịch (giao dịch thật đến qua webhook), "Đưa về chưa khớp" cho giao dịch đã bỏ qua nhầm. Cổng HV / PH: VietQR theo tài khoản chi nhánh server trả.
