namespace CrossServer.Shared.Interfaces;

public interface IXLogger
{
    void LogTrace(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}