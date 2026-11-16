# User story — JP Center ERP

Mỗi file = 1 nhóm nghiệp vụ. Viết từ giao diện frontend đã dựng + quy tắc backend ([../nghiep-vu.md](../nghiep-vu.md),
[../database.md](../database.md)). Dùng làm đặc tả để viết API, test tích hợp và kiểm thử tay.

## Danh sách

| # | File | Mã | Trạng thái |
|---|---|---|---|
| 01 | [Đăng nhập & bảo mật tài khoản](01-dang-nhap-bao-mat.md) | `AUTH-xx` | ✍️ Đã viết |
| 02 | [Vai trò & phân quyền](02-phan-quyen.md) | `PQ-xx` | ✍️ Đã viết |
| 03 | Tuyển sinh (lead, chia lead, test xếp lớp) | `CRM-xx` | ⏳ Chưa viết |
| 04 | Học viên & ghi danh | `HV-xx` | ⏳ |
| 05 | Lớp học & buổi học | `DT-xx` | ⏳ |
| 06 | Học phí & hóa đơn | `TC-xx` | ⏳ |

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
