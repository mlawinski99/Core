using System.Reflection;
using Core.DomainTypes;
using FluentAssertions;
using Xunit;

namespace Core.UnitTests.StartupValidation;

public class StartupValidationUnitTests
{
    [Fact]
    public void Enumerations_ShouldNotDeclareDuplicateIdsOrNames()
    {
        // Arrange
        var assemblyFiles = Directory.GetFiles(AppContext.BaseDirectory, "Core.*.dll");
        var assemblies = assemblyFiles.Select(Assembly.LoadFrom).ToList();

        // Assert assembly discovery completeness (fail closed if discovery is broken)
        // Find repository root by walking up from test binary location
        var searchPath = new DirectoryInfo(AppContext.BaseDirectory);
        while (searchPath != null && !Directory.Exists(Path.Combine(searchPath.FullName, "src")))
        {
            searchPath = searchPath.Parent;
        }

        if (searchPath != null)
        {
            var expectedProjectCount = Directory.GetFiles(
                Path.Combine(searchPath.FullName, "src"),
                "*.csproj",
                SearchOption.AllDirectories).Length;

            var discoveredCoreAssemblies = assemblies
                .Where(a => a.GetName().Name?.StartsWith("Core.") == true)
                .ToList();

            discoveredCoreAssemblies.Should().HaveCountGreaterThanOrEqualTo(expectedProjectCount,
                "assembly discovery must find all Core.* production assemblies");
        }

        var enumerationTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsAssignableTo(typeof(Enumeration)) && !t.IsAbstract);

        foreach (var enumerationType in enumerationTypes)
        {
            // Act
            var members = enumerationType
                .GetFields(BindingFlags.Public |
                           BindingFlags.Static |
                           BindingFlags.DeclaredOnly)
                .Select(f => f.GetValue(null))
                .OfType<Enumeration>()
                .ToList();

            // Assert
            members.Should().OnlyHaveUniqueItems(m => m.Id, $"{enumerationType} must not declare duplicate ids");
            members.Should().OnlyHaveUniqueItems(m => m.Name, $"{enumerationType} must not declare duplicate names");
        }
    }
}
