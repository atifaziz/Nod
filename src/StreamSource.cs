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
    using System.IO;
    using System.Text;

    interface IStreamSource
    {
        Stream Open();
    }

    static class StreamSource
    {
        public static IStreamSource Create(Func<Stream> opener) =>
            new DelegatingStreamSource(opener);

        sealed class DelegatingStreamSource : IStreamSource
        {
            readonly Func<Stream> _opener;

            public DelegatingStreamSource(Func<Stream> opener) =>
                _opener = opener ?? throw new ArgumentNullException(nameof(opener));

            public Stream Open() => _opener();
        }

        public static string ReadAsText(this IStreamSource source, Encoding encoding = null)
        {
            using (var stream = source.Open())
            using (var reader = encoding == null ? new StreamReader(stream)
                                                 : new StreamReader(stream, encoding))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
