using System;
using System.Threading;
using System.Threading.Tasks;

namespace GlassBar.Dashboard
{
    public class DebounceTimer
    {
        private readonly int _delayMs;
        private readonly Func<Task> _action;
        private CancellationTokenSource _cts;

        public DebounceTimer(int delayMs, Func<Task> action)
        {
            _delayMs = delayMs;
            _action = action;
        }

        public void Trigger()
        {
            // Cancel and dispose previous timer
            var oldCts = _cts;
            _cts = new CancellationTokenSource();
            oldCts?.Cancel();
            oldCts?.Dispose();

            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_delayMs, token);
                    if (!token.IsCancellationRequested)
                    {
                        await _action();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when debouncing
                }
            }, token);
        }
    }
}
