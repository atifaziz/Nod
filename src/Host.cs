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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ClearScript;
    using static MoreLinq.Extensions.ToDelimitedStringExtension;

    public class Host : ExtendedHostFunctions, IDisposable, ILogSource
    {
        readonly Action<string> _loader;
        readonly ScheduleTaskHandler _scheduler;

        internal event EventHandler<HostService> ServiceCreated;
        internal event EventHandler<NamedTask> TaskStarting;
        internal event EventHandler<NamedTask> TaskFinishing;
        internal event EventHandler<NamedTask> TaskFinished;

        public Host(Action<string> loader,
                    ConsoleService console,
                    TaskScheduler taskScheduler)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));

            if (taskScheduler == null) throw new ArgumentNullException(nameof(taskScheduler));

            var scheduler = _scheduler = Schedule;

            Console = console;
            Timer   = new TimerService(scheduler);
            Xhr     = new XhrService(scheduler);

            void Schedule(ILogSource source, string name, CancellationToken cancellationToken,
                          Func<CancellationToken, Task> onSchedule,
                          Action<Exception> onError,
                          Action<OperationCanceledException> onCancel,
                          Action onFinally)
            {
                var task =
                    AsyncTask.Create(name,
                        async thisTask =>
                        {
                            try
                            {
                                await onSchedule(cancellationToken);
                                TaskFinishing?.Invoke(this, thisTask);
                                thisTask.FlagSuccess();
                            }
                            catch (OperationCanceledException e)
                            {
                                try
                                {
                                    source.WarnLog?.Invoke(source, e, name);
                                    onCancel?.Invoke(e);
                                }
                                finally
                                {
                                    TaskFinishing?.Invoke(this, thisTask);
                                    thisTask.FlagCanceled(e.CancellationToken);
                                }
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    source.ErrorLog?.Invoke(source, e, name);
                                    onError?.Invoke(e);
                                }
                                finally
                                {
                                    TaskFinishing?.Invoke(this, thisTask);
                                    thisTask.FlagError(e);
                                }
                            }
                            finally
                            {
                                try
                                {
                                    onFinally?.Invoke();
                                }
                                finally
                                {
                                    TaskFinished?.Invoke(this, thisTask);
                                }
                            }
                        });

                TaskStarting?.Invoke(this, task);
                task.Start(taskScheduler);
            }
        }

        public ConsoleService Console { get; }
        public TimerService   Timer   { get; }
        public XhrService     Xhr     { get; }

        public HostWebSocket CreateWebSocket(int id, string url, string[] protocols, dynamic callback)
        {
            InfoLog?.Invoke(this, $"{nameof(CreateWebSocket)}(url = {url}, protocols = {protocols?.ToDelimitedString(",").Bracket("[", "]")}, callback)");
            var socket = new HostWebSocket(id, url, protocols ?? Enumerable.Empty<string>(), callback, _scheduler);
            ServiceCreated?.Invoke(this, socket);
            return socket;
        }

        public void Load(string module)
        {
            InfoLog?.Invoke(this, $"load: {module}");
            _loader(module);
        }

        public void Dispose() => Xhr.Dispose();

        internal LogEventHandler InfoLog       { get; set; }
        internal ErrorLogEventHandler WarnLog  { get; set; }
        internal ErrorLogEventHandler ErrorLog { get; set; }

        LogEventHandler ILogSource.InfoLog       { get => InfoLog;  set => InfoLog  = value; }
        ErrorLogEventHandler ILogSource.WarnLog  { get => WarnLog;  set => WarnLog  = value; }
        ErrorLogEventHandler ILogSource.ErrorLog { get => ErrorLog; set => ErrorLog = value; }
    }
}
