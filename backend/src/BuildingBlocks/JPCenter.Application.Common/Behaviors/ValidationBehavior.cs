using FluentValidation;
using FluentValidation.Results;
using JPCenter.Application.Common.Messaging;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Behaviors;

/// <summary>Chạy mọi validator của request; sai → trả lỗi <c>validation.failed</c> kèm lỗi từng trường, KHÔNG gọi handler.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        var list = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (list.Length == 0) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();
        foreach (var validator in list)
            failures.AddRange((await validator.ValidateAsync(context, ct)).Errors);

        if (failures.Count == 0) return await next();

        var fieldErrors = failures
            .GroupBy(f => JsonName(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());
        return ResultFactory.Failure<TResponse>(Error.InvalidInput(fieldErrors));
    }

    /// <summary>"NewPassword" → "newPassword", "Items[0].Name" → "items[0].name" — khớp tên trường JSON frontend.</summary>
    private static string JsonName(string property) =>
        string.Join('.', property.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}
