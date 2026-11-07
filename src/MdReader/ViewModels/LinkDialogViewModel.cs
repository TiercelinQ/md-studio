using CommunityToolkit.Mvvm.ComponentModel;

namespace MdReader.ViewModels;

/// <summary>Content of the link dialog: the address to insert (specification C10).</summary>
public sealed partial class LinkDialogViewModel : ObservableObject
{
    /// <summary>Gets or sets the typed address.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string Url { get; set; } = "https://";

    /// <summary>Gets a value indicating whether the address is an absolute http, https or mailto URI.</summary>
    public bool CanConfirm =>
        Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto)
        && (uri.Scheme == Uri.UriSchemeMailto || !string.IsNullOrEmpty(uri.Host));
}
