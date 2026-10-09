using Frtune.Core.Abstractions;
using Microsoft.Win32;

namespace Frtune.Windows.Platform;

/// <summary>
/// Real registry, always the 64-bit view so 32/64-bit redirection never hides a key.
/// Some keys (e.g. ...\Control\Class\{guid}\Properties) are readable only by SYSTEM, even for
/// administrators. Reads treat such keys as unreadable rather than crashing; writes still throw.
/// </summary>
public sealed class WindowsRegistry : IRegistry
{
    private static bool IsDenied(Exception e) => e is System.Security.SecurityException or UnauthorizedAccessException;

    /// <summary>Opens a key for reading; null when missing. Sets <paramref name="denied"/> when it exists but can't be read.</summary>
    private static RegistryKey? OpenRead(RegistryKey b, string keyPath, out bool denied)
    {
        denied = false;
        try { return b.OpenSubKey(keyPath); }
        catch (Exception e) when (IsDenied(e)) { denied = true; return null; }
    }

    private static RegistryKey Base(RegHive hive) => RegistryKey.OpenBaseKey(
        hive == RegHive.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    public RegValue? GetValue(RegHive hive, string keyPath, string name)
    {
        using var b = Base(hive);
        using var key = OpenRead(b, keyPath, out _);
        if (key is null) return null;
        object? data;
        try { data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames); }
        catch (Exception e) when (IsDenied(e)) { return null; }
        if (data is null) return null;
        return key.GetValueKind(name) switch
        {
            RegistryValueKind.DWord => RegValue.DWord(unchecked((uint)(int)data)),
            RegistryValueKind.QWord => RegValue.QWord(unchecked((ulong)(long)data)),
            RegistryValueKind.String => RegValue.Str((string)data),
            RegistryValueKind.ExpandString => RegValue.Expand((string)data),
            RegistryValueKind.MultiString => RegValue.Multi((string[])data),
            RegistryValueKind.Binary => RegValue.Bin((byte[])data),
            _ => null,
        };
    }

    public void SetValue(RegHive hive, string keyPath, string name, RegValue value)
    {
        using var b = Base(hive);
        using var key = b.CreateSubKey(keyPath, writable: true);
        switch (value.Kind)
        {
            case RegKind.DWord: key.SetValue(name, unchecked((int)(uint)value.Data), RegistryValueKind.DWord); break;
            case RegKind.QWord: key.SetValue(name, unchecked((long)(ulong)value.Data), RegistryValueKind.QWord); break;
            case RegKind.String: key.SetValue(name, value.Data, RegistryValueKind.String); break;
            case RegKind.ExpandString: key.SetValue(name, value.Data, RegistryValueKind.ExpandString); break;
            case RegKind.MultiString: key.SetValue(name, value.Data, RegistryValueKind.MultiString); break;
            case RegKind.Binary: key.SetValue(name, value.Data, RegistryValueKind.Binary); break;
        }
    }

    public void DeleteValue(RegHive hive, string keyPath, string name)
    {
        using var b = Base(hive);
        using var key = b.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public bool KeyExists(RegHive hive, string keyPath)
    {
        using var b = Base(hive);
        using var key = OpenRead(b, keyPath, out var denied);
        return key is not null || denied;
    }

    public void DeleteKeyTree(RegHive hive, string keyPath)
    {
        using var b = Base(hive);
        b.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
    }

    public bool IsTreeEmpty(RegHive hive, string keyPath)
    {
        using var b = Base(hive);
        using var key = OpenRead(b, keyPath, out var denied);
        if (denied) return false; // can't prove it's empty, so never delete it
        return key is null || IsEmpty(key);

        static bool IsEmpty(RegistryKey k)
        {
            if (k.ValueCount > 0) return false;
            foreach (var name in k.GetSubKeyNames())
            {
                using var sub = OpenRead(k, name, out var subDenied);
                if (subDenied || (sub is not null && !IsEmpty(sub))) return false;
            }
            return true;
        }
    }

    public IReadOnlyList<string> GetSubKeyNames(RegHive hive, string keyPath)
    {
        using var b = Base(hive);
        using var key = OpenRead(b, keyPath, out _);
        if (key is null) return Array.Empty<string>();
        try { return key.GetSubKeyNames(); }
        catch (Exception e) when (IsDenied(e)) { return Array.Empty<string>(); }
    }
}
