using System.Globalization;
using System.Text.Json;

namespace Frtune.Core.Abstractions;

public enum RegHive
{
    LocalMachine,
    CurrentUser,
}

public enum RegKind
{
    DWord,
    QWord,
    String,
    ExpandString,
    MultiString,
    Binary,
}

/// <summary>
/// A typed registry value. Values are serialised to a compact "token" so that original
/// values can be stored in the JSON backup and written back byte-for-byte on revert.
/// </summary>
public sealed class RegValue
{
    public RegKind Kind { get; }
    public object Data { get; }

    private RegValue(RegKind kind, object data)
    {
        Kind = kind;
        Data = data;
    }

    public static RegValue DWord(uint value) => new(RegKind.DWord, value);
    public static RegValue QWord(ulong value) => new(RegKind.QWord, value);
    public static RegValue Str(string value) => new(RegKind.String, value);
    public static RegValue Expand(string value) => new(RegKind.ExpandString, value);
    public static RegValue Multi(params string[] value) => new(RegKind.MultiString, value);
    public static RegValue Bin(params byte[] value) => new(RegKind.Binary, value);

    public static RegValue FromHex(string hex) => Bin(Convert.FromHexString(hex.Replace(",", "").Replace(" ", "")));

    public uint AsDWord => Kind == RegKind.DWord ? (uint)Data : throw new InvalidOperationException("Not a DWORD");

    public string ToToken() => Kind switch
    {
        RegKind.DWord => "dword:" + ((uint)Data).ToString(CultureInfo.InvariantCulture),
        RegKind.QWord => "qword:" + ((ulong)Data).ToString(CultureInfo.InvariantCulture),
        RegKind.String => "sz:" + (string)Data,
        RegKind.ExpandString => "expand:" + (string)Data,
        RegKind.MultiString => "multi:" + JsonSerializer.Serialize((string[])Data),
        RegKind.Binary => "bin:" + Convert.ToHexString((byte[])Data),
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static RegValue FromToken(string token)
    {
        var i = token.IndexOf(':');
        if (i < 0) throw new FormatException($"Invalid registry token '{token}'");
        var kind = token[..i];
        var data = token[(i + 1)..];
        return kind switch
        {
            "dword" => DWord(uint.Parse(data, CultureInfo.InvariantCulture)),
            "qword" => QWord(ulong.Parse(data, CultureInfo.InvariantCulture)),
            "sz" => Str(data),
            "expand" => Expand(data),
            "multi" => Multi(JsonSerializer.Deserialize<string[]>(data) ?? Array.Empty<string>()),
            "bin" => Bin(Convert.FromHexString(data)),
            _ => throw new FormatException($"Unknown registry token kind '{kind}'"),
        };
    }

    public bool ValueEquals(RegValue? other)
    {
        if (other is null || other.Kind != Kind) return false;
        return Kind switch
        {
            RegKind.MultiString => ((string[])Data).SequenceEqual((string[])other.Data),
            RegKind.Binary => ((byte[])Data).AsSpan().SequenceEqual((byte[])other.Data),
            _ => Data.Equals(other.Data),
        };
    }

    public override string ToString() => Kind switch
    {
        RegKind.DWord => $"DWORD 0x{(uint)Data:X} ({(uint)Data})",
        RegKind.QWord => $"QWORD 0x{(ulong)Data:X}",
        RegKind.String => $"SZ \"{Data}\"",
        RegKind.ExpandString => $"EXPAND_SZ \"{Data}\"",
        RegKind.MultiString => $"MULTI_SZ [{string.Join(", ", (string[])Data)}]",
        RegKind.Binary => $"BINARY {BitConverter.ToString((byte[])Data).Replace('-', ' ')}",
        _ => Data.ToString() ?? "",
    };

    public static string HiveName(RegHive hive) => hive == RegHive.LocalMachine ? "HKLM" : "HKCU";
}
