# Khuôn một module

Mọi module trong `src/Modules/` theo đúng khuôn dưới đây. Ví dụ dùng module **Crm**.

```
src/Modules/Crm/
├─ JPCenter.Modules.Crm/
│  ├─ CrmModule.cs                         Đăng ký DI + endpoint của module (Host gọi)
│  ├─ Domain/
│  │  ├─ Common/                           (tùy chọn) helper domain CHỈ module này dùng
│  │  └─ <Aggregate>/                      1 thư mục / aggregate — vd Leads/
│  │     ├─ Lead.cs                        Aggregate root
│  │     ├─ LeadId.cs                      Id strongly-typed
│  │     ├─ Consultation.cs                Entity con
│  │     ├─ LeadStatus.cs                  Enum
│  │     ├─ LeadErrors.cs                  Danh mục lỗi (Error code + thông điệp tiếng Việt)
│  │     └─ Events/LeadConverted.cs        Domain event
│  ├─ Application/
│  │  ├─ Common/                           (tùy chọn) helper application CHỈ module này dùng
│  │  ├─ Abstractions/                     Interface module cần, Infrastructure hiện thực (vd ICrmDbContext)
│  │  └─ <Feature>/                        Nhóm theo aggregate / màn hình — vd Leads/
│  │     ├─ CreateLead/
│  │     │  ├─ CreateLeadCommand.cs
│  │     │  ├─ CreateLeadHandler.cs
│  │     │  └─ CreateLeadValidator.cs
│  │     ├─ GetLeads/
│  │     │  ├─ GetLeadsQuery.cs
│  │     │  ├─ GetLeadsHandler.cs
│  │     │  └─ LeadListItemDto.cs
│  │     └─ EventHandlers/                 Nghe domain event (cùng module) / integration event (module khác)
│  ├─ Infrastructure/
│  │  ├─ Persistence/
│  │  │  ├─ CrmDbContext.cs                : ModuleDbContextBase, schema "crm"
│  │  │  ├─ Configurations/LeadConfiguration.cs
│  │  │  └─ Migrations/
│  │  └─ Services/                         (tùy chọn) hiện thực interface trong Application/Abstractions
│  └─ Api/
│     ├─ CrmEndpoints.cs                   MapGroup("/api/v1/crm") + gọi từng file endpoint
│     └─ Endpoints/LeadEndpoints.cs        Endpoint mỏng: request → command/query → response
└─ JPCenter.Modules.Crm.Contracts/
   ├─ ICrmLookup.cs                        Truy vấn đồng bộ module khác được gọi (chỉ trả DTO)
   ├─ Dtos/                                DTO dùng trong lookup / event
   └─ IntegrationEvents/LeadConvertedToStudent.cs
```

Thư mục đánh dấu **(tùy chọn)** chỉ tạo khi cần. Ngoài các thư mục trên **không tạo thêm**.

## Một use case — luồng chuẩn

```
HTTP  →  Api/Endpoints/LeadEndpoints.cs
          .RequirePermission("crm.leads", PermissionAction.Create)
          → ISender.Send(new CreateLeadCommand(...))
      →  Pipeline chung (Application.Common): Log → Validate (FluentValidation) → Transaction
      →  Application/Leads/CreateLead/CreateLeadHandler.cs
          → Lead.Create(...)              (Domain kiểm tra bất biến, trả Result)
          → db.Leads.Add(lead); SaveChanges   (interceptor: audit, outbox domain event)
      ←  Result<LeadId>  →  201 / ProblemDetails
```

## Checklist thiết kế entity (bước tiếp theo)

Với mỗi aggregate ghi rõ:

1. **Thuộc module nào**, aggregate root là gì, entity con nào nằm trong ranh giới (lưu / xóa cùng nhau).
2. **Id** + mã hiển thị (nếu có) + quy tắc sinh mã.
3. **Thuộc tính** — kiểu (Money, DateOnly, TimeOnly, enum...), bắt buộc / tùy chọn, có `BranchId` không.
4. **Trạng thái & chuyển trạng thái** hợp lệ (sơ đồ), ai được chuyển (quyền).
5. **Bất biến** (vd: sĩ số ≤ tối đa, buổi đã điểm danh không xóa được).
6. **Tham chiếu sang module khác** — chỉ lưu Id (`StudentId`, `EmployeeId`), không navigation property xuyên module.
7. **Domain event** phát ra + module nào nghe.
8. **Đối chiếu type frontend** tương ứng (`frontend/src/features/**/types.ts`) — chênh lệch thì ghi chú.
