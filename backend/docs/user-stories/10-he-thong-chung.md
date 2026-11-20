# 10 · Nền chung: Cài đặt chung, Thông báo, Nhật ký hệ thống, Thùng rác

Module: **System** (`system.settings`, `audit_logs`, `trash_items`) · **Notifications** (`notifications.notifications`) ([database.md](../database.md))
Frontend: `pages/staff/system/{settings/SettingsPage,audit-logs/AuditLogsPage,trash/TrashPage}.tsx`, `features/system/{settings,trash,activity}.ts`,
`features/notifications/notificationStore.ts`, `components/layout/NotificationBell.tsx`, `features/portal/realtime.ts`

## Mô hình (đọc trước)

```
Cài đặt chung   system.settings: mỗi mục 1 dòng, khóa "nhóm.mục" (crm.defaultSlaMinutes, atRisk.streak…), giá trị JSON
                 └─ module khác đọc qua System.Contracts.ICenterSettings (đọc lại sau khi lưu, không cần khởi động lại)

Thông báo        module bất kỳ ──(outbox) NotificationRequested──▶ Notifications: lưu 1 dòng / người nhận ──SignalR──▶ chuông
                 người nhận: tài khoản cụ thể · hoặc "ai có quyền X ở chi nhánh Y" (System.Contracts.IStaffAccessDirectory)

Nhật ký          mọi module ghi qua IAuditTrail (đã có) ──▶ system.audit_logs (chỉ thêm) ──▶ trang xem / lọc / xuất

Thùng rác        module sở hữu xóa mềm bản ghi + tạo mục trash_items ──▶ Khôi phục: gọi module sở hữu (ITrashRestorer theo kind)
                                                                     └▶ Quá hạn / Xóa vĩnh viễn: module sở hữu xóa hẳn, rồi bỏ mục
```

**Quy tắc vàng:**
1. Cài đặt là **cấu hình toàn trung tâm** (không theo chi nhánh). Giá trị sai bị chặn ngay khi lưu, không lưu một nửa.
2. Thông báo trong app chỉ là **lối tắt**: mất thông báo không làm mất dữ liệu nghiệp vụ. Gửi thông báo không bao giờ làm hỏng thao tác chính.
3. Nhật ký **chỉ thêm**, không sửa / xóa, kể cả Super Admin.
4. Thùng rác không giữ bản sao dữ liệu: bản ghi gốc vẫn nằm ở module sở hữu (xóa mềm). Lead / HV **ẩn** không vào thùng rác (đọc cờ ẩn ở module đó).

## Tổng quan

| Mã | Tên | Ai | Ưu tiên |
|---|---|---|---|
| CD-01 | Xem cài đặt chung | Admin, quản lý | Cao |
| CD-02 | Sửa cài đặt chung | Admin | Cao |
| CD-03 | Module khác dùng cài đặt | (hệ thống) | Cao |
| TB-01 | Xem chuông thông báo | Mọi tài khoản | Cao |
| TB-02 | Đánh dấu đã đọc / đọc hết | Mọi tài khoản | Cao |
| TB-03 | Nhận thông báo realtime | Mọi tài khoản | Cao |
| TB-04 | Module gửi thông báo | (hệ thống) | Cao |
| TB-05 | Dọn thông báo cũ | (hệ thống) | Thấp |
| NK-01 | Xem / lọc nhật ký hệ thống | Admin, quản lý | Cao |
| NK-02 | Xuất nhật ký | Admin | TB |
| TR-01 | Xem thùng rác | Admin, quản lý | TB |
| TR-02 | Khôi phục | Admin, quản lý | TB |
| TR-03 | Xóa vĩnh viễn | Admin | TB |
| TR-04 | Tự xóa hẳn khi quá hạn | (hệ thống) | TB |

**Quyền:** `system.settings` (view / update) · `system.audit` (view / export) · `system.trash` (view / update = khôi phục / delete = xóa vĩnh viễn).
Thông báo không cần mã quyền: mỗi tài khoản chỉ thấy thông báo của mình.

---

## CD-01 · Xem cài đặt chung

> Là **Admin**, tôi muốn **xem toàn bộ cài đặt chung kèm người sửa gần nhất** để **biết hệ thống đang chạy theo ngưỡng nào**.

**Nguồn:** `SettingsPage.tsx`, `settings.ts` · **Quyền cần:** `system.settings.view` · **Ưu tiên:** Cao

**Các bước:**
1. Mở Hệ thống › Cài đặt chung.
2. Hệ thống trả về 7 nhóm giống `CenterSettings` của frontend: `crm`, `office`, `invoice`, `payroll`, `atRisk`, `parent`, `safety`,
   kèm `updatedAt`, `updatedBy`. Mục chưa từng lưu → giá trị mặc định (`DEFAULT_SETTINGS`).

**Tiêu chí chấp nhận:**
- [ ] Mọi nhân viên đăng nhập đều **đọc** được bộ cài đặt (frontend cần lúc đăng nhập để tính SLA, mã hóa đơn…); trang quản lý cần `system.settings.view`.
- [ ] Cổng GV / HV / PH không đọc được.

**Dữ liệu / API:** `GET /api/v1/system/settings` → `{ settings, updatedAt?, updatedBy? }`

## CD-02 · Sửa cài đặt chung

> Là **Admin**, tôi muốn **đổi ngưỡng và cấu hình chung** để **hệ thống chạy đúng quy định của trung tâm**.

**Nguồn:** `SettingsPage.tsx` · **Quyền cần:** `system.settings.update` · **Ưu tiên:** Cao

**Các bước:**
1. Admin sửa và bấm Lưu → gửi **cả bộ** cài đặt.
2. Hệ thống kiểm tra từng mục, chỉ lưu mục đổi, ghi 1 dòng nhật ký `setting` / `warning` "Đổi cài đặt chung" kèm danh sách trước → sau (nhãn theo `SETTING_LABELS`).
3. Trả bộ cài đặt mới + số mục đã đổi.

**Trường hợp đặc biệt:**
| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| CD-02.E1 | Giá trị ngoài khoảng (bảng dưới) | Chặn, `validation.failed`, lỗi theo trường `atRisk.streak`… — không lưu mục nào |
| CD-02.E2 | Giờ hành chính bắt đầu ≥ kết thúc, hoặc giờ sai dạng HH:mm | `system.settings.office_hours_invalid` |
| CD-02.E3 | Mẫu mã hóa đơn thiếu `{SO}` / `{SO:n}`, hoặc chứa biến lạ | `system.settings.invoice_pattern_invalid` |
| CD-02.E4 | Bỏ học: mức Trung bình ≥ mức Cao | `system.settings.at_risk_levels_invalid` |
| CD-02.E5 | Không đổi gì | Thành công, `changed = 0`, không ghi nhật ký |
| CD-02.E6 | 2 người cùng lưu | Người sau ghi đè từng mục (cài đặt ít khi sửa, không cần chống sửa đè); nhật ký thấy cả 2 lần |

**Khoảng hợp lệ:**

| Mục | Khoảng |
|---|---|
| `crm.defaultSlaMinutes` | 1 → 1440 |
| `office.from`, `office.to`, `office.satTo` | HH:mm, `from < to`; `satTo` trống = nghỉ thứ 7, nếu có thì > `from` |
| `office.lateGraceMin` | 0 → 120 |
| `invoice.codePattern` | ≤ 40 ký tự, chỉ biến `{CN} {YYYY} {YY} {MM} {SO} {SO:1-9}`, bắt buộc có `{SO}` |
| `invoice.resetEvery` | `never` · `year` · `month` |
| `payroll.lockDay` | 1 → 28 |
| `atRisk.absent`, `atRisk.streak` | 1 → 20 |
| `atRisk.attendanceRate`, `submitRate`, `scorePct` | 0 → 100 |
| `atRisk.mediumFrom`, `highFrom` | 1 → 20, `mediumFrom < highFrom` |
| `parent.absentAlertMin` | 0 → 120 (0 = tắt) |
| `parent.underAge` | 0 → 25 (0 = mọi HV) |
| `parent.weeklyDay` | 0 → 6 · `parent.weeklyTime` HH:mm |
| `safety.refundApprovalMin` | ≥ 0 (số nguyên VND) |
| `safety.trashDays` | 1 → 365 |

**Dữ liệu / API:** `PUT /api/v1/system/settings` `{ settings }` → `{ settings, changed, updatedAt, updatedBy }`

## CD-03 · Module khác dùng cài đặt

> Là **hệ thống**, tôi muốn **mọi module đọc cùng một bộ cài đặt** để **đổi 1 chỗ là áp dụng khắp nơi**.

**Tiêu chí chấp nhận:**
- [ ] `System.Contracts.ICenterSettings.GetAsync()` trả bộ cài đặt có kiểu (cache trong bộ nhớ, xóa cache khi lưu — 1 instance nên không cần Redis).
- [ ] Students tính nguy cơ bỏ học theo `atRisk.*` (thay ngưỡng mặc định đang cố định).
- [ ] Xóa vai trò dùng `safety.trashDays` (thay hằng số 30 trong `DeleteRoleHandler`).
- [ ] Các module sau (Crm SLA, Finance mã hóa đơn / ngưỡng hoàn tiền, Hr giờ hành chính) đọc từ đây khi làm.

---

## TB-01 · Xem chuông thông báo

> Là **người dùng** (nhân viên, GV, HV, PH), tôi muốn **thấy các thông báo mới nhất của mình** để **không bỏ lỡ việc cần xử lý**.

**Nguồn:** `NotificationBell.tsx` · **Quyền cần:** đã đăng nhập · **Ưu tiên:** Cao

**Tiêu chí chấp nhận:**
- [ ] Trả 50 thông báo mới nhất của **chính tài khoản đang đăng nhập** (nhân viên theo `user_accounts`, cổng theo `portal_accounts`) + số chưa đọc.
- [ ] Trường khớp `AppNotification`: `id, title, description, createdAt, read, tone, link?`.
- [ ] Phân trang thêm bằng `before` (createdAt của dòng cuối) khi cần xem cũ hơn.

**Dữ liệu / API:** `GET /api/v1/notifications?before=&limit=` → `{ items, unread }`

## TB-02 · Đánh dấu đã đọc / đọc hết

**Tiêu chí chấp nhận:**
- [ ] `POST /api/v1/notifications/{id}/read` — chỉ thông báo của mình; của người khác → 404 (`notifications.not_found`), không lộ có tồn tại.
- [ ] `POST /api/v1/notifications/read-all` → `{ marked }`.
- [ ] Đọc lại thông báo đã đọc không đổi `readAt`.

## TB-03 · Nhận thông báo realtime

**Tiêu chí chấp nhận:**
- [ ] Ngay khi lưu, đẩy sự kiện SignalR `notification.created` (dữ liệu = 1 `AppNotification`) tới đúng tài khoản qua hub `/hubs/realtime` đang có.
- [ ] Tài khoản không trực tuyến: vẫn thấy ở lần tải chuông sau.

## TB-04 · Module gửi thông báo

> Là **module nghiệp vụ**, tôi muốn **gửi thông báo cho người liên quan** mà không phụ thuộc vào cách lưu / đẩy realtime.

**Các bước:**
1. Module phát integration event `NotificationRequested` (Notifications.Contracts) qua outbox **trong cùng giao dịch** với thao tác nghiệp vụ.
2. Notifications xác định người nhận:
   - danh sách tài khoản cụ thể (`staff` / `portal`), **hoặc**
   - "ai có quyền `X` tại chi nhánh `Y`" → `IStaffAccessDirectory.WithPermissionAsync`.
3. Lưu 1 dòng / người nhận (bỏ trùng), đẩy TB-03.

**Trường hợp đặc biệt:**
| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TB-04.E1 | Không tìm được người nhận | Bỏ qua, ghi log cảnh báo |
| TB-04.E2 | Tiêu đề trống | Bỏ qua dòng đó, ghi log lỗi (lỗi lập trình, không chặn nghiệp vụ) |
| TB-04.E3 | Người thao tác cũng là người nhận theo quyền | Không gửi cho chính họ (`excludeAccountId`) |

**Nối luôn ở giai đoạn này** (đang ghi ⏳ ở story 07):
- Đơn hủy buổi mới / đơn xin nghỉ mới → người có `academic.requests.approve` ở chi nhánh của lớp.
- GV báo vấn đề trong báo cáo buổi → người có `academic.sessions.update` ở chi nhánh.
- HV / PH bấm "Quan tâm" lớp kỳ sau → người có `academic.classes.update` ở chi nhánh.
- Đơn được duyệt / từ chối → GV / HV / PH gửi đơn (ngoài sự kiện realtime đang có).

## TB-05 · Dọn thông báo cũ

- [ ] Job hằng đêm xóa hẳn thông báo **đã đọc** quá 90 ngày, và mọi thông báo quá 180 ngày (bảng này không phải dữ liệu nghiệp vụ, không xóa mềm).

---

## NK-01 · Xem / lọc nhật ký hệ thống

> Là **Admin**, tôi muốn **tra ai đã làm gì, lúc nào** để **kiểm soát thao tác nhạy cảm và điều tra sự cố**.

**Nguồn:** `AuditLogsPage.tsx` · **Quyền cần:** `system.audit.view` · **Ưu tiên:** Cao

**Các bước:**
1. Lọc theo: chi nhánh, loại (`category`), mức độ, người thực hiện, khoảng ngày, từ khóa (hành động, đối tượng, người, IP).
2. Trả trang `{ items, total, page, pageSize }`, mới nhất trước. Trường khớp type `AuditLog` của frontend (`actor` = tên người làm).
3. Kèm thống kê cho thẻ đầu trang (theo bộ lọc chi nhánh): thao tác hôm nay, nghiêm trọng 7 ngày, xuất file 7 ngày, đăng nhập thất bại 7 ngày.

**Trường hợp đặc biệt:**
| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| NK-01.E1 | Quyền phạm vi `branch` | Chỉ thấy dòng thuộc chi nhánh của mình và dòng không gắn chi nhánh do chính mình làm |
| NK-01.E2 | Khoảng ngày > 1 năm | Chặn `system.audit.range_too_long` |
| NK-01.E3 | Không chọn ngày | Mặc định 30 ngày gần nhất |

**Dữ liệu / API:** `GET /api/v1/system/audit-logs?branchId=&category=&severity=&actorId=&from=&to=&q=&page=&pageSize=` ·
`GET /api/v1/system/audit-logs/stats?branchId=` · `GET /api/v1/system/audit-logs/actors` (danh sách người cho ô lọc)

## NK-02 · Xuất nhật ký

- [ ] `GET /api/v1/system/audit-logs/export?…` cùng bộ lọc NK-01, tối đa 10 000 dòng mới nhất → `{ items, total, truncated }`; **frontend ghi file `.xlsx`**
  bằng `lib/spreadsheet.ts` có sẵn (backend không thêm thư viện Excel). Quyền `system.audit.export`.
- [ ] Bản thân việc xuất ghi 1 dòng `export` / `warning` "Xuất nhật ký hệ thống".

---

## TR-01 · Xem thùng rác

> Là **Admin**, tôi muốn **xem dữ liệu vừa bị xóa** để **khôi phục khi xóa nhầm**.

**Nguồn:** `TrashPage.tsx`, `trash.ts` · **Quyền cần:** `system.trash.view` · **Ưu tiên:** TB

**Tiêu chí chấp nhận:**
- [ ] Danh sách mục: `id, kind, label, detail, branchId, deletedAt, deletedBy` (tên), `daysLeft`; lọc chi nhánh, loại.
- [ ] Phạm vi `branch`: chỉ mục của chi nhánh mình (mục không gắn chi nhánh — vd vai trò — chỉ phạm vi `all` thấy).
- [ ] Lead / HV bị ẩn **không** trả ở đây (frontend tự ghép từ danh sách lead / HV như hiện tại).

**Dữ liệu / API:** `GET /api/v1/system/trash?branchId=&kind=`

## TR-02 · Khôi phục

**Các bước:**
1. Admin bấm Khôi phục → System tìm `ITrashRestorer` của `kind` (module sở hữu đăng ký) → module bỏ cờ xóa mềm.
2. Thành công → xóa mục thùng rác, ghi nhật ký `data` / `info` "Khôi phục từ Thùng rác".

**Trường hợp đặc biệt:**
| Mã | Tình huống | Hệ thống xử lý |
|---|---|---|
| TR-02.E1 | Mục đã quá hạn (đang chờ job dọn) | `system.trash.expired` |
| TR-02.E2 | Không có module nào nhận `kind` | `system.trash.not_restorable` |
| TR-02.E3 | Module sở hữu từ chối (vd vai trò trùng tên → tự đổi tên "… (khôi phục)" theo PQ-04.E3) | Trả lỗi / cảnh báo của module đó |
| TR-02.E4 | Bản ghi gốc không còn | Xóa mục, báo `system.trash.source_missing` |

**Dữ liệu / API:** `POST /api/v1/system/trash/{id}/restore` · quyền `system.trash.update`

## TR-03 · Xóa vĩnh viễn

- [ ] `DELETE /api/v1/system/trash/{id}` · quyền `system.trash.delete` → module sở hữu xóa cứng bản ghi, rồi xóa mục; nhật ký `data` / `critical` "Xóa vĩnh viễn".

## TR-04 · Tự xóa hẳn khi quá hạn

- [ ] Job dọn hằng đêm của System (02:00): mục có `purgeAfter` ≤ bây giờ → như TR-03 (người làm = Hệ thống). Lỗi ở 1 mục không chặn các mục khác.
- [ ] `purgeAfter` tính lúc xóa theo `safety.trashDays` lúc đó; đổi cài đặt sau **không** đổi hạn của mục cũ.

---

## Phạm vi giai đoạn 16 (chốt 2026-11-20)

- **Làm:** CD-01 → 03, TB-01 → 05, NK-01 → 02, TR-01 → 04; loại thùng rác đầu tiên là `system.role` (xóa vai trò đã có). Các loại khác
  (mẫu tin nhắn, quy tắc chia lead, bài tập, hóa đơn, điều chỉnh lương, tài liệu GV) đăng ký `ITrashRestorer` khi làm module đó.
- **Chưa làm:** Tích hợp (Zalo OA, SMS, ngân hàng, Zoom) → giai đoạn 22. Gửi Zalo / SMS / email theo `parent.*` → khi có Crm mẫu tin nhắn + tích hợp.
- **Domain (phase 1):** `CenterSettings` (7 nhóm, giá trị mặc định, `Validate()`, phẳng hóa ↔ khóa `nhóm.mục`, so sánh trước → sau);
  `Setting.Change` báo có đổi hay không; `Notification.Create` trả `Result` (tiêu đề bắt buộc, cắt độ dài), `IsRead`;
  `TrashItem.IsExpired` / `DaysLeft`.

## Đã làm — backend (giai đoạn 16 phase 3, 2026-11-20)

Code: `System/Application/{Settings,AuditLogs,Trash}/`, `Notifications/Application/Notifications/`, `Academic/Application/Common/AcademicNotices.cs` ·
Test: `tests/JPCenter.IntegrationTests/System/FoundationFlowTests.cs` (+ kiểm tra chuông trong `Academic/PortalFlowTests.cs`).

| Story | API | Quyền |
|---|---|---|
| CD-01 | `GET /api/v1/system/settings` → `{ settings, updatedAt, updatedBy }` (`settings` đúng hình dạng `CenterSettings` frontend) | cổng nhân viên / GV |
| CD-02 | `PUT /api/v1/system/settings` `{ settings }` → `{ settings, updatedAt, updatedBy, changed }` | `system.settings.update` |
| NK-01 | `GET /api/v1/system/audit-logs?branchId=&category=&severity=&actorId=&from=&to=&q=&page=&pageSize=` · `…/stats?branchId=` · `…/actors` | `system.audit.view` |
| NK-02 | `GET /api/v1/system/audit-logs/export?…` → `{ items, total, truncated }` | `system.audit.export` |
| TR-01 → 03 | `GET /api/v1/system/trash?branchId=&kind=` · `POST /api/v1/system/trash/{id}/restore` → `{ kind, entityId, note }` · `DELETE /api/v1/system/trash/{id}` | `system.trash.view / update / delete` |
| TB-01, 02 | `GET /api/v1/notifications?before=&limit=` → `{ items, unread }` · `POST /api/v1/notifications/{id}/read` · `POST /api/v1/notifications/read-all` → `{ marked }` | đã đăng nhập (mọi cổng) |

**Chốt khi code:**
- Cài đặt: cache trong bộ nhớ (`SettingsCache`, 1 instance), cập nhật ngay khi lưu. Module khác đọc qua `System.Contracts.ICenterSettings`.
  Đã dùng: Students tính nguy cơ bỏ học theo `atRisk.*`; xóa vai trò theo `safety.trashDays`. Nhật ký "Đổi cài đặt chung" ghi trước → sau theo nhãn tiếng Việt.
- Đọc cài đặt hỏng trong DB (sửa tay) → dùng mặc định + ghi log cảnh báo, không lỗi.
- Nhật ký: phạm vi `branch` thấy dòng của chi nhánh được gán + dòng không gắn chi nhánh do chính mình làm; lọc chi nhánh kèm dòng không gắn chi nhánh.
- Thùng rác: module sở hữu đăng ký `System.Contracts.ITrashHandler` (khôi phục / xóa hẳn) và phát `ItemTrashed` khi xóa mềm. Loại đầu tiên: `system.role`
  (trùng tên → "… (khôi phục)"). Khôi phục / xóa hẳn ghi nhật ký `data`. Helper chung `db.Undelete(entity)` (shared-helpers.md).
- Thông báo: module khác phát `Notifications.Contracts.NotificationRequested` (người nhận: tài khoản nhân viên / cổng, nhân viên theo `EmployeeIds`,
  hoặc theo quyền + chi nhánh qua `IStaffAccessDirectory`); lưu rồi đẩy SignalR `notification.created` (dữ liệu = `AppNotification`).
  Đã nối từ Academic: đơn hủy buổi / đơn xin nghỉ mới → người có `academic.requests.approve` của chi nhánh; đơn được duyệt / từ chối → GV / HV / PH gửi đơn;
  báo cáo buổi có vấn đề → `academic.sessions.update`; HV / PH bấm "Quan tâm" → Sale phụ trách (chưa giao → `academic.classes.update`).
- Job: dọn thông báo cũ 03:30 hằng ngày; xóa hẳn mục thùng rác quá hạn trong job dọn hệ thống 02:00.
- ⏳ GV và nhân viên dùng chung tài khoản nhân viên → chuông ở cổng nhân viên và cổng GV là 1 danh sách.

### Frontend (giai đoạn 16 phase 4, 2026-11-20)
- `features/system/settingsApi.ts`: tải Cài đặt chung sau đăng nhập (nhân viên, GV) đổ vào `useSettingsStore`; trang Cài đặt chung lưu qua API,
  lỗi theo mục báo đúng ô trên form; phụ đề "sửa lần cuối … bởi …" lấy từ server.
- `features/system/auditApi.ts` + `AuditLogsPage`: server lọc (chi nhánh, loại, mức độ, người theo Id, khoảng ngày — mặc định 30 ngày),
  trang tải tối đa 1000 dòng mới nhất rồi tìm tại chỗ; thẻ thống kê và ô "Người thực hiện" từ server; Xuất → file .xlsx ghi ở trình duyệt.
- `features/system/trashApi.ts` + `TrashPage`: mục đã xóa từ server (khôi phục / xóa vĩnh viễn theo quyền, tải lại dữ liệu loại đó sau khi khôi phục);
  lead / HV bị ẩn vẫn ghép như cũ, bỏ ẩn HV gọi API.
- `features/notifications/notificationApi.ts`: chuông lấy 50 thông báo + số chưa đọc từ server; đọc 1 / đọc hết gọi server;
  SignalR `notification.created` thêm vào chuông ngay. Bỏ chuông tự tạo ở trình duyệt cho kết quả duyệt đơn (server đã gửi).
- Kiểm tra tay (Playwright): chuông Giáo vụ đọc → lưu DB; sửa SLA → lưu + nhật ký; nhật ký 165 dòng thật + thống kê; khôi phục vai trò từ Thùng rác.
- ⏳ Chuông vẫn có thông báo tạm tại chỗ của phần chưa có backend (Tuyển sinh — SLA lead), mất khi tải lại trang.
