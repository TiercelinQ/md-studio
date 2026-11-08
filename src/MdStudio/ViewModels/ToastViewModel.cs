using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MdStudio.Models;
using MdStudio.Resources;

namespace MdStudio.ViewModels;

/// <summary>
/// One toast (toast specification, sections 2, 4 and 6): texts, actions, and an auto-close timer that
/// pauses while hovered and resumes with the remaining time. Errors have no timer.
/// </summary>
public sealed partial class ToastViewModel : ObservableObject, IDisposable
{
    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private readonly Action<ToastViewModel> _remove;
    private readonly bool _animated;
    private TimeSpan _remaining;
    private long _startedAt;
    private bool _paused;
    private ITimer? _timer;
    private ITimer? _removal;

    /// <summary>Initializes a new instance of the <see cref="ToastViewModel"/> class.</summary>
    /// <param name="feedback">Type, title and message.</param>
    /// <param name="actions">Zero to two actions.</param>
    /// <param name="time">Clock and timers.</param>
    /// <param name="post">Runs a callback of a timer on the UI thread.</param>
    /// <param name="remove">Removes the toast from its host.</param>
    /// <param name="animated">False when Windows reduces animations: the toast is removed at once.</param>
    public ToastViewModel(ResultError feedback, IReadOnlyList<ToastAction> actions, TimeProvider time, Action<Action> post, Action<ToastViewModel> remove, bool animated)
    {
        _time = time;
        _post = post;
        _remove = remove;
        _animated = animated;
        Type = feedback.Type;
        Title = feedback.Title;
        Message = feedback.Message;
        Duration = Type switch
        {
            FeedbackType.Success => TimeSpan.FromMilliseconds(AppConfig.ToastSuccessMs),
            FeedbackType.Info => TimeSpan.FromMilliseconds(AppConfig.ToastInfoMs),
            FeedbackType.Warning => TimeSpan.FromMilliseconds(AppConfig.ToastWarningMs),
            _ => null,
        };
        _remaining = Duration ?? TimeSpan.Zero;
        LastShownAt = time.GetUtcNow();

        // The main action comes first (anatomy, section 4).
        Actions = [.. actions.OrderByDescending(a => a.IsPrimary).Select(a => new ToastActionItem(a.Label, a.IsPrimary, new RelayCommand(() => Run(a))))];
    }

    /// <summary>Gets the severity.</summary>
    public FeedbackType Type { get; }

    /// <summary>Gets the kicker: the type label in capitals, never set by the caller (TS-20).</summary>
    public string Kicker => KindLabel.ToUpper(CultureInfo.CurrentCulture);

    /// <summary>Gets the title.</summary>
    public string Title { get; }

    /// <summary>Gets the message.</summary>
    public string Message { get; }

    /// <summary>Gets the text read by screen readers: type, title and message (TS-61).</summary>
    public string AccessibleText => $"{KindLabel} : {Title}. {Message}";

    /// <summary>Gets a value indicating whether screen readers announce the toast as an alert (warning, error).</summary>
    public bool IsAlert => Type is FeedbackType.Warning or FeedbackType.Danger;

    /// <summary>Gets the display duration, or null for an error, which stays until closed (TS-40).</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Gets a value indicating whether the toast closes by itself (timer bar shown, TS-23).</summary>
    public bool HasTimer => Duration is not null;

    /// <summary>Gets the action buttons, main action first.</summary>
    public IReadOnlyList<ToastActionItem> Actions { get; }

    /// <summary>Gets a value indicating whether the toast has an action row.</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>Gets the time the toast was last shown or repeated (TS-49).</summary>
    public DateTimeOffset LastShownAt { get; private set; }

    /// <summary>Gets a value indicating whether the toast is closing (exit animation running).</summary>
    [ObservableProperty]
    public partial bool IsClosing { get; private set; }

    /// <summary>Gets a counter raised each time the timer starts over, so the view restarts its bar.</summary>
    [ObservableProperty]
    public partial int TimerRun { get; private set; }

    private string KindLabel => Type switch
    {
        FeedbackType.Success => Strings.Toast_Kind_Success,
        FeedbackType.Info => Strings.Toast_Kind_Info,
        FeedbackType.Warning => Strings.Toast_Kind_Warning,
        _ => Strings.Toast_Kind_Error,
    };

    /// <summary>Starts the auto-close timer, if the type has one.</summary>
    public void Start() => StartTimer();

    /// <summary>Freezes the timer while the pointer is over the toast (TS-41).</summary>
    public void Pause()
    {
        if (_timer is null || _paused)
        {
            return;
        }

        _remaining -= _time.GetElapsedTime(_startedAt);
        _timer.Dispose();
        _timer = null;
        _paused = true;
    }

    /// <summary>Resumes the timer with the remaining time, not from the start (TS-41).</summary>
    public void Resume()
    {
        if (!_paused)
        {
            return;
        }

        _paused = false;
        StartTimer();
    }

    /// <summary>Shows the same message again: the timer starts over instead of a second toast (TS-49).</summary>
    public void Restart()
    {
        LastShownAt = _time.GetUtcNow();
        if (Duration is not { } duration || IsClosing)
        {
            return;
        }

        _remaining = duration;
        TimerRun++;
        if (!_paused)
        {
            StartTimer();
        }
    }

    /// <summary>Closes the toast: the exit animation plays, then the toast is removed (TS-42).</summary>
    [RelayCommand]
    public void Close()
    {
        if (IsClosing)
        {
            return;
        }

        IsClosing = true;
        _timer?.Dispose();
        _timer = null;
        if (!_animated)
        {
            _remove(this);
            return;
        }

        _removal = _time.CreateTimer(_ => _post(() => _remove(this)), null, TimeSpan.FromMilliseconds(AppConfig.ToastRemoveDelayMs), Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _timer?.Dispose();
        _removal?.Dispose();
    }

    private void StartTimer()
    {
        if (Duration is null || IsClosing)
        {
            return;
        }

        _timer?.Dispose();
        _startedAt = _time.GetTimestamp();
        _timer = _time.CreateTimer(_ => _post(Close), null, _remaining > TimeSpan.Zero ? _remaining : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private void Run(ToastAction action)
    {
        // TS-43: the action runs, then the toast closes.
        action.Run();
        Close();
    }
}

/// <summary>Action button of a toast.</summary>
/// <param name="Label">Button text.</param>
/// <param name="IsPrimary">True for the main action (accent style).</param>
/// <param name="Command">Runs the action, then closes the toast.</param>
public sealed record ToastActionItem(string Label, bool IsPrimary, IRelayCommand Command);
