using System.Windows;
using MdReader.Models;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MdReader.Services;

/// <summary>Shows messages to the user as notifications (component C-18).</summary>
public interface IFeedbackService
{
    /// <summary>Shows a message. A new message replaces the one on screen.</summary>
    /// <param name="feedback">Message and severity.</param>
    void Show(ResultError feedback);
}

/// <summary>
/// WPF UI implementation of <see cref="IFeedbackService"/>: one notification at a time, information
/// for 3.2 s, warnings and errors until closed or replaced (deviation E-05).
/// </summary>
/// <param name="snackbars">WPF UI snackbar service holding the presenter of the shell.</param>
/// <param name="logger">Logger.</param>
public sealed partial class FeedbackService(ISnackbarService snackbars, ILogger<FeedbackService> logger) : IFeedbackService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification ignoree, aucun presentateur : {Message}")]
    private static partial void LogNoPresenter(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec d'affichage d'une notification")]
    private static partial void LogNotificationFailed(ILogger logger, Exception? exception);

    /// <inheritdoc/>
    public void Show(ResultError feedback)
    {
        var presenter = snackbars.GetSnackbarPresenter();
        if (presenter is null)
        {
            LogNoPresenter(logger, feedback.Message);
            return;
        }

        var persistent = feedback.Type is FeedbackType.Danger or FeedbackType.Warning;
        var snackbar = new Snackbar(presenter)
        {
            Content = feedback.Description is null ? feedback.Message : $"{feedback.Message}\n{feedback.Description}",
            Appearance = persistent ? ControlAppearance.Danger : ControlAppearance.Info,
            IsCloseButtonEnabled = persistent,
            Timeout = persistent ? TimeSpan.FromMilliseconds(int.MaxValue) : TimeSpan.FromMilliseconds(AppConfig.InfoNotificationMs),
            Style = Application.Current.TryFindResource("NotificationStyle") as Style,
        };

        _ = DisplayAsync(presenter, snackbar);
    }

    private async Task DisplayAsync(SnackbarPresenter presenter, Snackbar snackbar)
    {
        try
        {
            await presenter.ImmediatelyDisplay(snackbar);
        }
        catch (InvalidOperationException ex)
        {
            LogNotificationFailed(logger, ex);
        }
    }
}
