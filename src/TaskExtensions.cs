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
    }
}
