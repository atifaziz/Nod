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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class TimerService : HostService
    {
        int _id;

        readonly Dictionary<int, CancellationTokenSource>
            _cancellationTokenSources = new Dictionary<int, CancellationTokenSource>();

        public TimerService(ScheduleTaskHandler scheduler) :
            base(scheduler) {}

        public void CancelAll()
        {
            // Materialize before looping to prevent InvalidOperationException:
            // "Collection was modified; enumeration operation may not execute."

            foreach (var e in _cancellationTokenSources.ToArray())
            {
                InfoLog?.Invoke(this, $"{nameof(CancelAll)}(id = {e.Key})");
                e.Value.Cancel();
            }
        }

        public void ClearTimeout(int id)
        {
            InfoLog?.Invoke(this, $"{nameof(ClearTimeout)}(id = {id})");

            if (!_cancellationTokenSources.TryGetValue(id, out var s))
                return;

            s.Cancel();
        }

        public int SetTimeout(Action callback, int milliseconds)
        {
            var id = ++_id;

            InfoLog?.Invoke(this, $"{nameof(SetTimeout)}(callback, milliseconds = {milliseconds}) -> {id}");

            var cancellationTokenSource = _cancellationTokenSources[id] = new CancellationTokenSource();

            Schedule(
                $"{nameof(TimerService)}[{id}]",
                cancellationTokenSource.Token,
                async cancellationToken =>
                {
                    await Task.Delay(milliseconds, cancellationToken);
                    callback();
                },
                onFinally: () =>
                {
                   _cancellationTokenSources.Remove(id);
                   cancellationTokenSource.Dispose();
                });

            return id;
        }
    }
}
