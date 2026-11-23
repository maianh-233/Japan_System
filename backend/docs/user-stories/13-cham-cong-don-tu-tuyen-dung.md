# 13 · Chấm công, đơn từ & phê duyệt, tuyển dụng, onboarding

Module: **Hr** · Bảng: `hr.time_records`, `hr_requests`, `hr_request_steps`, `jobs`, `candidates`, `candidate_notes`, `onboardings`, `onboarding_items`
([database.md](../database.md))
Frontend: `pages/staff/hr/{timesheets,requests,recruitment,onboarding}`, trang cá nhân `pages/staff/me/MyAttendancePage.tsx`,
`features/hr/{types,constants,hrUtils,hrStore}.ts` · Giai đoạn 21 ([ke-hoach.md](../ke-hoach.md)) · Story nhân sự gốc: [04](04-nhan-su.md)

## Mô hình (đọc trước)

```
Employee (kind = staff) ── TimeRecord (1 / ngày: giờ vào, giờ ra, sửa tay có lý do)
         │                     └─ so với "giờ phải làm" của ngày: giờ hành chính (Cài đặt chung) hoặc ca được xếp (Work)
         └─ HrRequest (đơn từ) ── ApprovalStep 1..n (duyệt lần lượt) ── duyệt xong → áp dụng (trừ phép, đổi ca, miễn vi phạm)
Job (tin tuyển) ── Candidate (Ứng tuyển → Sàng lọc → Phỏng vấn → Offer → Nhận việc / Loại)
                        └─ nhận việc → Onboarding (checklist) ── "Tạo hồ sơ" (= NS-03) → Employee
```

**Quy tắc vàng:**
1. **Trang cá nhân tách trang quản lý** (Cá nhân › Chấm công của tôi ≠ Nhân sự › Chấm công). Trang cá nhân không cần mã quyền — chỉ thấy dữ liệu của chính mình.
2. **Giờ lấy ở server** (`IClock`, đổi sang giờ Việt Nam): nút check-in không nhận giờ từ trình duyệt.
3. **Giáo viên / trợ giảng không chấm công văn phòng** — chấm công theo buổi dạy (story 07). Đơn từ của GV đi đường cổng GV (DT-xx).
4. Trạng thái ngày (đúng giờ, muộn, về sớm, quên ra, vắng, nghỉ phép) **tính khi đọc**, không lưu — đổi ca / duyệt đơn / sửa công là bảng công đổi theo.
5. Đơn chỉ được **người đến lượt** quyết định; người có `hr.requests.approve` duyệt thay được (ghi tên người duyệt thật).

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| CC-01 | Check-in / check-out | Nhân viên | Cao |
| CC-02 | Bảng công tháng của tôi | Nhân viên | Cao |
| CC-03 | Bảng công toàn bộ nhân viên | Nhân sự, quản lý | Cao |
| CC-04 | Sửa công | Nhân sự | TB |
| DN-01 | Tạo đơn (nghỉ phép, muộn / sớm, công tác, đề xuất, tạm ứng, đổi ca) | Nhân viên | Cao |
| DN-02 | Duyệt / từ chối nhiều cấp | Quản lý, Phó GĐ, Kế toán trưởng, GĐ | Cao |
| DN-03 | Hủy đơn | Người gửi | TB |
| DN-04 | Danh sách đơn | Nhân sự, người duyệt | TB |
| TD-01 | Tin tuyển dụng | Nhân sự | TB |
| TD-02 | Ứng viên & pipeline | Nhân sự | TB |
| TD-03 | Phỏng vấn, offer | Nhân sự | TB |
| TD-04 | Nhận việc / loại | Nhân sự | TB |
| OB-01 | Phiếu onboarding & checklist | Nhân sự, người hướng dẫn | TB |
| OB-02 | Tạo hồ sơ từ onboarding (= NS-12) | Nhân sự | TB |

**Quyền:** `hr.timesheets` (view bảng công · update sửa công · export) · `hr.requests` (view danh sách · approve duyệt thay) ·
`hr.recruitment` (view / create / update / delete) · `hr.onboarding` (view / create / update). Phạm vi `own` / `branch` / `all` như Nhân sự (NS).
Trang cá nhân (CC-01, CC-02, DN-01, DN-03, "Đơn của tôi") không cần mã quyền.

---

## CC-01 · Check-in / check-out

> Là **nhân viên văn phòng / làm theo ca**, tôi muốn **bấm check-in khi đến và check-out khi về** để **có công tính lương**.

**Nguồn:** Cá nhân › Chấm công của tôi · **Quyền:** đăng nhập cổng nhân viên, tài khoản gắn hồ sơ `kind = staff`

**Các bước:**
1. Trang hiện giờ hiện tại, "giờ phải làm" hôm nay (HC 08:00–17:30 hoặc ca S/C/T đã xếp), trạng thái.
2. Bấm **Check-in** → server ghi giờ vào = giờ hiện tại (VN), chi nhánh = chi nhánh chính của nhân viên. Muộn quá `lateGraceMin` → báo "muộn N phút".
3. Bấm **Check-out** → ghi giờ ra. Bấm lần nữa → cập nhật giờ ra (về muộn hơn dự kiến).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CC-01.E1 | Tài khoản không gắn hồ sơ / hồ sơ là GV, trợ giảng | `hr.time_record.not_staff` (403) — GV chấm công theo buổi dạy |
| CC-01.E2 | Đã nghỉ việc / nghỉ dài hạn | `hr.time_record.inactive` |
| CC-01.E3 | Check-out khi chưa check-in | `hr.time_record.not_checked_in` |
| CC-01.E4 | Bấm check-in 2 lần | Lần 2 trả bản ghi cũ (không đổi giờ vào) |
| CC-01.E5 | Hôm nay nghỉ phép đã duyệt / không có ca | Vẫn cho chấm (đi làm bù) — bảng công tính "có công" |
| CC-01.E6 | Qua nửa đêm chưa check-out | Hôm sau là ngày mới; hôm trước thành "Quên check-out" |
| CC-01.E7 | Giới hạn chấm tại trung tâm (IP / GPS) | ⏳ Chưa làm — ghi nhận IP vào nhật ký để đối chiếu |

## CC-02 · Bảng công tháng của tôi

**Các bước:** chọn tháng (không xem tháng tương lai) → lịch từng ngày: giờ vào / ra, trạng thái; thống kê ngày công, số lần muộn (tổng phút),
về sớm, quên check-out, vắng không phép, phép năm còn lại; danh sách **ngày cần giải trình** (muộn, sớm, quên ra, vắng) → nút tạo đơn sẵn ngày.

**Cách tính trạng thái 1 ngày** (dùng chung CC-02, CC-03):

| Điều kiện (theo thứ tự) | Trạng thái |
|---|---|
| Có đơn nghỉ phép / công tác đã duyệt phủ ngày | `leave` |
| Ngày ở tương lai | `future` |
| Không có giờ phải làm (CN, T7 không làm, NV theo ca không có ca) | có bản ghi → `ok`, không → `off` |
| Chưa check-in | hôm nay & chưa hết giờ → `future`; còn lại → `absent` |
| Chưa check-out | hôm nay → `working`; ngày trước → `no_out` |
| Muộn > `lateGraceMin` / về sớm > `lateGraceMin` (trừ khi có đơn muộn / sớm đã duyệt ngày đó) | `late` / `early` / `late_early`; còn lại `ok` |

- Giờ phải làm: NV theo ca = từ giờ bắt đầu ca sớm nhất đến giờ kết thúc ca muộn nhất được xếp ngày đó; NV hành chính = `office.from`–`office.to`
  (T7: đến `office.satTo`, không có = nghỉ; CN nghỉ). Giờ ca: sáng 08:00–12:00, chiều 13:00–17:30, tối 17:30–21:30.
- Giờ công = giờ ra − giờ vào (chỉ ngày có cả 2).

## CC-03 · Bảng công toàn bộ nhân viên

**Nguồn:** Nhân sự › Chấm công · **Quyền:** `hr.timesheets.view` (phạm vi chi nhánh)

Lưới nhân viên × ngày của tháng (nhân viên `kind = staff` chưa nghỉ, trong phạm vi), lọc phòng ban, "chỉ người có vi phạm";
cột tổng: công, muộn (kèm tổng phút), sớm, quên ra, vắng, phép, giờ. Ô sửa tay có viền + tooltip "sửa bởi … : lý do".
Bảng công tháng là căn cứ tính **Lương nhân viên** (giai đoạn 22 đọc qua `Hr.Contracts`).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CC-03.E1 | Nhân viên nghỉ việc giữa tháng | Vẫn hiện tháng đó (đến ngày nghỉ); tháng sau không hiện |
| CC-03.E2 | Xuất Excel | ⏳ `hr.timesheets.export` — làm cùng báo cáo (giai đoạn 24) |

## CC-04 · Sửa công

**Quyền:** `hr.timesheets.update` · Bấm ô → nhập giờ vào / ra (bỏ trống được) + **lý do bắt buộc** → lưu, ghi người sửa.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CC-04.E1 | Không có lý do | `hr.time_record.reason_required` |
| CC-04.E2 | Giờ ra trước giờ vào | `hr.time_record.invalid_checkout` |
| CC-04.E3 | Ngày tương lai | `hr.time_record.future_date` |
| CC-04.E4 | Ngày chưa có bản ghi | Tạo bản ghi mới (đánh dấu sửa tay) |
| CC-04.E5 | Sửa công của chính mình | `hr.time_record.self_edit` (403) — nhờ người khác sửa |

---

## DN-01 · Tạo đơn

> Là **nhân viên**, tôi muốn **gửi đơn nghỉ phép / giải trình / tạm ứng** và **biết đơn đang chờ ai duyệt**.

**Nguồn:** Cá nhân › Chấm công của tôi (tab Đơn của tôi), Nhân sự › Đơn từ (người có `hr.requests.create` tạo hộ nhân viên trong phạm vi) ·
đơn đổi ca tạo ở Xếp ca / Đơn từ.

| Loại | Trường | Ghi chú |
|---|---|---|
| `leave` nghỉ phép | từ ngày – đến ngày*, có lương?*, lý do* | Số ngày = ngày làm việc trong khoảng (bỏ CN), server tính |
| `late_early` đi muộn / về sớm | ngày*, lý do* | Duyệt xong → ngày đó không tính vi phạm |
| `business_trip` công tác | từ – đến*, lý do* | Duyệt xong → các ngày đó tính `leave` (không trừ phép) |
| `proposal` đề xuất | tiêu đề*, số tiền (≥ 0), lý do* | |
| `advance` tạm ứng | số tiền* (> 0), lý do* | |
| `shift_swap` đổi ca | ca của tôi*, đồng nghiệp*, ca của đồng nghiệp*, lý do* | Chỉ NV theo ca, cùng chi nhánh, ca từ hôm nay trở đi |

**Cấp duyệt (server dựng khi tạo, lưu vào đơn):**
1. **Quản lý trực tiếp** (`managerId` của người gửi).
2. Nghỉ phép **> 3 ngày** → thêm **Phó giám đốc**.
3. Tạm ứng → thêm **Kế toán trưởng**.
4. Đề xuất **> 5.000.000đ** → thêm **Kế toán trưởng**, rồi **Giám đốc**.

- Người giữ chức vụ tìm theo **vị trí** trong hồ sơ (nhân viên chưa nghỉ, vị trí bắt đầu bằng "Phó giám đốc" / "Kế toán trưởng" / "Giám đốc").
  Không tìm thấy, hoặc người đó chính là người gửi → bước **không có người duyệt cố định**: người có `hr.requests.approve` (phạm vi chi nhánh người gửi) duyệt.
  Hai bước liên tiếp cùng 1 người → gộp 1 bước.
- Mã đơn `DT-{năm}{số 4 chữ số}`. Gửi xong → thông báo người duyệt bước 1 (chuông).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DN-01.E1 | Ngày kết thúc trước ngày bắt đầu | `hr.request.invalid_dates` |
| DN-01.E2 | Nghỉ có lương vượt phép năm còn lại | `hr.request.leave_exceeded` — gợi ý bỏ "có lương" |
| DN-01.E3 | Khoảng nghỉ không có ngày làm việc nào (chỉ CN) | `hr.request.no_workdays` |
| DN-01.E4 | Trùng đơn nghỉ / công tác đang chờ hoặc đã duyệt | `hr.request.overlap` |
| DN-01.E5 | Đề xuất / tạm ứng thiếu số tiền hợp lệ | `hr.request.amount_required` |
| DN-01.E6 | Đổi ca: ca không phải của mình / của đồng nghiệp, ca đã qua, khác chi nhánh, đổi với chính mình | `hr.request.invalid_swap` |
| DN-01.E7 | Người gửi là GV / trợ giảng | `hr.request.not_staff` — dùng đơn ở cổng GV |
| DN-01.E8 | Nghỉ phép ngày đã qua (bổ sung đơn sau khi vắng) | Cho phép (≤ 30 ngày trước) — để giải trình |

## DN-02 · Duyệt / từ chối

**Nguồn:** Nhân sự › Đơn từ, tab "Chờ tôi duyệt" · **Quyền:** người của bước hiện tại; hoặc `hr.requests.approve` (duyệt thay)

1. Duyệt → bước hiện tại xong; còn bước sau → thông báo người duyệt kế tiếp. Bước cuối → đơn **Đã duyệt** + áp dụng:
   - nghỉ phép có lương → **trừ phép năm** (`Employee.UseLeave`);
   - đổi ca → phát `ShiftSwapApproved` → Work đổi người của 2 ca;
   - thông báo người gửi.
2. Từ chối → **bắt buộc lý do**, đơn **Từ chối** ngay (bỏ các bước sau), thông báo người gửi.

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| DN-02.E1 | Chưa tới lượt mình, không có quyền duyệt thay | `hr.request.not_your_turn` (403) |
| DN-02.E2 | Đơn đã xử lý / đã hủy | `hr.request.not_pending` (409) |
| DN-02.E3 | Tự duyệt đơn của mình (kể cả duyệt thay) | `hr.request.self_approve` (403) |
| DN-02.E4 | Từ chối không lý do | `hr.request.reason_required` |
| DN-02.E5 | Lúc duyệt bước cuối phép năm không còn đủ (đơn khác vừa trừ) | `hr.request.leave_exceeded` — người duyệt từ chối hoặc người gửi tạo lại không lương |
| DN-02.E6 | Đổi ca: 1 trong 2 ca đã bị đổi người / xóa / đã qua | `hr.request.swap_stale` — đơn không duyệt được, từ chối |
| DN-02.E7 | Duyệt thay | Bước ghi người duyệt thật + "(duyệt thay)" |

## DN-03 · Hủy đơn

Người gửi hủy khi đơn còn **Chờ duyệt** (`hr.request.only_owner`, `hr.request.not_pending`). Đơn đã duyệt muốn hủy → tạo đơn khác / nhờ nhân sự (⏳ thu hồi đơn đã duyệt chưa làm).

## DN-04 · Danh sách đơn

Tab **Chờ tôi duyệt** (đến lượt mình, + bước không cố định nếu có `hr.requests.approve`) · **Đơn của tôi** · **Tất cả** (`hr.requests.view`, phạm vi theo chi nhánh người gửi).
Lọc loại; thống kê: chờ tôi duyệt, nghỉ phép tuần này (đã duyệt), tổng tiền đang chờ (đề xuất + tạm ứng).

---

## TD-01 · Tin tuyển dụng

**Quyền:** `hr.recruitment.create / update / delete` · Trường: vị trí*, chi nhánh*, phòng ban*, loại nhân sự*, số lượng* (> 0), khoảng lương, hạn nộp* (≥ ngày đăng),
kênh đăng, mô tả. Trạng thái: **Đang tuyển ↔ Tạm dừng → Đã đóng** (mở lại được). Xóa → Thùng rác (chỉ khi chưa có ứng viên, `hr.job.has_candidates`).
Danh sách hiện số đã nhận / cần tuyển, số đang phỏng vấn / chờ offer.

## TD-02 · Ứng viên & pipeline

Thêm ứng viên vào tin: họ tên*, SĐT*, email*, nguồn*, CV (file ≤ 10 MB, PDF / Word), trình độ tiếng Nhật, lương mong muốn.
Kanban theo giai đoạn, kéo thả: vào **Phỏng vấn** bắt hẹn lịch (TD-03), vào **Offer** bắt nhập offer, vào **Nhận việc** = TD-04, vào **Loại** = TD-04.
Ghi chú theo thời gian (người ghi).

| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TD-02.E1 | Thêm ứng viên vào tin đã đóng | `hr.job.closed` |
| TD-02.E2 | Cùng SĐT / email đã ứng tuyển tin này | `hr.candidate.duplicate` |
| TD-02.E3 | Kéo ứng viên đã nhận việc / đã loại | `hr.candidate.closed` |
| TD-02.E4 | Kéo lùi giai đoạn (vd Phỏng vấn → Sàng lọc) | Cho phép (trừ đã đóng) |

## TD-03 · Phỏng vấn, offer

- **Phỏng vấn:** thời gian*, người phỏng vấn* (nhân viên), hình thức (trực tiếp / online), điểm (0–10), nhận xét. Sửa lại được. Người phỏng vấn nhận thông báo.
- **Offer:** lương*, ngày bắt đầu* (≥ hôm nay) → ghi ngày gửi. Ứng viên **từ chối offer** → Loại, lý do "Từ chối offer".

## TD-04 · Nhận việc / loại

- **Nhận việc** (chỉ từ Offer): ứng viên → Nhận việc, tự tạo **phiếu onboarding** (OB-01) với ngày bắt đầu = ngày offer, người hướng dẫn = người thao tác.
  Số người nhận việc ≥ số lượng cần tuyển → **tự đóng tin**.
- **Loại** (bất kỳ giai đoạn trừ Nhận việc): lý do bắt buộc. ⏳ Email cảm ơn tự động (giai đoạn 22 — kênh email thật).

---

## OB-01 · Phiếu onboarding & checklist

**Quyền:** `hr.onboarding.view / create / update`

- Tạo từ TD-04 hoặc **thêm tay**: họ tên*, SĐT*, email*, vị trí*, phòng ban*, loại*, chi nhánh*, ngày bắt đầu*, người hướng dẫn* (nhân viên).
- Checklist mặc định theo nhóm **Giấy tờ / Tài khoản / Thiết bị / Đào tạo**; mục "Dạy thử / dự giờ" chỉ cho GV / trợ giảng.
  Tick / bỏ tick ghi người + giờ. Người hướng dẫn nhận thông báo khi phiếu tạo.
- Lọc: đang onboarding / đã xong / tất cả. **Xong** = đã tạo hồ sơ + tick đủ 100%.
- Mục **"Tạo tài khoản đăng nhập"** tự tick khi System cấp tài khoản cho hồ sơ đã gắn (`StaffAccountCreated`).

## OB-02 · Tạo hồ sơ từ onboarding

Như **NS-12** (story 04): gọi cùng use case NS-03, kiểm quyền `hr.employees.create` / `hr.teachers.create` theo loại; gắn mã hồ sơ vào phiếu.
Phiếu đã gắn hồ sơ → `hr.onboarding.already_created`. NS-12.E5 hết ⏳ (phiếu lưu server).

---

## Dữ liệu / API (đề xuất)

| Story | API (`/api/v1/hr/...`) | Quyền |
|---|---|---|
| CC-01 | `POST me/check-in` · `POST me/check-out` → `TimeRecord` | Đăng nhập (NV) |
| CC-02 | `GET me/timesheet?month=YYYY-MM` → `{ employee, days[], stats, leaveBalance }` | Đăng nhập |
| CC-03 | `GET timesheets?month=&branchId=&department=&problemsOnly=` → `{ rows: [{ employee, days[], totals }] }` | `hr.timesheets.view` |
| CC-04 | `PUT timesheets/{employeeId}/{date}` `{ checkIn?, checkOut?, reason }` | `hr.timesheets.update` |
| DN-01 | `POST me/requests` · `POST requests` (tạo hộ) · `GET me/requests` | Đăng nhập / `hr.requests.create` |
| DN-02 | `POST requests/{id}/approve` `{ comment? }` · `POST requests/{id}/reject` `{ reason }` | lượt duyệt / `hr.requests.approve` |
| DN-03 | `POST me/requests/{id}/cancel` | Đăng nhập |
| DN-04 | `GET requests?tab=todo\|all&type=&status=&branchId=&page=` | `hr.requests.view` (tab todo: đăng nhập) |
| TD-01 | `GET/POST jobs` · `PUT jobs/{id}` · `POST jobs/{id}/{pause\|reopen\|close}` · `DELETE jobs/{id}` | `hr.recruitment.*` |
| TD-02..04 | `GET jobs/{id}/candidates` · `POST candidates` · `PUT candidates/{id}` · `POST candidates/{id}/{stage\|interview\|offer\|offer-declined\|hire\|reject\|notes\|cv}` | `hr.recruitment.update` |
| OB-01 | `GET/POST onboardings` · `PUT onboardings/{id}/items/{key}` `{ done }` | `hr.onboarding.*` |
| OB-02 | `POST onboardings/{id}/employee` `{ gender, teacherType?, levels?, rateOffline?, rateOnline? }` | `hr.onboarding.update` + `.create` trang NV / GV |

- JSON khớp `TimeRecord`, `HrRequest`, `Job`, `Candidate`, `Onboarding` ở `frontend/src/features/hr/types.ts` (`steps[].approver` = tên, thêm `approverId`).
- **Giữa module:** Hr đọc `ISettingsReader` (giờ hành chính, `lateGraceMin`) từ System.Contracts, ca được xếp qua 🆕 `Work.Contracts › IShiftSchedule`;
  Hr phát 🆕 `ShiftSwapApproved` (Work đổi ca), `NotificationRequested` (chuông). Hr.Contracts 🆕 `ITimesheetReader` cho Lương NV (giai đoạn 22).

### Đã làm — giai đoạn 21 phase 3 (API, test)

- **Đường dẫn:** trang cá nhân `/api/v1/hr/me/{check-in|check-out|timesheet|requests}` (cổng nhân viên, không mã quyền) · `/hr/timesheets` ·
  `/hr/requests?tab=todo|mine|all` · `/hr/jobs`, `/hr/candidates` · `/hr/onboardings`. Đơn tạo hộ: `POST /hr/requests` có `employeeId`.
- **Phạm vi:** chấm công / đơn từ theo người (phạm vi `own` = mình + cấp dưới theo cây quản lý), tuyển dụng / onboarding theo chi nhánh của tin / phiếu.
  Ngoài phạm vi → 404. Tab "all" không có `hr.requests.view` → 403 `hr.request.view_forbidden`; duyệt / từ chối kiểm lượt trong handler.
- **Bảng công:** trả sẵn `days[]` (trạng thái snake_case như `DayState`: `late_early`, `no_out`…), `stats` / `totals`, `problems` (ngày cần giải trình),
  `today`. Giờ phải làm của NV theo ca đọc từ Work (`Work.Contracts.IShiftSchedule`). Ngày trước khi vào làm / sau ngày nghỉ việc = `off`.
  Sửa công: `PUT /hr/timesheets/{employeeId}/{YYYY-MM-DD}` (giờ "HH:mm").
- **Đơn từ:** mã `DT-{năm}{4 số}` (dữ liệu mẫu giữ mã cũ `DT-02xx`). Người giữ chức vụ = NV chưa nghỉ có vị trí bắt đầu bằng "Phó giám đốc" /
  "Kế toán trưởng" / "Giám đốc" (nhiều người → vào làm sớm nhất). Đơn đổi ca: ca từ hôm nay, 2 NV làm theo ca cùng chi nhánh, chưa nằm trong
  đơn đổi khác đang chờ; ngày đơn = ngày 2 ca. Duyệt xong phát `Hr.Contracts.ShiftSwapApproved` → Work đổi người (XC-05).
  Trừ phép kiểm lại ở bước cuối (DN-02.E5). Thông báo chuông: người duyệt bước kế / người có `hr.requests.approve` (bước mở) / người gửi.
- **Tuyển dụng:** xóa tin → Thùng rác (`hr.job`); kéo Kanban `POST candidates/{id}/stage` chỉ cho Ứng tuyển / Sàng lọc / lùi về giai đoạn đã có thông tin;
  phỏng vấn / offer / nhận việc / loại / từ chối offer là thao tác riêng. CV: `POST candidates/{id}/cv` (multipart `file`, PDF / Word ≤ 10 MB),
  mở bằng `GET candidates/{id}/cv/link`. Nhận việc trả `onboardingId` + cảnh báo "tự đóng tin".
- **Onboarding:** "Tạo hồ sơ" gọi lại NS-03 (`CreateEmployeeCommand`): NV → thử việc 2 tháng, lương = lương offer; GV / trợ giảng → bán thời gian,
  cần `levels` + đơn giá; quản lý trực tiếp = người hướng dẫn. Cần thêm `hr.employees.create` hoặc `hr.teachers.create`.
  System cấp tài khoản (`StaffAccountCreated`) → tự tick mục "account" (người tick "Hệ thống").
- ⏳ `ITimesheetReader` cho Lương NV — làm cùng giai đoạn 22. Tạo hồ sơ + gắn phiếu là 2 lần lưu (hồ sơ tạo xong mà gắn lỗi → tạo lại báo trùng email).

### Đã làm — giai đoạn 21 phase 4 (nối frontend)

- `frontend/src/features/hr/hrOpsApi.ts`: nạp đơn từ (tab todo + của tôi + tất cả), tuyển dụng, onboarding sau đăng nhập (theo quyền xem trang);
  bảng công tải theo tháng ở trang. Trạng thái ngày lấy bản server (`serverDays.ts` — `dayState()` ưu tiên) nên Chấm công, Lương NV, Báo cáo, hồ sơ NV
  thống nhất với backend. Duyệt / từ chối / hủy đơn gọi từ mọi trang (cả Hộp duyệt chung) qua hàm store đã thay.
- Trang: check-in / out (giờ server, báo muộn / sớm), sửa công, tạo đơn (của tôi / tạo hộ; đơn đổi ca tải lưới ca 2 tuần của chi nhánh), tab "Chờ tôi duyệt"
  theo lượt server; Tuyển dụng: người phỏng vấn chọn trong nhân sự, CV tải file thật + mở link tạm, nhận việc → sang Onboarding; Onboarding: người
  hướng dẫn, tick, "Tạo hồ sơ" = OB-02.
- Sửa backend: hồ sơ tạo từ onboarding của Sale / Lễ tân là "làm theo ca".
