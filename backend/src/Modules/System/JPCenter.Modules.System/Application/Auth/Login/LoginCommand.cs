using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Auth.Common;

namespace JPCenter.Modules.System.Application.Auth.Login;

/// <summary>AUTH-01 — đăng nhập bằng email (nhân viên) hoặc tên đăng nhập / email (cổng HV / PH).</summary>
public sealed record LoginCommand(string Identifier, string Password, bool Remember) : ICommand<SignInResult>;
