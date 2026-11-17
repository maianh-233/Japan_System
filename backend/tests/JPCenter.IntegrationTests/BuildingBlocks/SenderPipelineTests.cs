using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.BuildingBlocks;

/// <summary>ISender tự viết: tìm đúng handler, validate trước khi chạy, trả lỗi theo trường.</summary>
public sealed class SenderPipelineTests
{
    public sealed record Greet(string Name) : ICommand<string>;

    public sealed class GreetHandler : ICommandHandler<Greet, string>
    {
        public Task<Result<string>> Handle(Greet request, CancellationToken ct) =>
            Task.FromResult<Result<string>>($"Xin chào {request.Name}");
    }

    public sealed class GreetValidator : AbstractValidator<Greet>
    {
        public GreetValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Chưa nhập tên");
    }

    private static ISender BuildSender()
    {
        var services = new ServiceCollection().AddLogging().AddMessaging();
        services.AddHandlersFromAssembly(typeof(SenderPipelineTests).Assembly);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Gui_dung_handler_va_tra_gia_tri()
    {
        var result = await BuildSender().Send(new Greet("Lan"), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Xin chào Lan");
    }

    [Fact]
    public async Task Du_lieu_sai_thi_khong_chay_handler_va_tra_loi_theo_truong()
    {
        var result = await BuildSender().Send(new Greet(""), TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation.failed");
        result.Error.FieldErrors!["name"].ShouldBe(["Chưa nhập tên"]);
    }
}
