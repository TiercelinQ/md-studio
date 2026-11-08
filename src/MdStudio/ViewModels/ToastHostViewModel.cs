using System.Collections.ObjectModel;
using MdStudio.Models;

namespace MdStudio.ViewModels;

/// <summary>Shows messages to the user as toasts (toast specification, section 6.1).</summary>
public interface IFeedbackService
{
    /// <summary>Shows a toast. Its type alone sets its duration, persistence and accessibility role.</summary>
    /// <param name="feedback">Type, title and message.</param>
    /// <param name="actions">Zero to two action buttons (TS-03).</param>
    void Show(ResultError feedback, IReadOnlyList<ToastAction>? actions = null);
}

/// <summary>
/// Stack of toasts at the bottom right of the window (deviation E-24: the feedback service is a
/// ViewModel, its state and timers being presentation state). At most four visible, the oldest
/// non-error evicted first, repeated messages merged.
/// </summary>
public sealed class ToastHostViewModel : IFeedbackService
{
    private const int MaxActions = 2;

    private readonly TimeProvider _time;
    private readonly Func<bool> _animationsEnabled;
    private readonly SynchronizationContext? _context;
    private readonly int _uiThreadId;

    /// <summary>Initializes a new instance of the <see cref="ToastHostViewModel"/> class, on the UI thread.</summary>
    /// <param name="time">Clock and timers.</param>
    /// <param name="animationsEnabled">False when Windows reduces animations (section 5.6).</param>
    public ToastHostViewModel(TimeProvider time, Func<bool> animationsEnabled)
    {
        _time = time;
        _animationsEnabled = animationsEnabled;
        _context = SynchronizationContext.Current;

        // The thread, not the context instance: WPF hands out new DispatcherSynchronizationContext
        // instances on the same thread, so comparing instances would repost forever.
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>Gets the toasts, the oldest first (top of the stack).</summary>
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <inheritdoc/>
    public void Show(ResultError feedback, IReadOnlyList<ToastAction>? actions = null)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(actions?.Count ?? 0, MaxActions);
        if (_context is not null && Environment.CurrentManagedThreadId != _uiThreadId)
        {
            // Background source: a caller off the UI thread; toasts are created on it (TS-53).
            _context.Post(_ => Show(feedback, actions), null);
            return;
        }

        var now = _time.GetUtcNow();
        var repeated = Toasts.FirstOrDefault(t => !t.IsClosing
            && t.Title == feedback.Title
            && t.Message == feedback.Message
            && now - t.LastShownAt < TimeSpan.FromMilliseconds(AppConfig.ToastRepeatWindowMs));
        if (repeated is not null)
        {
            repeated.Restart();
            return;
        }

        var toast = new ToastViewModel(feedback, actions ?? [], _time, Post, Remove, _animationsEnabled());
        Toasts.Add(toast);
        if (Toasts.Count(t => !t.IsClosing) > AppConfig.ToastMaxVisible)
        {
            Evict();
        }

        toast.Start();
    }

    /// <summary>
    /// Removes one older toast without exit animation (TS-13, TS-46): the oldest non-error, else the
    /// oldest error (TS-50). The toast just added is never the one removed.
    /// </summary>
    private void Evict()
    {
        var older = Toasts.Where(t => !t.IsClosing).SkipLast(1).ToList();
        Remove(older.FirstOrDefault(t => t.Type != FeedbackType.Danger) ?? older[0]);
    }

    private void Remove(ToastViewModel toast)
    {
        if (Toasts.Remove(toast))
        {
            toast.Dispose();
        }
    }

    private void Post(Action action)
    {
        // Background source: the timers of TimeProvider run on the thread pool.
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }
}
