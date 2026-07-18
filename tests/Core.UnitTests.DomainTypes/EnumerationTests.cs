using Core.DomainTypes;
using FluentAssertions;
using Xunit;

namespace Core.UnitTests.DomainTypes;

public class EnumerationTests
{
    [Fact]
    public void Equals_SameTypeAndId_ShouldBeTrue()
    {
        // Arrange
        var left = new TestStatus(1, "Pending");
        var right = new TestStatus(1, "Renamed");

        // Act
        var result = left.Equals(right);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_SameTypeDifferentId_ShouldBeFalse()
    {
        // Act
        var result = TestStatus.Pending.Equals(TestStatus.Shipped);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentTypeSameId_ShouldBeFalse()
    {
        // Act
        var result = TestStatus.Pending.Equals(OtherTestStatus.Pending);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        // Act
        var result = TestStatus.Pending.Equals(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_EqualInstances_ShouldMatch()
    {
        // Arrange
        var left = new TestStatus(1, "Pending");
        var right = new TestStatus(1, "Pending");

        // Act & Assert
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentTypeSameId_ShouldNotMatch()
    {
        // Act & Assert
        TestStatus.Pending.GetHashCode().Should().NotBe(OtherTestStatus.Pending.GetHashCode());
    }

    [Fact]
    public void CompareTo_Null_ShouldBePositive()
    {
        // Act
        var result = TestStatus.Pending.CompareTo(null);

        // Assert
        result.Should().BePositive();
    }

    [Fact]
    public void CompareTo_NonEnumeration_ShouldThrowArgumentException()
    {
        // Act
        var act = () => TestStatus.Pending.CompareTo("not an enumeration");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CompareTo_SameId_ShouldBeZero()
    {
        // Act
        var result = TestStatus.Pending.CompareTo(new TestStatus(1, "Pending"));

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CompareTo_DifferentTypeSameId_ShouldNotBeZero()
    {
        // Act
        var result = TestStatus.Pending.CompareTo(OtherTestStatus.Pending);
        var reversed = OtherTestStatus.Pending.CompareTo(TestStatus.Pending);

        // Assert
        result.Should().NotBe(0);
        reversed.Should().NotBe(0);
    }

    [Fact]
    public void GetAll_ShouldReturnAllDeclaredMembers()
    {
        // Act
        var statuses = Enumeration.GetAll<TestStatus>();

        // Assert
        statuses.Should().BeEquivalentTo([TestStatus.Pending, TestStatus.Shipped]);
    }

    [Fact]
    public void GetByName_ExistingName_ShouldReturnMember()
    {
        // Act
        var status = Enumeration.GetByName<TestStatus>("Pending");

        // Assert
        status.Should().Be(TestStatus.Pending);
    }
}
