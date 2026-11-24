namespace EasInput.Core;

public enum BubbleTrigger
{
    None,
    ModeChange,
    FocusChange,
}

/// <summary>
/// Decides when the bubble owes the user a show. Switching to a different focus window counts
/// as a trigger even when the input mode is unchanged; repeated observations of the same window
/// and mode are quiet, so the 300 ms poll does not re-show the bubble.
/// </summary>
public sealed class TriggerPolicy
{
    private InputMode _mode;
    private IntPtr _window;
    private bool _seeded;

    /// <summary>The mode last reported or seeded; the value the next observation is compared to.</summary>
    public InputMode CurrentMode => _mode;

    /// <summary>The window last reported or seeded.</summary>
    public IntPtr CurrentWindow => _window;

    /// <summary>False until the first observation; a failed startup read leaves this false.</summary>
    public bool IsSeeded => _seeded;

    /// <summary>Remembers the current state without treating it as a change (app startup).</summary>
    public void Seed(InputMode mode, IntPtr window)
    {
        _mode = mode;
        _window = window;
        _seeded = true;
    }

    /// <summary>
    /// Reports and consumes the trigger for this observation. A focus change takes precedence
    /// over a simultaneous mode change, because the new window is the more visible event.
    /// </summary>
    public BubbleTrigger Observe(InputMode mode, IntPtr window)
    {
        if (!_seeded)
        {
            Seed(mode, window);
            return BubbleTrigger.None;
        }

        if (window != _window)
        {
            _window = window;
            _mode = mode;
            return BubbleTrigger.FocusChange;
        }

        if (mode != _mode)
        {
            _mode = mode;
            return BubbleTrigger.ModeChange;
        }

        return BubbleTrigger.None;
    }
}
