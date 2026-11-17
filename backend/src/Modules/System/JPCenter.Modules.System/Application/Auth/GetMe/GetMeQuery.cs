using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Auth.Common;

namespace JPCenter.Modules.System.Application.Auth.GetMe;

/// <summary>Thông tin người đang đăng nhập (frontend gọi khi mở lại trang).</summary>
public sealed record GetMeQuery : IQuery<SessionUserDto>;
