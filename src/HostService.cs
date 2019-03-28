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
    using System.Threading;
    using System.Threading.Tasks;

    public delegate void LogEventHandler(object sender, string message);
    public delegate void ErrorLogEventHandler(object sender, Exception exception, string message);

    public interface ILogSource
    {
        LogEventHandler      InfoLog  { get; set; }
        ErrorLogEventHandler WarnLog  { get; set; }
        ErrorLogEventHandler ErrorLog { get; set; }
    }

    public abstract class LogSource : ILogSource
    {
        internal LogEventHandler      InfoLog  { get; set; }
        internal ErrorLogEventHandler WarnLog  { get; set; }
        internal ErrorLogEventHandler ErrorLog { get; set; }

        LogEventHandler      ILogSource.InfoLog  { get => InfoLog;  set => InfoLog  = value; }
        ErrorLogEventHandler ILogSource.WarnLog  { get => WarnLog;  set => WarnLog  = value; }
        ErrorLogEventHandler ILogSource.ErrorLog { get => ErrorLog; set => ErrorLog = value; }
    }

    public delegate void ScheduleTaskHandler(
        ILogSource source, string name, CancellationToken cancellationToken,
        Func<CancellationToken, Task> onSchedule,
        Action<Exception> onError,
        Action<OperationCanceledException> onCancel,
        Action onFinally);

    public abstract class HostService : LogSource
    {
        readonly ScheduleTaskHandler _scheduleTaskHandler;

        protected HostService(ScheduleTaskHandler scheduleTaskHandler) =>
            _scheduleTaskHandler = scheduleTaskHandler ?? throw new ArgumentNullException(nameof(scheduleTaskHandler));

        protected void Schedule(
            string name, CancellationToken cancellationToken,
            Func<CancellationToken, Task> onSchedule,
            Action<Exception> onError = null,
            Action<OperationCanceledException> onCancel = null,
            Action onFinally = null) =>
            _scheduleTaskHandler(this, name, cancellationToken,
                                 onSchedule, onError, onCancel, onFinally);
    }
}
