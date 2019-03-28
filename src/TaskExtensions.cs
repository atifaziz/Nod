#region Copyright (c) 2019 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace Nod
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Unit = System.ValueTuple;

    static class TaskExtensions
    {
        public static async Task WhenAll(this IEnumerable<Task> tasks, TimeSpan timeout)
        {
            using (var cancellationTokenSource = new CancellationTokenSource(timeout))
                await tasks.WhenAll(cancellationTokenSource.Token);
        }

        public static async Task WhenAll(this IEnumerable<Task> tasks, CancellationToken cancellationToken)
        {
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));

            if (!cancellationToken.CanBeCanceled)
            {
                await Task.WhenAll(tasks);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                var taskCompletionSource = new TaskCompletionSource<object>();
                using (cancellationToken.Register(() => taskCompletionSource.TrySetResult(null)))
                {
                    var task = await Task.WhenAny(taskCompletionSource.Task, Task.WhenAll(tasks));
                    if (task == taskCompletionSource.Task)
                        throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        public static void Do(this Task task,
            Action onResult, Action<Exception> onErrorOrCancellation)
            => task.Do(onResult, onErrorOrCancellation,
                       () => onErrorOrCancellation(null));

        public static void Do(this Task task,
            Action onResult, Action<Exception> onError, Action onCancellation)
            => task.Match(() => { onResult(); return new Unit(); },
                          e  => { onError(e); return new Unit(); },
                          () => { onCancellation(); return new Unit(); });

        public static void Do<T>(this Task<T> task,
            Action<T> onResult, Action<Exception> onErrorOrCancellation)
            => task.Do(onResult, onErrorOrCancellation,
                       () => onErrorOrCancellation(null));

        public static void Do<T>(this Task<T> task,
            Action<T> onResult, Action<Exception> onError, Action onCancellation)
            => task.Match(r  => { onResult(r); return new Unit(); },
                          e  => { onError(e); return new Unit(); },
                          () => { onCancellation(); return new Unit(); });

        public static TResult Match<T, TResult>(this Task<T> task,
            Func<T, TResult> onResult, Func<Exception, TResult> onError, Func<TResult> onCancellation)
            => task.IsFaulted  ? onError(task.Exception)
             : task.IsCanceled ? onCancellation()
             : onResult(task.Result);

        public static TResult Match<TResult>(this Task task,
            Func<TResult> onResult, Func<Exception, TResult> onError, Func<TResult> onCancellation)
            => task.IsFaulted  ? onError(task.Exception)
             : task.IsCanceled ? onCancellation()
             : onResult();
    }
}
