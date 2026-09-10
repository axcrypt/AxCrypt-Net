using AxCrypt.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

using static AxCrypt.Abstractions.TypeResolve;

namespace AxCrypt.Core.UI
{
    public class ProgressBackground
    {
        private long _workerCount = 0;

        // Signals waiters when all in-flight operations complete (_workerCount → 0).
        // RunContinuationsAsynchronously ensures TrySetResult doesn't run continuations
        // inline on the thread that decrements the counter.
        private TaskCompletionSource<bool> _idleTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _tcsLock = new();

        public event EventHandler<ProgressBackgroundEventArgs> OperationStarted;

        protected virtual void OnOperationStarted(ProgressBackgroundEventArgs e)
        {
            OperationStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressBackgroundEventArgs> OperationCompleted;

        protected virtual void OnOperationCompleted(ProgressBackgroundEventArgs e)
        {
            OperationCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// Perform a background operation with support for progress bars and cancel.
        /// </summary>
        /// <param name="workFunction">A 'work' delegate, taking a ProgressContext and return a FileOperationStatus. Executed on a background thread. Not the calling thread.</param>
        /// <param name="complete">A 'complete' delegate, taking the final status. Executed on the GUI thread.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public Task WorkAsync(string name, Func<IProgressContext, Task<FileOperationContext>> workFunction, Func<FileOperationContext, Task> complete, IProgressContext progress)
        {
            Task task = New<IUIThread>().SendToAsync(() => BackgroundWorkWithProgressOnUIThreadAsync(name, workFunction, complete, progress));
            return task;
        }

        private async Task BackgroundWorkWithProgressOnUIThreadAsync(string name, Func<IProgressContext, Task<FileOperationContext>> workAsync, Func<FileOperationContext, Task> completeAsync, IProgressContext progress)
        {
            ProgressBackgroundEventArgs e = new ProgressBackgroundEventArgs(progress);
            try
            {
                long newCount = Interlocked.Increment(ref _workerCount);
                if (newCount == 1)
                {
                    // Transitioning from idle → busy: ensure a fresh, unsignalled TCS
                    // is in place before any caller can observe Busy = true.
                    lock (_tcsLock)
                    {
                        if (_idleTcs.Task.IsCompleted)
                            _idleTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                }
                OnOperationStarted(e);

                FileOperationContext result = await Task.Run(() => workAsync(progress));
                await completeAsync(result);
            }
            finally
            {
                OnOperationCompleted(e);
                long remaining = Interlocked.Decrement(ref _workerCount);
                if (remaining == 0)
                {
                    // All operations done: unblock any WaitForIdle callers.
                    _idleTcs.TrySetResult(true);
                }
            }
        }

        /// <summary>
        /// Wait for all operations to complete. Returns immediately when already idle.
        /// </summary>
        public Task WaitForIdle()
        {
            return Busy ? _idleTcs.Task : Task.CompletedTask;
        }

        public bool Busy
        {
            get
            {
                return _workerCount > 0;
            }
        }
    }
}
