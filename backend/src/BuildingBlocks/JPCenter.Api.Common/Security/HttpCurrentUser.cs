using JPCenter.Application.Common.Security;
using Microsoft.AspNetCore.Http;

namespace JPCenter.Api.Common.Security;

/// <summary>Người đang gọi — đọc từ ngữ cảnh do <see cref="CurrentUserMiddleware"/> nạp.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    internal const string AccessKey = "jp.access";
    internal const string SessionKey = "jp.session";

    public UserAccess? Access => accessor.HttpContext?.Items[AccessKey] as UserAccess;

    public bool IsAuthenticated => Access is not null;

    public Guid? AccountId => Access?.AccountId;

    public Guid? SessionId => accessor.HttpContext?.Items[SessionKey] as Guid?;
}
