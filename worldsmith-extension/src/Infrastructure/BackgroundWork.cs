using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WorldsmithExtension
{
    internal static class BackgroundWork
    {
        internal static Action<Action, Action<Exception>> On(Dispatcher dispatcher)
        {
            return (work, finished) => Task.Run(() =>
            {
                Exception failure = null;
                try { work(); } catch (Exception error) { failure = error; }
                // Workflow completion owns leases, even if its view is no longer visible
                dispatcher.Invoke(new Action(() => finished(failure)));
            });
        }
    }
}
