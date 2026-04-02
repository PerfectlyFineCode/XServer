using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CrossServer.Shared;

public class ClientContextDataBag
{
    private Dictionary<string, object?> Metadata { get; } = new();

    public object? this[string key]
    {
        get => Metadata.GetValueOrDefault(key);
        set => Metadata[key] = value;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (Metadata.TryGetValue(key, out object? obj) && obj is T t)
        {
            value = t;
            return true;
        }
        
        value = default;
        return false;
    }
    
    public void Set<T>(string key, T? value)
    {
        Metadata[key] = value;
    }
    
    public void Unset(string key)
    {
        Metadata.Remove(key);
    }

    public bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? value)
    {
        if (Metadata.TryGetValue(key, out object? obj) && obj is T t)
        {
            value = t;
            return true;
        }
        
        value = default;
        return false;
    }
}