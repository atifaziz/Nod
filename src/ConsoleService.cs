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
    using static System.Console;
    using static System.ConsoleColor;
    using SysConsole = System.Console;

    public class ConsoleService : LogSource
    {
        public static readonly ConsoleService Default = new ConsoleService();

        ConsoleService() {}

        public void Puts (string s)       { lock (typeof(SysConsole)) WriteLine(s); }
        public void Log  (string message) => Write(message);
        public void Warn (string message) => Write(message, Yellow);
        public void Error(string message) => Write(message, Red);
        public void Info (string message) => Write(message, Cyan);

        static void Write(string message, ConsoleColor? fg = null, ConsoleColor? bg = null)
        {
            lock (typeof(SysConsole)) // lock over color changes
            {
                ConsoleColor? oldForeground = null;
                ConsoleColor? oldBackground = null;

                if (fg is ConsoleColor fgc)
                    (oldForeground, ForegroundColor) = (ForegroundColor, fgc);

                if (bg is ConsoleColor bgc)
                    (oldBackground, BackgroundColor) = (BackgroundColor, bgc);

                SysConsole.Error.WriteLine(message);

                if (oldForeground is ConsoleColor ofgc)
                    ForegroundColor = ofgc;

                if (oldBackground is ConsoleColor obgc)
                    BackgroundColor = obgc;
            }
        }
    }
}
