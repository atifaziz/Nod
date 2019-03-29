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
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using JavaScriptEngineSwitcher.V8;
    using NDesk.Options;
    using SysConsole = System.Console;

    static class Program
    {
        static bool _verbose;

        static async Task Wain(IEnumerable<string> args)
        {
            var inspect = false;
            var pauseDebuggerOnStart = false;
            var inspectLoadSet = new HashSet<string>(StringComparer.Ordinal);

            var options = new OptionSet
            {
                { "v|verbose", "enable verbose output",
                   _ => _verbose = true },

                { "debug", "break into debugger on start",
                   _ => Debugger.Launch() },

                { "inspect", "activate inspector and break at start of script",
                  _ => inspect = Environment.UserInteractive },

                { "inspect-brk", "activate inspector and break at start of script",
                  _ => pauseDebuggerOnStart = Environment.UserInteractive },

                { "inspect-brk-load=", "activate inspector and break on load of script",
                  v => { if (Environment.UserInteractive) inspectLoadSet.Add(v); } },
            };

            var tail = options.Parse(args);

            if (tail.Count == 0)
                throw new Exception("Missing script file path specification.");

            var scriptPathFile = new FileInfo(tail.First());

            if (!scriptPathFile.Exists)
                throw new FileNotFoundException("File not found: " + scriptPathFile);

            await Run(scriptPathFile, tail.Skip(1).ToArray(),
                      inspect, pauseDebuggerOnStart,
                      ImmutableHashSet.CreateRange(
                          from e in inspectLoadSet
                          where !string.IsNullOrEmpty(e)
                          select e),
                      SysConsole.In.ReadLines(),
                      _verbose);
        }

        static async Task
            Run(FileInfo mainScriptFile, string[] argv,
                bool inspect, bool pauseDebuggerOnStart,
                IImmutableSet<string> inspectLoadSet,
                IEnumerator<string> commands,
                bool verbose)
        {
            var rootDir = mainScriptFile.Directory;
            Debug.Assert(rootDir != null);

            var settings = new V8Settings
            {
                EnableDebugging =
                    inspect || pauseDebuggerOnStart
                            || inspectLoadSet.Any(),
                AwaitDebuggerAndPauseOnStart =
                    pauseDebuggerOnStart || inspectLoadSet.Any(),
            };

            using (var engine = new V8JsEngine(settings))
            {
                var scheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
                var console = ConsoleService.Default;

                void Load(string module)
                {
                    var path = Path.Combine(rootDir.FullName, module);
                    var source = File.ReadAllText(path);
                    if (inspectLoadSet.Contains(source))
                        source = "debugger;" + source;
                    engine.Execute(source, module);
                }

                using (var host = new Host(Load, console, scheduler))
                {
                    string FormatMessage(object sender, string message)
                    {
                        var senderName = sender is string s ? s : sender.GetType().Name;
                        var formatted = $"{senderName}[{Thread.CurrentThread.ManagedThreadId}]: {message}";
                        return formatted.FormatFoldedLines().TrimNewLineAtTail();
                    }

                    void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args) =>
                        console.Error(FormatMessage(sender, args.Exception.ToString()));

                    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                    var infoLog = !verbose ? null : new LogEventHandler((sender, message) =>
                        host.Console.Info(FormatMessage(sender, message)));

                    var warnLog = !verbose ? null : new ErrorLogEventHandler((sender, e, message) =>
                        host.Console.Warn(FormatMessage(sender, e == null ? message : string.IsNullOrEmpty(message) ? e.ToString() : message + Environment.NewLine + e)));

                    var errorLog = !verbose ? null : new ErrorLogEventHandler((sender, e, message) =>
                        host.Console.Error(FormatMessage(sender, string.IsNullOrEmpty(message) ? e.ToString() : message + Environment.NewLine + e)));

                    foreach (var service in new ILogSource[] { host, host.Timer, host.Xhr })
                    {
                        service.InfoLog  = infoLog;
                        service.WarnLog  = warnLog;
                        service.ErrorLog = errorLog;
                    }

                    var tasks = new List<NamedTask>();

                    void AddTask(NamedTask task)    { lock (tasks) tasks.Add(task);    }
                    void RemoveTask(NamedTask task) { lock (tasks) tasks.Remove(task); }

                    host.TaskStarting += (_, task) => AddTask(task);
                    host.TaskFinishing += (_, task) => RemoveTask(task);

                    if (verbose)
                    {
                        host.ServiceCreated += (_, service) =>
                        {
                            service.InfoLog  = infoLog;
                            service.WarnLog  = warnLog;
                            service.ErrorLog = errorLog;
                        };
                    }

                    engine.EmbedHostObject("host", host);
                    var initScript = GetManifestResourceStream("init.js", typeof(Program)).ReadAsText();
                    dynamic init = engine.Evaluate(initScript, "__init.js");
                    init(host, engine.Evaluate("this"), argv);

                    if (settings.AwaitDebuggerAndPauseOnStart)
                        console.Warn(FormatMessage(nameof(Program), "Will wait for debugger to attach."));

                    Load(mainScriptFile.Name);

                    void Schedule(string name, Action<AsyncTaskControl> action)
                    {
                        var task = AsyncTask.Create(name, thisTask =>
                        {
                            Exception error = null;
                            try
                            {
                                action(thisTask);
                            }
                            catch (Exception e)
                            {
                                error = e;
                            }
                            RemoveTask(thisTask);
                            switch (error)
                            {
                                case null:
                                    thisTask.FlagSuccess();
                                    break;
                                case OperationCanceledException e:
                                    thisTask.FlagCanceled(e.CancellationToken);
                                    break;
                                default:
                                    errorLog?.Invoke(nameof(Program), error, null);
                                    thisTask.FlagError(error);
                                    break;
                            }
                        });
                        AddTask(task);
                        task.Start(scheduler);
                    }

                    while (commands.MoveNext())
                    {
                        var command = commands.Current?.Trim();
                        if (string.IsNullOrEmpty(command))
                            continue;

                        const string ondata = "ondata";
                        Schedule(ondata, delegate
                        {
                            infoLog?.Invoke(nameof(Program), "STDIN: " + command);
                            engine.CallFunction(ondata, command);
                        });
                    }

                    const string onclose = "onclose";
                    Schedule(onclose, delegate
                    {
                        engine.Execute(@"if (typeof onclose === 'function') onclose();");
                    });

                    host.Timer.CancelAll();
                    host.Xhr.AbortAll();

                    infoLog?.Invoke(typeof(Program), "Shutting down...");

                    ImmutableArray<Task> tasksSnapshot;

                    lock (tasks)
                        tasksSnapshot = ImmutableArray.CreateRange(from t in tasks select t.Task);

                    if (await tasksSnapshot.WhenAll(TimeSpan.FromSeconds(30)))
                        warnLog?.Invoke(typeof(Program), null, "Timed-out waiting for all tasks to end for a graceful shutdown!");
                    else
                        Debug.Assert(tasks.Count == 0);

                    infoLog?.Invoke(typeof(Program), "Shutdown completed.");

                    TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                }
            }
        }

        interface IStreamSource
        {
            Stream Open();
        }

        sealed class StreamSource : IStreamSource
        {
            readonly Func<Stream> _opener;

            public StreamSource(Func<Stream> opener) =>
                _opener = opener ?? throw new ArgumentNullException(nameof(opener));

            public Stream Open() => _opener();
        }

        static string ReadAsText(this IStreamSource source, Encoding encoding = null)
        {
            using (var stream = source.Open())
            using (var reader = encoding == null ? new StreamReader(stream)
                                                 : new StreamReader(stream, encoding))
            {
                return reader.ReadToEnd();
            }
        }

        static IStreamSource GetManifestResourceStream(string name, Type type = null) =>
            new StreamSource(() => type != null ? type.Assembly.GetManifestResourceStream(type, name)
                               : Assembly.GetCallingAssembly().GetManifestResourceStream(name));

        static async Task<int> Main(string[] args)
        {
            try
            {
                await Wain(args);
                return 0;
            }
            catch (Exception e)
            {
                SysConsole.Error.WriteLine(_verbose ? e.ToString()
                                                    : e.GetBaseException().Message);
                return 0xbad;
            }
        }
    }
}
