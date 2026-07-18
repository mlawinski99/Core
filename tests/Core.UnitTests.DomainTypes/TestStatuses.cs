using Core.DomainTypes;

namespace Core.UnitTests.DomainTypes;

public class TestStatus(int id, string name) : Enumeration(id, name)
{
    public static readonly TestStatus Pending = new(1, nameof(Pending));
    public static readonly TestStatus Shipped = new(2, nameof(Shipped));
}

public class OtherTestStatus(int id, string name) : Enumeration(id, name)
{
    public static readonly OtherTestStatus Pending = new(1, nameof(Pending));
}
