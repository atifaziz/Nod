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
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Unit = System.ValueTuple;

    readonly struct AsyncTask
    {
        public static AsyncTask Create(string name, Action<AsyncTaskControl> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var taskCompletionSource = new TaskCompletionSource<Unit>();
            var task = taskCompletionSource.Task;
            var namedTask = new NamedTask(string.IsNullOrEmpty(name) ? "Task#" + task.Id : name, task);
            var control = new AsyncTaskControl(namedTask, taskCompletionSource);
            return new AsyncTask(namedTask, new Task(() => action(control), TaskCreationOptions.DenyChildAttach));
        }

        readonly NamedTask _task;
        readonly Task _inner;

        AsyncTask(NamedTask task, Task inner)
        {
            _task  = task;
            _inner = inner;
        }

        public string Name => _task.Name;
        public Task   Task => _task.Task;

        public NamedTask Start(TaskScheduler scheduler)
        {
            _inner.Start(scheduler);
            return this;
        }

        public static implicit operator Task(AsyncTask task) => task.Task;
        public static implicit operator NamedTask(AsyncTask task) => task._task;
    }

    [DebuggerDisplay("{" + nameof(Name) + "} ({" + nameof(Task) + "})")]
    readonly struct NamedTask : IEquatable<NamedTask>
    {
        public NamedTask(string name, Task task)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Task = task ?? throw new ArgumentNullException(nameof(task));
        }

        public string Name { get; }
        public Task Task   { get; }

        public bool Equals(NamedTask other) =>
            string.Equals(Name, other.Name) && Task.Equals(other.Task);

        public override bool Equals(object obj) =>
            obj is NamedTask other && Equals(other);

        public override int GetHashCode() =>
            unchecked((Name.GetHashCode() * 397) ^ Task.GetHashCode());

        public static bool operator ==(NamedTask left, NamedTask right) =>
            left.Equals(right);

        public static bool operator !=(NamedTask left, NamedTask right) =>
            !left.Equals(right);

        public static implicit operator Task(NamedTask task) => task.Task;
    }

    readonly struct AsyncTaskControl
    {
        readonly TaskCompletionSource<Unit> _completionSource;

        public AsyncTaskControl(NamedTask task, TaskCompletionSource<Unit> completionSource)
        {
            Task = task;
            _completionSource = completionSource ?? throw new ArgumentNullException(nameof(completionSource));
        }

        public NamedTask Task { get; }

        public void FlagSuccess() => _completionSource.SetResult(default);
        public void FlagError(Exception exception) => _completionSource.SetException(exception);
        public void FlagCanceled(CancellationToken cancellationToken) => _completionSource.TrySetCanceled(cancellationToken);

        public static implicit operator NamedTask(AsyncTaskControl task) => task.Task;
    }
}
