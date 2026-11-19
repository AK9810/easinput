using EasInput.Core;

namespace EasInput.Core.Tests;

public class TriggerPolicyTests
{
    private static readonly IntPtr WindowA = new(1);
    private static readonly IntPtr WindowB = new(2);

    [Fact]
    public void FirstObservationDoesNotTriggerButSeeds()
    {
        var policy = new TriggerPolicy();

        Assert.Equal(BubbleTrigger.None, policy.Observe(InputMode.Chinese, WindowA));
        Assert.Equal(BubbleTrigger.None, policy.Observe(InputMode.Chinese, WindowA));
        Assert.Equal(BubbleTrigger.FocusChange, policy.Observe(InputMode.Chinese, WindowB));
    }

    [Fact]
    public void SameModeAndWindowDoesNotTrigger()
    {
        var policy = new TriggerPolicy();
        policy.Seed(InputMode.Chinese, WindowA);

        Assert.Equal(BubbleTrigger.None, policy.Observe(InputMode.Chinese, WindowA));
    }

    [Fact]
    public void NewWindowTriggersEvenWhenModeIsUnchanged()
    {
        // The user's report: switching to a window that has a caret feels like it should show
        // the current mode, even when the mode itself did not change.
        var policy = new TriggerPolicy();
        policy.Seed(InputMode.Chinese, WindowA);

        Assert.Equal(BubbleTrigger.FocusChange, policy.Observe(InputMode.Chinese, WindowB));
    }

    [Fact]
    public void ModeChangeInSameWindowTriggersModeChange()
    {
        var policy = new TriggerPolicy();
        policy.Seed(InputMode.Chinese, WindowA);

        Assert.Equal(BubbleTrigger.ModeChange, policy.Observe(InputMode.LatinLower, WindowA));
    }

    [Fact]
    public void FocusChangeWinsWhenWindowAndModeBothChange()
    {
        var policy = new TriggerPolicy();
        policy.Seed(InputMode.Chinese, WindowA);

        Assert.Equal(BubbleTrigger.FocusChange, policy.Observe(InputMode.LatinUpper, WindowB));
    }

    [Fact]
    public void ObserveConsumesTheTriggerSoRepeatedPollsStayQuiet()
    {
        var policy = new TriggerPolicy();
        policy.Seed(InputMode.Chinese, WindowA);

        Assert.Equal(BubbleTrigger.FocusChange, policy.Observe(InputMode.Chinese, WindowB));
        Assert.Equal(BubbleTrigger.None, policy.Observe(InputMode.Chinese, WindowB));
    }

    [Fact]
    public void ReturningToAPreviouslySeenWindowTriggersAgain()
    {
        var policy = new TriggerPolicy();
        policy.Seed(InputMode.Chinese, WindowA);

        Assert.Equal(BubbleTrigger.FocusChange, policy.Observe(InputMode.Chinese, WindowB));
        Assert.Equal(BubbleTrigger.FocusChange, policy.Observe(InputMode.Chinese, WindowA));
    }
}
