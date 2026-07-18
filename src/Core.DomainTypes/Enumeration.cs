using System.Reflection;

namespace Core.DomainTypes;

public abstract class Enumeration(int id, string name) : IComparable
{
    public string Name { get; } = name;

    public int Id { get; } = id;

    public override string ToString() => Name;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<T>();

    public override bool Equals(object? obj) =>
        obj is Enumeration otherValue
        && GetType() == otherValue.GetType()
        && Id == otherValue.Id;

    public static T GetByName<T>(string name) where T : Enumeration =>
        GetAll<T>().FirstOrDefault(e => e.Name == name)
        ?? throw new InvalidOperationException($"'{name}' does not exist in {typeof(T)}");

    public int CompareTo(object? other) => other switch
    {
        null => 1,
        Enumeration enumeration => GetType() == enumeration.GetType()
            ? Id.CompareTo(enumeration.Id)
            : string.CompareOrdinal(GetType().FullName, enumeration.GetType().FullName),
        _ => throw new ArgumentException($"Object must be of type {nameof(Enumeration)}", nameof(other)),
    };

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
