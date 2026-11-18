using EasInput.Core;

namespace EasInput.Core.Tests;

public class KeyKindsTests
{
    [Theory]
    [InlineData(0x10)] // VK_SHIFT
    [InlineData(0x11)] // VK_CONTROL
    [InlineData(0x12)] // VK_MENU
    [InlineData(0xA0)] // VK_LSHIFT
    [InlineData(0xA1)] // VK_RSHIFT
    [InlineData(0xA2)] // VK_LCONTROL
    [InlineData(0xA3)] // VK_RCONTROL
    [InlineData(0xA4)] // VK_LMENU
    [InlineData(0xA5)] // VK_RMENU
    [InlineData(0x5B)] // VK_LWIN
    [InlineData(0x5C)] // VK_RWIN
    public void ModifierKeysAreRecognized(int virtualKey)
    {
        Assert.True(KeyKinds.IsModifier(virtualKey));
    }

    [Theory]
    [InlineData(0x41)] // 'A'
    [InlineData(0x14)] // VK_CAPITAL
    public void NonModifierKeysAreRejected(int virtualKey)
    {
        Assert.False(KeyKinds.IsModifier(virtualKey));
    }
}
