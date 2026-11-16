# ADR 0001 — Công nghệ & kiến trúc backend

- Ngày: 2026-11-17
- Trạng thái: **Chấp nhận**

## Bối cảnh

Frontend (React) đã dựng gần đủ giao diện bằng dữ liệu giả. Cần backend cho ERP trung tâm tiếng Nhật nhiều chi nhánh:
~10 nhóm nghiệp vụ, phân quyền theo module + phạm vi (own/branch/all), 4 cổng (nhân viên, GV, HV, PH), đội phát triển nhỏ.
Chủ dự án muốn thử công nghệ ngoài Spring.

## Quyết định

1. **.NET 10 (LTS, hỗ trợ đến 11/2028) + ASP.NET Core Minimal API.** Hiệu năng tốt, công cụ mạnh, kiểu tĩnh tương tự Java/TS.
   (.NET 8 trên máy hết hỗ trợ 11/2026 → không dùng.)
2. **Modular Monolith** — 1 ứng dụng, 1 database, mỗi module 1 schema; giao tiếp qua Contracts + integration event (Outbox).
   Đơn giản để vận hành, ranh giới rõ để sau này tách service nếu cần.
3. **PostgreSQL + EF Core.** Miễn phí, mạnh về JSON / full-text / sequence; EF Core migrations theo module.
4. **Tự viết** lớp Command/Query + pipeline thay **MediatR**, mapping viết tay thay **AutoMapper** — hai thư viện này
   chuyển sang license thương mại (2025); phần cần dùng chỉ vài chục dòng.
5. **FluentValidation, Serilog, Quartz.NET, SignalR, Testcontainers, NetArchTest** — đều mã nguồn mở, license MIT/Apache.

## Phương án đã cân nhắc

| Phương án | Lý do không chọn |
|---|---|
| Spring Boot (Java) | Chủ dự án muốn thử công nghệ khác |
| NestJS (Node) | Cùng ngôn ngữ với frontend, nhưng nghiệp vụ tính tiền / lịch phức tạp hợp kiểu tĩnh mạnh của C# hơn |
| Clean Architecture 4 project / module | 10 module × 4 = 40 project, quá nặng; giữ các tầng bằng thư mục + test kiến trúc |
| Microservices | Quá sớm cho đội nhỏ, vận hành phức tạp |

## Hệ quả

- Cần cài .NET SDK 10.
- Quy tắc phụ thuộc giữa tầng nằm trong cùng 1 project / module → bắt buộc có `ArchitectureTests` để giữ kỷ luật.
