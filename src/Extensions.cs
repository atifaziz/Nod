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
    using System.Text;

    static class Extensions
    {
        public static string Bracket(this string s, string left, string right) =>
            left + s + right;

        public static T If<T>(this T obj, bool condition) where T : class =>
            condition ? obj : null;

        public static IEnumerator<string> ReadLines(this TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            return _(); IEnumerator<string> _()
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    yield return line;
            }
        }

        public static string TrimNewLineAtTail(this string str)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));

            var length = str.Length;
            if (length > 0)
            {
                var lastIndex = length - 1;
                var endChar = str[lastIndex];
                var cut
                    = endChar == '\r' ? 1
                    : endChar == '\n' ? length > 1 && str[lastIndex - 1] == '\r' ? 2 : 1
                    : 0;
                if (cut > 0)
                    return str.Substring(0, length - cut);
            }

            return str;
        }

        public static string FormatFoldedLines(this string str)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                return str;

            var sb = new StringBuilder();
            str.FormatFoldedLinesTo(sb);
            return sb.ToString();
        }

        public static void FormatFoldedLinesTo(this string str, StringBuilder output)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            if (output == null) throw new ArgumentNullException(nameof(output));

            if (str.Length == 0)
                return;

            using (var reader = new StringReader(str))
            {
                var i = 0;
                for (var line = reader.ReadLine();
                     line != null;
                     line = reader.ReadLine(), i++)
                {
                    if (i > 0)
                        output.Append("  ");
                    output.AppendLine(line);
                }
            }
        }
    }
}
