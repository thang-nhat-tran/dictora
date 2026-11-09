using System.Reflection;

namespace Dictora.ArchitectureTests;

/// <summary>
/// Enforces the modular monolith rules from docs/decisions/ADR-001-modular-monolith.md.
/// Modules are discovered from the build output, so a new module is covered automatically
/// once this project references it.
/// </summary>
public class ModuleBoundaryTests
{
    private const string ModulePrefix = "Dictora.Modules.";
    private const string ContractsSuffix = ".Contracts";

    private static readonly string[] AllModuleAssemblies = Directory
        .GetFiles(AppContext.BaseDirectory, ModulePrefix + "*.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .OfType<string>()
        .Order()
        .ToArray();

    private static readonly string[] Implementations =
        AllModuleAssemblies.Where(n => !n.EndsWith(ContractsSuffix)).ToArray();

    private static readonly string[] Contracts =
        AllModuleAssemblies.Where(n => n.EndsWith(ContractsSuffix)).ToArray();

    public static TheoryData<string> ImplementationNames => new(Implementations);
    public static TheoryData<string> ContractNames => new(Contracts);

    [Fact]
    public void All_modules_are_discovered()
    {
        Assert.Equal(
            ["Dictora.Modules.Identity", "Dictora.Modules.Lessons", "Dictora.Modules.Practice"],
            Implementations);
        Assert.All(Implementations, m => Assert.Contains(m + ContractsSuffix, Contracts));
    }

    [Theory]
    [MemberData(nameof(ImplementationNames))]
    public void Module_does_not_depend_on_another_modules_implementation(string module)
    {
        var forbidden = ReferencedModuleAssemblies(module)
            .Where(r => !r.EndsWith(ContractsSuffix) && r != module);

        Assert.Empty(forbidden);
    }

    [Theory]
    [MemberData(nameof(ContractNames))]
    public void Contracts_do_not_depend_on_any_module_implementation(string contracts)
    {
        var forbidden = ReferencedModuleAssemblies(contracts)
            .Where(r => !r.EndsWith(ContractsSuffix));

        Assert.Empty(forbidden);
    }

    // Only assemblies whose types are actually used end up in the compiled metadata,
    // so this catches real dependencies, not just project references.
    private static IEnumerable<string> ReferencedModuleAssemblies(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith(ModulePrefix));
}
