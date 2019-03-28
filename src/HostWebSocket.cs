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
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;

    public class HostWebSocket : HostService
    {
        readonly int _id;
        readonly ClientWebSocket _socket;
        readonly CancellationTokenSource _cancellationTokenSource;

        public HostWebSocket(int id, string url, IEnumerable<string> protocols, dynamic callback,
                             ScheduleTaskHandler scheduler) :
            base(scheduler)
        {
            _id = id;
            _cancellationTokenSource = new CancellationTokenSource();

            _socket = new ClientWebSocket();
            foreach (var protocol in protocols)
                _socket.Options.AddSubProtocol(protocol);
            //_socket.Options.UseDefaultCredentials = true;

            MemoryStream ms = null;
            var buffer = new byte[4096];

            Schedule($"{nameof(HostWebSocket)}[{id}]", _cancellationTokenSource.Token,
                async cancellationToken =>
                {
                    try
                    {
                        await _socket.ConnectAsync(new Uri(url), cancellationToken);
                    }
                    catch (OperationCanceledException) { /* ignore */}
                    catch (WebSocketException e)
                    {
                        callback("error", e.GetBaseException().Message);
                        return;
                    }

                    callback("open", null);

                    while (true)
                    {
                        var receiveTask = _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        var result = await receiveTask;
                        ms = ms ?? new MemoryStream();
                        await ms.WriteAsync(buffer, 0, result.Count, cancellationToken);
                        if (result.EndOfMessage)
                        {
                            var data = Encoding.UTF8.GetString(ms.ToArray());
                            ms = null;
                            try
                            {
                                callback("receive", data);
                            }
                            catch (Exception e)
                            {
                                ErrorLog?.Invoke(this, e, null);
                            }
                        }
                    }
                    // ReSharper disable once FunctionNeverReturns
                });
        }

        public void Send(string data)
        {
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data));
            _socket.SendAsync(buffer, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }
    }
}
