namespace CrossServer.Shared.Interfaces;

public interface IXLogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}