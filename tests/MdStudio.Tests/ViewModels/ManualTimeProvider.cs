namespace MdStudio.Tests.ViewModels;

/// <summary>Clock driven by the test: time only moves on <see cref="Advance"/>, which fires the timers due on the way.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset Origin = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
    private readonly List<ManualTimer> _timers = [];
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public override DateTimeOffset GetUtcNow() => Origin.AddTicks(_ticks);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan delay)
    {
        var target = _ticks + delay.Ticks;
        while (_timers.Where(t => t.Due <= target).OrderBy(t => t.Due).FirstOrDefault() is { } next)
        {
            _ticks = next.Due!.Value;
            next.Fire();
        }

        _ticks = target;
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public long? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            owner._timers.Remove(this);
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._ticks + dueTime.Ticks;
            if (Due is not null)
            {
                owner._timers.Add(this);
            }

            return true;
        }

        public void Fire()
        {
            owner._timers.Remove(this);
            Due = null;
            callback(state);
        }

        public void Dispose() => owner._timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
