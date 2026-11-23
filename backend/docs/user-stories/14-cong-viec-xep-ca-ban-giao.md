# 14 · Công việc: task / Kanban, giao việc cấp dưới, xếp ca, bàn giao ca

Module: **Work** · Bảng: `work.tasks`, `task_checklist_items`, `task_comments`, `shift_assignments`, `shift_registrations`, `handovers`, `handover_leads`, `handover_items`
([database.md](../database.md))
Frontend: `pages/staff/work/{tasks,team-tasks,shifts,handover}`, trang cá nhân `pages/staff/me/{MyTasksPage,MyShiftAvailabilityPage}.tsx`,
`features/work/{types,constants,workStore,TaskDrawer}.ts(x)`, `features/me/TaskItem.tsx` · Giai đoạn 21 ([ke-hoach.md](../ke-hoach.md))

## Mô hình (đọc trước)

```
WorkTask ── người làm (1) · người giao · người theo dõi · checklist · bình luận · gắn lead / lớp / HV · lặp ngày / tuần / tháng
   Cần làm → Đang làm → Chờ duyệt → Hoàn thành   (hoàn thành task lặp → sinh lần kế tiếp)
ShiftAssignment ── NV "làm theo ca" × ngày × ca (sáng / chiều / tối)       ShiftRegistration ── NV tự báo rảnh / bận từng ca
Handover ── cuối ca: tiền mặt (hệ thống tính / NV đếm / người nhận đếm lại), lead đang dở, việc tồn, sự cố → ca sau xác nhận
```

**Quy tắc vàng:**
1. **3 trang task tách nhau** (cùng 1 bảng): *Task toàn hệ thống* (`work.tasks`, giao cho mọi NV trong phạm vi) · *Giao việc cấp dưới*
   (không cần mã quyền — chỉ giao cho người **dưới quyền** theo cây "quản lý trực tiếp", cả gián tiếp) · *Việc của tôi* (chỉ task giao cho mình).
2. Người dính tới task (người làm, người giao, người theo dõi) luôn xem / bình luận được, kể cả không có `work.tasks`.
3. Xếp ca chỉ cho NV có cờ **làm theo ca** (Hr). Báo rảnh / bận là **nguyện vọng**, không thay lịch ca; ca đã xếp chỉ đổi qua người xếp ca hoặc đơn đổi ca (DN-01).
4. Không sửa ca / báo rảnh cho **ngày đã qua** — sai công thì sửa ở Chấm công (CC-04).
5. Tiền mặt hệ thống tính lấy từ **phiếu thu tiền mặt** (Finance) trong khung giờ ca — không cho nhập tay số "hệ thống".

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| CV-01 | Tạo / sửa task (toàn hệ thống) | Nhân sự, quản lý | Cao |
| CV-02 | Kanban, đổi trạng thái, hoàn thành, task lặp | Người làm, người giao | Cao |
| CV-03 | Việc của tôi | Mọi NV | Cao |
| CV-04 | Giao việc cấp dưới, duyệt hoàn thành / yêu cầu làm lại | Quản lý | Cao |
| CV-05 | Checklist, bình luận, người theo dõi | Người dính tới task | TB |
| CV-06 | Nhắc hạn | Hệ thống | TB |
| XC-01 | Lưới xếp ca theo tuần | Quản lý ca | Cao |
| XC-02 | Xếp theo đăng ký, chép tuần trước | Quản lý ca | TB |
| XC-03 | Kiểm tra đủ người mỗi ca | Quản lý ca | TB |
| XC-04 | Đăng ký ca rảnh / bận của tôi | NV theo ca | TB |
| XC-05 | Đổi ca (đơn DN-01 đã duyệt) | Hệ thống | TB |
| BG-01 | Tạo bàn giao ca | NV theo ca | Cao |
| BG-02 | Nhận ca, đếm lại tiền | NV ca sau | Cao |
| BG-03 | Danh sách bàn giao, lệch tiền | Quản lý, kế toán | TB |

**Quyền:** `work.tasks` (view / create / update / delete) · `work.shifts` (view xem lưới · update xếp ca) · `work.handover` (view danh sách · create tạo).
Phạm vi `own` / `branch` / `all`: task theo chi nhánh của **người làm**; ca, bàn giao theo chi nhánh.

---

## CV-01 · Tạo / sửa task

**Nguồn:** Công việc › Task · **Quyền:** `work.tasks.create` / `.update`

Trường: tiêu đề* (≤ 200), mô tả, người làm* (NV chưa nghỉ, trong phạm vi), người theo dõi, ưu tiên (khẩn / cao / bình thường / thấp), hạn (ngày),
lặp lại (không / ngày / tuần / tháng — cần hạn), gắn với (lead / lớp / HV — chọn trong danh sách), checklist ban đầu.
Mã `TK-{số}`; chi nhánh = chi nhánh của người làm. Người làm nhận thông báo "được giao việc".

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CV-01.E1 | Thiếu tiêu đề | `work.task.title_required` |
| CV-01.E2 | Người làm đã nghỉ việc / ngoài phạm vi | `work.task.assignee_invalid` |
| CV-01.E3 | Lặp lại mà không có hạn | `work.task.repeat_needs_due` |
| CV-01.E4 | Gắn lead / lớp / HV không tồn tại | `work.task.link_not_found` |
| CV-01.E5 | Đổi người làm | Người mới nhận thông báo; người cũ không còn dính tới task (trừ khi là người giao / theo dõi) |
| CV-01.E6 | Xóa task | `work.tasks.delete` hoặc người giao; vào Thùng rác |

## CV-02 · Kanban & trạng thái

Kéo thẻ giữa 4 cột (hoặc chọn trạng thái trong chi tiết). Lọc: tất cả (`work.tasks.view`) / tôi làm / tôi giao / tôi theo dõi, người làm, ưu tiên;
sắp theo ưu tiên rồi hạn. Thống kê: quá hạn, hạn hôm nay, chờ duyệt, xong 7 ngày.

**Ai được đổi trạng thái:** người làm, người giao, người có `work.tasks.update` (phạm vi).

- **Hoàn thành** → ghi giờ xong; task lặp → tạo lần kế tiếp: hạn + 1 ngày / tuần / tháng, checklist bỏ tick, cùng người làm / theo dõi / gắn.
- Mở lại task đã xong (→ Đang làm) → bỏ giờ xong; lần lặp đã sinh **không** bị xóa.
- Người làm chuyển **Chờ duyệt** → người giao nhận thông báo; người giao / quản lý chuyển Hoàn thành → người làm nhận thông báo.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CV-02.E1 | Người không dính tới task, không có quyền | `work.task.forbidden` (403) |
| CV-02.E2 | Hoàn thành task lặp 2 lần (mở lại rồi xong lại) | Chỉ sinh lần kế tiếp **1 lần** cho mỗi task |

## CV-03 · Việc của tôi

**Nguồn:** Cá nhân › Việc của tôi · không cần mã quyền. Task giao cho mình, nhóm theo hạn: quá hạn / hôm nay / tuần này / sau / không hạn / đã xong gần đây.
Bắt đầu làm, tick checklist, bình luận, **xác nhận hoàn thành** (task người khác giao → có thể chọn "gửi duyệt" = Chờ duyệt).

## CV-04 · Giao việc cấp dưới

**Nguồn:** Công việc › Giao việc cấp dưới · không cần mã quyền

- Danh sách cấp dưới (trực tiếp trước, rồi gián tiếp) kèm số việc mở / quá hạn. Không có cấp dưới → trang trống.
- Giao task cho **nhiều người** một lần → mỗi người 1 task riêng. Người làm phải là cấp dưới (`work.task.not_your_report`, 403).
- Task cấp dưới ở **Chờ duyệt** → "Duyệt xong" (Hoàn thành); task mình giao đã xong → "Yêu cầu làm lại" (Đang làm, bình luận lý do).
- Lọc "chỉ task tôi giao" / mọi task của cấp dưới.

## CV-05 · Checklist, bình luận, người theo dõi

Người dính tới task: thêm / tick / xóa mục checklist, bình luận (≤ 2000 ký tự). Bình luận → thông báo những người còn lại.
Người giao / `work.tasks.update` đổi người theo dõi.

## CV-06 · Nhắc hạn

Job **07:30 hằng ngày**: mỗi người làm nhận 1 thông báo gom "N việc hôm nay, M việc quá hạn" (task chưa xong). Không có → không gửi.
NV nghỉ việc (`EmployeeQuit`): task chưa xong của họ **giữ nguyên**, người giao nhận thông báo để giao lại (NS-07.E5).

---

## XC-01 · Lưới xếp ca theo tuần

**Nguồn:** Công việc › Xếp ca · **Quyền:** `work.shifts.view` (xem) / `.update` (xếp)

Hàng = NV làm theo ca của chi nhánh (chưa nghỉ), cột = 7 ngày (T2–CN); mỗi ô 3 nút S / C / T. Ô đã đăng ký rảnh viền đứt, báo bận gạch chéo,
ngày nghỉ phép (đơn đã duyệt) đánh dấu. Bấm → xếp / bỏ ca. Chuyển tuần trước / sau.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| XC-01.E1 | Ngày đã qua | `work.shift.past_date` |
| XC-01.E2 | NV đã báo bận ca đó | `work.shift.busy` — chọn người khác / hỏi lại NV (NV đổi sang rảnh) |
| XC-01.E3 | NV không làm theo ca / đã nghỉ / khác chi nhánh | `work.shift.employee_invalid` |
| XC-01.E4 | Xếp trùng (đã có) | Bỏ qua, trả ca có sẵn |
| XC-01.E5 | NV nghỉ phép ngày đó | Cho xếp, cảnh báo trên ô |
| XC-01.E6 | Bỏ ca đang có đơn đổi ca chờ duyệt | `work.shift.swap_pending` |

## XC-02 · Xếp theo đăng ký, chép tuần trước

- **Xếp theo đăng ký:** mọi ca NV báo **rảnh** trong tuần đang xem (từ hôm nay) và chưa xếp → xếp. Trả số ca thêm.
- **Chép tuần trước:** ca tuần trước → cùng thứ tuần này, chỉ ô **còn trống**, bỏ qua ca NV báo bận, ngày đã qua, NV đã nghỉ. Trả số ca thêm.
- Cả hai là **1 request** (không gọi từng ô).

## XC-03 · Kiểm tra đủ người

Dòng cuối lưới: số người mỗi ca so với tối thiểu theo bộ phận — Sale: T2–T6 S1 / C1 / T2, T7–CN 1 / 1 / 1; Lễ tân: T2–T6 1 / 1 / 0, T7–CN 1 / 0 / 0.
Thiếu → đỏ. (Định mức để trong code Work; ⏳ đưa vào Cài đặt chung khi cần sửa.)

## XC-04 · Đăng ký ca rảnh / bận của tôi

**Nguồn:** Cá nhân › Đăng ký lịch rảnh · không cần mã quyền · chỉ NV làm theo ca (`work.shift.not_shift_based`)
Lưới 2 tuần (tuần này + tuần sau), mỗi ca chọn **Rảnh / Bận / bỏ chọn**. Ngày đã qua khóa (`work.shift.past_date`).
Bận ở ca đã được xếp → vẫn lưu, quản lý thấy cảnh báo trên lưới (không tự gỡ ca).

## XC-05 · Đổi ca

Nghe `ShiftSwapApproved` (Hr, DN-02): 2 ca đổi người cho nhau (`ShiftAssignment.SwapTo`). Một trong 2 ca không còn / đã đổi người → bỏ qua, ghi log
(Hr đã kiểm `swap_stale` lúc duyệt). Hai người nhận thông báo lịch mới. NV nghỉ việc (`EmployeeQuit`) → **gỡ ca từ hôm nay trở đi**.

---

## BG-01 · Tạo bàn giao ca

**Nguồn:** Công việc › Bàn giao ca · **Quyền:** `work.handover.create` (NV theo ca)

1. Bấm "Tạo bàn giao" → hệ thống gợi ý ca của tôi hôm nay, người nhận = NV có ca **kế tiếp** cùng chi nhánh hôm nay.
2. **Tiền mặt hệ thống** = quỹ đầu ca (1.000.000đ, Cài đặt chung) + tổng phiếu thu **tiền mặt** của chi nhánh trong khung giờ ca (Finance). Server tính, không nhận từ client.
3. NV nhập tiền **đếm thực tế**, ghi chú lệch; lead đang dở (lead chưa chốt của chi nhánh + ghi chú); việc tồn; sự cố.
4. Lưu → người nhận được thông báo.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| BG-01.E1 | Người nhận là chính mình / không làm theo ca / khác chi nhánh | `work.handover.invalid_receiver` |
| BG-01.E2 | Đã bàn giao ca này hôm nay | `work.handover.duplicate` |
| BG-01.E3 | Đếm lệch với hệ thống mà không ghi chú | `work.handover.note_required` |
| BG-01.E4 | Lead không thuộc chi nhánh / đã chốt | `work.handover.lead_invalid` |

## BG-02 · Nhận ca

Chỉ **người nhận**: nhập tiền tự đếm lại + ghi chú → **Đã nhận** nếu đếm lại = NV đếm = hệ thống; ngược lại **Lệch tiền** (`disputed`) →
thông báo người có `work.handover.update` của chi nhánh (quản lý / kế toán). Tick việc tồn đã xử lý (người nhận, sau khi nhận).
Lỗi: `work.handover.not_receiver` (403), `work.handover.closed` (409).

## BG-03 · Danh sách bàn giao

`work.handover.view` (phạm vi chi nhánh); NV không có quyền xem vẫn thấy bàn giao **mình gửi / mình nhận**. Lọc trạng thái; khối "Chờ tôi nhận" ở đầu trang.
⏳ Kế toán ghi kết luận lệch tiền: chưa làm (đối chiếu ngoài hệ thống).

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/work/...`) | Quyền |
|---|---|---|
| CV-01, CV-02 | `GET tasks?scope=all\|mine\|assigned\|watching&assigneeId=&priority=&status=` · `POST tasks` · `PUT tasks/{id}` · `POST tasks/{id}/status` `{ status, comment? }` · `DELETE tasks/{id}` | `work.tasks.*` / người dính tới task |
| CV-03 | `GET me/tasks` | Đăng nhập |
| CV-04 | `GET team/reports` · `GET team/tasks?onlyMine=` · `POST team/tasks` `{ assigneeIds[], … }` | Đăng nhập (cấp dưới) |
| CV-05 | `POST tasks/{id}/checklist` · `PUT tasks/{id}/checklist/{itemId}` · `DELETE …` · `POST tasks/{id}/comments` | Người dính tới task |
| XC-01..03 | `GET shifts?branchId=&week=YYYY-MM-DD` → `{ employees, assignments, registrations, leaves, coverage }` · `POST shifts/toggle` · `POST shifts/apply-registrations` · `POST shifts/copy-week` | `work.shifts.view / update` |
| XC-04 | `GET me/shift-registrations?from=` · `PUT me/shift-registrations` `{ date, shift, status: available\|busy\|null }` | Đăng nhập (NV theo ca) |
| BG-01..03 | `GET handovers?status=&branchId=` · `GET handovers/draft` (gợi ý ca, người nhận, tiền hệ thống, lead mở) · `POST handovers` · `POST handovers/{id}/receive` · `PUT handovers/{id}/items/{itemId}` | `work.handover.*` / người gửi, người nhận |

- JSON khớp `Task`, `ShiftAssignment`, `ShiftAvailability`, `Handover` ở `frontend/src/features/work/types.ts`.
- **Giữa module:** Work đọc `Hr.Contracts › IEmployeeDirectory` (🆕 `managerId`, `shiftBased`, `ReportsOfAsync`), 🆕 `IHrLeaveCalendar` (ngày nghỉ đã duyệt);
  Crm / Academic / Students lookup cho gắn task / lead bàn giao; 🆕 `Finance.Contracts › ICashLedger.CashCollectedAsync(branch, from, to)`; System settings (quỹ đầu ca).
  Work cung cấp 🆕 `Work.Contracts › IShiftSchedule` (Hr đọc cho chấm công, đơn đổi ca); nghe `ShiftSwapApproved`, `EmployeeQuit`.
