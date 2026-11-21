# User story — JP Center ERP

Mỗi file = 1 nhóm nghiệp vụ. Viết từ giao diện frontend đã dựng + quy tắc backend ([../nghiep-vu.md](../nghiep-vu.md),
[../database.md](../database.md)). Dùng làm đặc tả để viết API, test tích hợp và kiểm thử tay.

## Danh sách

| # | File | Mã | Trạng thái |
|---|---|---|---|
| 01 | [Đăng nhập & bảo mật tài khoản](01-dang-nhap-bao-mat.md) | `AUTH-xx` | ✍️ Đã viết |
| 02 | [Vai trò & phân quyền](02-phan-quyen.md) | `PQ-xx` | ✍️ Đã viết |
| 03 | [Quản lý chi nhánh](03-chi-nhanh.md) | `CN-xx` | ✍️ Đã viết |
| 04 | [Quản lý nhân viên & giáo viên](04-nhan-su.md) | `NS-xx` | ✍️ Đã viết |
| 05 | [Tuyển sinh (lead, chia lead, test xếp lớp, lịch hẹn, chiến dịch)](05-tuyen-sinh.md) | `CRM-xx` | ✅ Giai đoạn 18 |
| 06 | [Học viên & ghi danh (phụ huynh, cảnh báo bỏ học, chứng nhận, JLPT, du học)](06-hoc-vien.md) | `HV-xx` | ✅ HV-01 → HV-11 (giai đoạn 14) · ⏳ HV-12 → HV-17 |
| 07 | [Lớp học & buổi học (điểm danh, dạy thay, đơn từ, lên lớp)](07-lop-hoc.md) | `DT-xx` | 🚧 Backend đợt 1 (phòng, lớp, buổi) |
| 08 | [Học phí, hóa đơn & tài chính (đối soát, công nợ, lương, hoa hồng)](08-tai-chinh.md) | `TC-xx` | ✍️ Đã viết |
| 09 | [Khóa học & lộ trình (phiên bản, tài liệu giảng dạy, mốc thi)](09-khoa-hoc-lo-trinh.md) | `KH-xx`, `LT-xx` | ✍️ Đã viết |
| 10 | [Nền chung: cài đặt chung, thông báo, nhật ký hệ thống, thùng rác](10-he-thong-chung.md) | `CD-xx`, `TB-xx`, `NK-xx`, `TR-xx` | ✅ Giai đoạn 16 |
| 11 | [Ngân hàng câu hỏi (soạn, duyệt, file nghe, nhập Excel, bốc câu cho đề)](11-ngan-hang-cau-hoi.md) | `NH-xx` | ✅ Giai đoạn 17 |

**Chưa có nhóm story riêng** (trang frontend đã dựng — viết khi bắt đầu làm module): Bài tập, bài thi,
thông báo lớp, đánh giá GV, flashcard · Phòng học & báo hỏng · Thư viện tài liệu · Tuyển dụng & Onboarding · Chấm công, đơn từ & phê duyệt ·
Công việc, xếp ca & bàn giao ca · Hệ thống (tích hợp) · Báo cáo & bảng điều khiển. Thứ tự làm: [../ke-hoach.md](../ke-hoach.md).

## Mẫu 1 user story

```
## <MÃ> · <Tên ngắn>

> Là **<vai trò>**, tôi muốn **<việc cần làm>** để **<lợi ích>**.

**Nguồn:** trang / file frontend · **Quyền cần:** mã quyền · **Ưu tiên:** Cao / TB / Thấp

**Điều kiện trước:** ...

**Các bước (luồng chính):**
1. Người dùng ...
2. Hệ thống ...

**Trường hợp đặc biệt:**
| Mã | Tình huống | Hệ thống xử lý |

**Tiêu chí chấp nhận:** - [ ] ...

**Dữ liệu / API:** bảng, endpoint, mã lỗi
```

## Quy ước

- **Mã story**: `<NHÓM>-<số 2 chữ số>`, không đánh lại số khi xóa. Trường hợp đặc biệt: `<MÃ>.E<số>` (vd `AUTH-01.E3`) —
  dùng làm tên test case: `AUTH-01.E3 Đăng nhập tài khoản bị khóa`.
- **Mã lỗi** khớp `Error.Code` trong Domain / ProblemDetails (vd `auth.invalid_credentials`).
- Ghi **🆕 Đề xuất backend** cho hành vi frontend mock chưa làm nhưng backend thật cần có (khóa khi nhập sai nhiều lần, phiên đăng nhập...).
- Đổi giao diện / nghiệp vụ → sửa story trước, rồi mới sửa code.
- ⏳ = phụ thuộc module chưa làm: viết sẵn quy tắc, làm khi có module đó.
