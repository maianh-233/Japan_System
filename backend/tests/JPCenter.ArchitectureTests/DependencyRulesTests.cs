using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace JPCenter.ArchitectureTests;

/// <summary>Kiểm tra tự động quy tắc phụ thuộc trong CLAUDE.md mục 3 — vi phạm là build đỏ.</summary>
public sealed class DependencyRulesTests
{
    private static readonly string[] Modules =
        ["Academic", "Crm", "Finance", "Hr", "Library", "Notifications", "Reporting", "Students", "System", "Work"];

    private static Assembly Load(string name) => Assembly.Load(name);

    private static IEnumerable<string> JpReferences(Assembly a) =>
        a.GetReferencedAssemblies().Select(r => r.Name!).Where(n => n.StartsWith("JPCenter.", StringComparison.Ordinal));

    [Fact]
    public void SharedKernel_khong_phu_thuoc_project_nao() =>
        JpReferences(Load("JPCenter.SharedKernel")).ShouldBeEmpty();

    [Fact]
    public void ApplicationCommon_chi_phu_thuoc_SharedKernel() =>
        JpReferences(Load("JPCenter.Application.Common")).ShouldBeSubsetOf(["JPCenter.SharedKernel"]);

    [Fact]
    public void ApiCommon_khong_phu_thuoc_Infrastructure() =>
        JpReferences(Load("JPCenter.Api.Common")).ShouldBeSubsetOf(["JPCenter.Application.Common", "JPCenter.SharedKernel"]);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contracts_chi_phu_thuoc_SharedKernel(string module) =>
        JpReferences(Load($"JPCenter.Modules.{module}.Contracts")).ShouldBeSubsetOf(["JPCenter.SharedKernel"]);

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_chi_tham_chieu_Contracts_cua_module_khac(string module)
    {
        var forbidden = JpReferences(Load($"JPCenter.Modules.{module}"))
            .Where(n => n.StartsWith("JPCenter.Modules.", StringComparison.Ordinal))
            .Where(n => !n.EndsWith(".Contracts", StringComparison.Ordinal))
            .ToList();
        forbidden.ShouldBeEmpty($"Module {module} tham chiếu bên trong module khác: {string.Join(", ", forbidden)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_khong_biet_EF_HTTP_JSON(string module)
    {
        var result = Types.InAssembly(Load($"JPCenter.Modules.{module}"))
            .That().ResideInNamespace($"JPCenter.Modules.{module}.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "System.Text.Json", "FluentValidation",
                $"JPCenter.Modules.{module}.Application", $"JPCenter.Modules.{module}.Infrastructure", $"JPCenter.Modules.{module}.Api")
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_khong_phu_thuoc_Infrastructure_va_Api(string module)
    {
        var result = Types.InAssembly(Load($"JPCenter.Modules.{module}"))
            .That().ResideInNamespace($"JPCenter.Modules.{module}.Application")
            .ShouldNot().HaveDependencyOnAny($"JPCenter.Modules.{module}.Infrastructure", $"JPCenter.Modules.{module}.Api", "Microsoft.AspNetCore")
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    public static TheoryData<string> ModuleNames() => new(Modules);
}
