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
        var enumerationTypes = Directory.GetFiles(AppContext.BaseDirectory, "Core.*.dll")
            .Select(Assembly.LoadFrom)
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
