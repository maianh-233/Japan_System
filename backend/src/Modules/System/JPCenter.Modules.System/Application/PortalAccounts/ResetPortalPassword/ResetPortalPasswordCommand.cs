using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.PortalAccounts.Common;

namespace JPCenter.Modules.System.Application.PortalAccounts.ResetPortalPassword;

/// <summary>AUTH-10.E1 / E3 — giáo vụ đặt lại mật khẩu tạm cho HV / PH (HV nhỏ không có email, quên mật khẩu...).</summary>
public sealed record ResetPortalPasswordCommand(Guid AccountId) : ICommand<PortalCredentialDto>;
