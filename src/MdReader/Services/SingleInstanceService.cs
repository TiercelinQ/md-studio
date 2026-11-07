using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MdReader.Services;

/// <summary>Keeps a single running instance and forwards the paths of later launches to it (F-OUV-06).</summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>Raised on a background thread with the paths sent by a later launch (possibly empty).</summary>
    event EventHandler<IReadOnlyList<string>>? PathsReceived;

    /// <summary>Tries to become the single instance.</summary>
    /// <returns>True for the first instance.</returns>
    bool TryAcquire();

    /// <summary>Sends paths to the first instance.</summary>
    /// <param name="paths">Absolute paths, possibly empty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when delivered.</returns>
    Task<bool> SendAsync(IReadOnlyList<string> paths, CancellationToken ct);

    /// <summary>Starts receiving paths from later launches.</summary>
    void StartListening();
}

/// <summary>Named mutex + named pipe implementation of <see cref="ISingleInstanceService"/>.</summary>
/// <param name="logger">Logger.</param>
public sealed partial class SingleInstanceService(ILogger<SingleInstanceService> logger) : ISingleInstanceService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Premiere instance injoignable")]
    private static partial void LogFirstInstanceUnreachable(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Echec de reception d'une seconde instance")]
    private static partial void LogReceiveFailed(ILogger logger, Exception? exception);

    private const int ConnectTimeoutMs = 2000;

    private static readonly string Identity = $"{AppConfig.AppName}.{Environment.UserName}";

    private readonly CancellationTokenSource _stop = new();
    private Mutex? _mutex;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<IReadOnlyList<string>>? PathsReceived;

    private static string PipeName => $"{Identity}.Pipe";

    /// <inheritdoc/>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{Identity}.SingleInstance", out var createdNew);
        return createdNew;
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(IReadOnlyList<string> paths, CancellationToken ct)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(ConnectTimeoutMs, ct).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(string.Join('\n', paths));
            await client.WriteAsync(bytes, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            LogFirstInstanceUnreachable(logger, ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public void StartListening() => _ = ListenAsync(_stop.Token);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Cancel();
        _stop.Dispose();
        _mutex?.Dispose();
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                var content = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                var paths = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                PathsReceived?.Invoke(this, paths);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException ex)
            {
                LogReceiveFailed(logger, ex);
            }
        }
    }
}
