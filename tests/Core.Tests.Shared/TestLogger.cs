using Core.Logger;

namespace Core.Tests.Shared;

public class TestLogger<T> : IAppLogger<T>
{
    public void LogInformation(string message, params object[] args) { }
    public void LogWarning(string message, params object[] args) { }
    public void LogDebug(string message, params object[] args) { }
    public void LogError(string message, params object[] args) { }
    public void LogError(Exception exception, string message, params object[] args) { }
}