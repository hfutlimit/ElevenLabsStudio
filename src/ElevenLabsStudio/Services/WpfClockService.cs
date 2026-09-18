using System.Windows.Threading;
using ElevenLabsStudio.Core.Abstractions;

namespace ElevenLabsStudio.Services;

/// <summary>
/// Production implementation of <see cref="IClockService"/> backed by
/// <c>System.Windows.Threading.DispatcherTimer</c>. Keeps the WPF
/// dependency in this one file so view-models can be tested without
/// WPF.
/// </summary>
public sealed class WpfClockService : IClockService
{
    public DateTime Now => DateTime.Now;

    public IDisposable Start(TimeSpan interval, Action tick)
    {
        var timer = new DispatcherTimer { Interval = interval };
        var handler = new EventHandler((_, _) => tick());
        timer.Tick += handler;
        timer.Start();
        return new TimerHandle(timer, handler);
    }

    private sealed class TimerHandle : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly EventHandler _handler;

        public TimerHandle(DispatcherTimer timer, EventHandler handler)
        {
            _timer = timer;
            _handler = handler;
        }

        public void Dispose()
        {
            _timer.Tick -= _handler;
            _timer.Stop();
        }
    }
}
