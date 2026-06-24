using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Marshals async work onto a WinForms control's UI thread without blocking the message loop.
    /// </summary>
    public static class ControlInvokeAsyncHelper
    {
        public static Task<T> InvokeAsync<T>(Control control, Func<Task<T>> work)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (control.IsDisposed)
            {
                var canceled = new TaskCompletionSource<T>();
                canceled.TrySetCanceled();
                return canceled.Task;
            }

            // Same thread: return the inner task so callers can await without blocking the message loop
            // (async void + TCS on the UI thread would deadlock with GetAwaiter().GetResult() on that thread).
            if (!control.InvokeRequired)
                return work();

            var tcs = new TaskCompletionSource<T>();
            async void Execute()
            {
                try
                {
                    T value = await work().ConfigureAwait(true);
                    tcs.TrySetResult(value);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            control.BeginInvoke((Action)Execute);
            return tcs.Task;
        }

        public static async Task InvokeAsync(Control control, Func<Task> work)
        {
            await InvokeAsync(control, async () =>
            {
                await work().ConfigureAwait(true);
                return true;
            }).ConfigureAwait(false);
        }
    }
}
