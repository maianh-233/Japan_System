# JP Center ERP — Frontend

React + TypeScript + Vite · Ant Design · Tailwind CSS v4 · lucide-react / @ant-design/icons ·
TanStack Query · Zustand · React Router · MSW (mock API khi chưa có backend)

## Chạy cùng backend thật (mặc định)

```bash
# 1. Backend: Docker + API (xem backend/README.md)
cd backend && docker compose -f deploy/docker-compose.yml --env-file .env up -d && dotnet run --project src/Host/JPCenter.Api
# 2. Frontend
cd frontend && npm install && npm run dev   # http://localhost:5173 — /api được chuyển tới http://localhost:5080
```

Đăng nhập bằng tài khoản mẫu (vd `superadmin@jpcenter.vn`), mật khẩu `Jpcenter@123` (= `Seed__DefaultPassword` của backend).
Email (OTP quên mật khẩu, link mời kích hoạt `/activate?token=`) xem ở Mailpit: http://localhost:8025.

| Phần | Nguồn dữ liệu |
|---|---|
| Đăng nhập / đăng xuất / làm mới phiên, quên & đổi mật khẩu, kích hoạt tài khoản, thiết bị & lịch sử đăng nhập | **API thật** |
| Vai trò & quyền, Phân quyền người dùng, Duyệt cấp quyền tài chính | **API thật** |
| Các trang khác (CRM, Học viên, Đào tạo, Tài chính, Nhân sự...) | Dữ liệu giả — chờ backend từng module |

- Access token chỉ nằm trong bộ nhớ; refresh token là cookie HttpOnly. Tải lại trang → tự xin phiên mới (`src/features/auth/session.ts`).
- Id backend (GUID) ↔ Id mock (`cg`, `e-huy`, `R-SA`): `src/lib/api/ids.ts`, bảng sinh bởi `npm run export-seed`.
- Chạy không cần backend: tạo `.env.local` với `VITE_API_MODE=mock` (xem `.env.example`).

## Cấu trúc thư mục

```
public/                      mockServiceWorker.js (MSW tự sinh, không sửa tay)
src/
├─ app/
│  ├─ providers/             QueryClient, ConfigProvider (theme AntD), ...
│  ├─ router/                Khai báo route cho 3 cổng: staff / teacher / student
│  └─ guards/                RequireAuth, RequirePermission, RequirePortal
├─ assets/                   images, fonts, styles (Tailwind entry)
├─ config/                   Danh mục permission (module → group → permission), menu từng cổng, hằng số
├─ types/                    Kiểu dữ liệu dùng chung (entity, API response)
├─ lib/                      api client, tính quyền hiệu lực, format tiền/ngày
├─ stores/                   Zustand: phiên đăng nhập, chi nhánh đang chọn, cổng đang dùng
├─ hooks/                    useCan, useMe, useBranch, ...
├─ components/
│  ├─ common/                Bảng, form, tag trạng thái, page header dùng lại
│  ├─ layout/                Sidebar, header, chọn chi nhánh, chuyển cổng, chuông thông báo
│  ├─ permission/            <Can />, ma trận phân quyền
│  ├─ timetable/             Thời khóa biểu tuần/tháng
│  └─ check-in/              Nút Check-in / Check-out trên header
├─ layouts/                  Layout chung cho từng cổng
├─ mocks/
│  ├─ handlers/              Mock API theo từng module (cùng contract với backend sau này)
│  └─ data/                  Dữ liệu mẫu (seed)
├─ features/                 Logic theo nghiệp vụ, dùng chung giữa các cổng
│  │                         (mỗi feature: api hooks, components, types riêng)
│  ├─ auth/  permission/  branch/  notification/
│  ├─ crm/                   Lead, tư vấn, chiến dịch
│  ├─ student/               Hồ sơ học viên, liên hệ phụ huynh/giám hộ
│  ├─ academic/              Khóa học, lộ trình, lớp, buổi học, phòng, điểm danh
│  ├─ content/               Tài liệu, ngân hàng câu hỏi, đề thi, bài tập
│  ├─ finance/               Bảng giá, ghi danh, hóa đơn, thanh toán, công nợ, lương GV
│  ├─ hr/                    Nhân viên, giáo viên, chấm công
│  └─ workflow/              Đơn từ & phê duyệt (nghỉ phép, hủy buổi, xin nghỉ học, hoàn tiền)
└─ pages/                    Màn hình — chỉ ghép component từ features/
   ├─ auth/                  login, select-portal
   ├─ errors/                403, 404
   ├─ staff/                 Cổng nhân viên (Super Admin, Admin, QL chi nhánh, Giáo vụ, Sale, Marketing, Kế toán)
   │  ├─ dashboard/  reports/
   │  ├─ crm/                leads, consultations, campaigns
   │  ├─ students/           students, contacts
   │  ├─ academic/           courses, learning-paths, classes, schedule, rooms, attendance
   │  ├─ content/            documents, question-bank, exams
   │  ├─ finance/            price-list, enrollments, invoices, payments, debts, payroll
   │  ├─ hr/                 employees, teachers, timesheets
   │  ├─ workflow/           requests
   │  └─ system/             branches, roles, user-permissions, audit-logs
   ├─ teacher/               dashboard, timetable, classes, session-log, assignments,
   │                         documents, requests (hủy buổi), timesheet
   └─ student/               dashboard, timetable, assignments, exams, documents,
                             tuition, requests (xin nghỉ), progress
```

## Quy ước

- `pages/` không gọi API trực tiếp — dùng hooks trong `features/`.
- Ẩn/hiện menu, nút bấm theo **permission** (`useCan('crm.lead.update')`), không theo tên role.
- Chuyển trang sau đăng nhập theo **cổng** (staff / teacher / student), không theo role.
- Giáo viên chỉ được **gửi đơn hủy buổi**; **hủy cả lớp** chỉ nhân viên trung tâm có quyền `academic.class.cancel`.
- Tailwind dùng cho layout/spacing; component nghiệp vụ dùng AntD. Khi cấu hình cần đặt Tailwind
  trong CSS layer để preflight không ghi đè style của AntD.
- Thư mục rỗng giữ bằng `.gitkeep` — xóa khi đã có file thật.
