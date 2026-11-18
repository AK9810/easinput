namespace EasInput.Core.Interop;

/// <summary>Values from ime_cmodes.h.</summary>
internal static class ImeConversionMode
{
    public const uint Alphanumeric = 0x0000;
    public const uint Native = 0x0001;
    public const uint Katakana = 0x0002;
    public const uint Language = 0x0003;
    public const uint FullShape = 0x0008;
    public const uint Roman = 0x0010;
    public const uint CharCode = 0x0020;
    public const uint HanjaConvert = 0x0040;
    public const uint SoftKeyboard = 0x0080;
    public const uint NoConversion = 0x0100;
    public const uint Eudc = 0x0200;
    public const uint Symbol = 0x0400;
    public const uint Fixed = 0x0800;
}
