using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MdStudio.ViewModels;

namespace MdStudio.Views;

/// <summary>
/// One toast. Code-behind limited to view plumbing: enter and exit animations, the timer bar, the hover
/// pause forwarded to the ViewModel, and the screen reader announcement (toast specification 5.6, 7).
/// </summary>
public partial class ToastView : UserControl
{
    // Windows "show animations" setting: when off, no motion and no timer bar; the timer still runs.
    private readonly bool _animated = SystemParameters.ClientAreaAnimation;
    private ToastViewModel? _toast;
    private AnimationClock? _timerClock;

    /// <summary>Initializes a new instance of the <see cref="ToastView"/> class.</summary>
    public ToastView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        MouseEnter += (_, _) =>
        {
            _toast?.Pause();
            _timerClock?.Controller.Pause();
        };
        MouseLeave += (_, _) =>
        {
            _toast?.Resume();
            _timerClock?.Controller.Resume();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ToastViewModel toast || ReferenceEquals(toast, _toast))
        {
            return;
        }

        _toast = toast;
        toast.PropertyChanged += OnToastPropertyChanged;
        if (_animated)
        {
            PlayEnter();
            StartTimerBar();
        }
        else
        {
            TimerBar.Visibility = Visibility.Collapsed;
        }

        Announce();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_toast is not null)
        {
            _toast.PropertyChanged -= OnToastPropertyChanged;
        }

        _timerClock?.Controller.Stop();
    }

    private void OnToastPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToastViewModel.IsClosing) && _toast?.IsClosing == true && _animated)
        {
            _timerClock?.Controller.Pause();
            PlayExit();
        }
        else if (e.PropertyName == nameof(ToastViewModel.TimerRun) && _animated)
        {
            StartTimerBar();
        }
    }

    /// <summary>Opacity 0 to 1 and slide from the right, ToastEnterDuration, ToastEnterSpline.</summary>
    private void PlayEnter()
    {
        var duration = (Duration)FindResource("ToastEnterDuration");
        var spline = (KeySpline)FindResource("ToastEnterSpline");
        Slide.X = (double)FindResource("ToastEnterOffset");
        Opacity = 0;
        Slide.BeginAnimation(TranslateTransform.XProperty, Eased(0, duration, spline));
        BeginAnimation(OpacityProperty, Eased(1, duration, spline));
    }

    /// <summary>Opacity 1 to 0 and slide to the right, ToastExitDuration; the ViewModel removes the toast at 200 ms.</summary>
    private void PlayExit()
    {
        var duration = (Duration)FindResource("ToastExitDuration");
        Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation((double)FindResource("ToastExitOffset"), duration));
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, duration));
    }

    /// <summary>Linear shrink of the bar over the toast duration, started again when the message repeats.</summary>
    private void StartTimerBar()
    {
        if (_toast?.Duration is not { } duration)
        {
            return;
        }

        _timerClock?.Controller.Stop();
        _timerClock = new DoubleAnimation(1, 0, new Duration(duration)).CreateClock();
        TimerScale.ApplyAnimationClock(ScaleTransform.ScaleXProperty, _timerClock);
        if (IsMouseOver)
        {
            _timerClock.Controller.Pause();
        }
    }

    /// <summary>Asks screen readers to read the toast, politely or as an alert (TS-61). The focus does not move.</summary>
    private void Announce()
    {
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            UIElementAutomationPeer.CreatePeerForElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    private static DoubleAnimationUsingKeyFrames Eased(double to, Duration duration, KeySpline spline)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration.TimeSpan), spline));
        return animation;
    }
}
