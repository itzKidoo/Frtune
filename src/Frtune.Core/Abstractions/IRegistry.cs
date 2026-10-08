namespace Frtune.Core.Abstractions;

/// <summary>Registry access. The Windows build uses the 64-bit registry view; tests use an in-memory fake.</summary>
public interface IRegistry
{
    /// <summary>Returns null when the key or value does not exist. Name "" is the key's default value.</summary>
    RegValue? GetValue(RegHive hive, string keyPath, string name);

    /// <summary>Writes a value, creating the key (and any missing parents) if needed.</summary>
    void SetValue(RegHive hive, string keyPath, string name, RegValue value);

    /// <summary>Deletes a value. Does nothing if it is already absent.</summary>
    void DeleteValue(RegHive hive, string keyPath, string name);

    bool KeyExists(RegHive hive, string keyPath);

    /// <summary>Deletes a key and everything under it. Does nothing if it is absent.</summary>
    void DeleteKeyTree(RegHive hive, string keyPath);

    /// <summary>True when the key and all of its subkeys hold no values.</summary>
    bool IsTreeEmpty(RegHive hive, string keyPath);

    IReadOnlyList<string> GetSubKeyNames(RegHive hive, string keyPath);
}
