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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;

    public sealed class XhrService : HostService, IDisposable
    {
        HttpClient _client;
        readonly Dictionary<int, CancellationTokenSource> _cancellationTokenSources = new Dictionary<int, CancellationTokenSource>();

        public XhrService(ScheduleTaskHandler scheduler) :
            base(scheduler) {}

        HttpClient HttpClient =>
            _client ?? (_client = new HttpClient(new HttpClientHandler
            {
                UseDefaultCredentials = true
            }));

        public void Dispose() =>
            _client?.Dispose();

        internal void AbortAll()
        {
            // Materialize before looping to prevent InvalidOperationException:
            // "Collection was modified; enumeration operation may not execute."

            foreach (var e in _cancellationTokenSources.ToArray())
            {
                InfoLog?.Invoke(this, $"{nameof(AbortAll)}(id = {e.Key})");
                e.Value.Cancel();
            }
        }

        public void Abort(int id)
        {
            InfoLog?.Invoke(this, $"{nameof(Abort)}(id = {id})");

            if (!_cancellationTokenSources.TryGetValue(id, out var s))
                return;

            _cancellationTokenSources.Remove(id);
            s.Cancel();
        }

        public void Send(int id, string method, string url, bool async, object[] headers, object data, dynamic callback)
        {
            InfoLog?.Invoke(this, $"{nameof(Send)}(method = {method}, url = {url}, async = {async}) -> [{id}]");

            var httpMethod
                = "GET" .Equals(method, StringComparison.OrdinalIgnoreCase) ? HttpMethod.Get
                : "POST".Equals(method, StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post
                : throw new NotSupportedException("Unsupported HTTP method: " + method);

            HttpContent content;

            switch (data)
            {
                case null:
                    content = null;
                    break;
                case string dataString:
                    content = new StringContent(dataString);
                    break;
                default:
                    throw new NotSupportedException("Unsupported HTTP method: " + method);
            }

            var request = new HttpRequestMessage(httpMethod, url)
            {
                Content = content
            };

            foreach (object[] kv in headers as IEnumerable)
            {
                var name  = (string) kv[0];
                var value = (string) kv[1];

                var requestHeaders
                    = name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)
                      || name.Equals("Expires", StringComparison.OrdinalIgnoreCase)
                      || name.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase)
                      || name.Equals("Allow", StringComparison.OrdinalIgnoreCase)
                    ? (HttpHeaders)content.Headers
                    : request.Headers;

                requestHeaders.Remove(name);
                requestHeaders.Add(name, value);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSources.Add(id, cancellationTokenSource);

            Schedule(
                $"{nameof(XhrService)}[{id}]", cancellationTokenSource.Token,
                async cancellationToken =>
                {
                    var response = await HttpClient.SendAsync(request, cancellationToken);

                    var statusCode = (int)response.StatusCode;
                    if (statusCode >= 200 && statusCode < 300)
                        InfoLog?.Invoke(this, $"[{id}] {statusCode} {response.ReasonPhrase} ({response.Content.Headers.ContentType})");
                    else
                        WarnLog?.Invoke(this, null, $"[{id}] {statusCode} {response.ReasonPhrase} ({response.Content.Headers.ContentType})");

                    var str = await response.Content.ReadAsStringAsync();

                    callback(null, statusCode, response.ReasonPhrase,
                             new[] { new[] { "Content-Type", response.Content.Headers.ContentType.MediaType } },
                             str);
                },
                onError: e => callback(e.GetBaseException().Message),
                onFinally: () =>
                {
                    _cancellationTokenSources.Remove(id);
                    cancellationTokenSource.Dispose();
                });
        }
    }
}
