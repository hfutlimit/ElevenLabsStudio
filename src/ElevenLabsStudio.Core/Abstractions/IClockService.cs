namespace ElevenLabsStudio.Core.Abstractions;

/// <summary>
/// Clock + timer abstraction so view-models don't reach for
/// <c>System.Windows.Threading.DispatcherTimer</c> directly. Keeps the
/// "what does 1Hz mean" decision in one place and lets unit tests
/// substitute a deterministic clock.
/// </summary>
public interface IClockService
{
    /// <summary>Current wall-clock time, evaluated lazily on each call.</summary>
    DateTime Now { get; }

    /// <summary>
    /// Schedule <paramref name="tick"/> to fire roughly every
    /// <paramref name="interval"/>. Returns an <see cref="IDisposable"/>
    /// the caller can use to stop the timer (e.g. from a Dispose path).
    /// Implementations may coalesce overlapping registrations.
    /// </summary>
    IDisposable Start(TimeSpan interval, Action tick);
}
