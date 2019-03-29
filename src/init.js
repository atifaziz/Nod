(function (host, $, args) {

    function load(module) {
        host.Load(module);
    }

    const argv = [];
    for (let i = 0; i < args.Length; i++)
        argv.push(args[i]);

    const console = (function () {

        /*  Node.js is licensed for use as follows:
         *
         *    Copyright Node.js contributors. All rights reserved.
         *
         *    Permission is hereby granted, free of charge, to any person obtaining a copy
         *    of this software and associated documentation files (the "Software"), to
         *    deal in the Software without restriction, including without limitation the
         *    rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
         *    sell copies of the Software, and to permit persons to whom the Software is
         *    furnished to do so, subject to the following conditions:
         *
         *    The above copyright notice and this permission notice shall be included in
         *    all copies or substantial portions of the Software.
         *
         *    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
         *    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
         *    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
         *    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
         *    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
         *    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
         *    IN THE SOFTWARE.
         *
         *  This license applies to parts of Node.js originating from the
         *  https://github.com/joyent/node repository:
         *
         *    Copyright Joyent, Inc. and other Node contributors. All rights reserved.
         *    Permission is hereby granted, free of charge, to any person obtaining a copy
         *    of this software and associated documentation files (the "Software"), to
         *    deal in the Software without restriction, including without limitation the
         *    rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
         *    sell copies of the Software, and to permit persons to whom the Software is
         *    furnished to do so, subject to the following conditions:
         *
         *    The above copyright notice and this permission notice shall be included in
         *    all copies or substantial portions of the Software.
         *
         *    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
         *    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
         *    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
         *    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
         *    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
         *    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
         *    IN THE SOFTWARE.
         */

        function inspect() {
            return "[INSPECT NOT IMPLEMENTED]";
        }

        let CIRCULAR_ERROR_MESSAGE;
        function tryStringify(arg) {
            try {
                return JSON.stringify(arg);
            } catch (err) {
                // Populate the circular error message lazily
                if (!CIRCULAR_ERROR_MESSAGE) {
                    try {
                        const a = {}; a.a = a; JSON.stringify(a);
                    } catch (err) {
                        CIRCULAR_ERROR_MESSAGE = err.message;
                    }
                }
                if (err.name === 'TypeError' && err.message === CIRCULAR_ERROR_MESSAGE)
                    return '[Circular]';
                throw err;
            }
        }

        function formatWithOptions(inspectOptions, ...args) {
            const first = args[0];
            let a = 0;
            let str = '';
            let join = '';
            if (typeof first === 'string') {
                if (args.length === 1) {
                    return first;
                }
                let tempStr;
                let lastPos = 0;
                for (var i = 0; i < first.length - 1; i++) {
                    if (first.charCodeAt(i) === 37) { // '%'
                        const nextChar = first.charCodeAt(++i);
                        if (a + 1 !== args.length) {
                            switch (nextChar) {
                                case 115: // 's'
                                    tempStr = String(args[++a]);
                                    break;
                                case 106: // 'j'
                                    tempStr = tryStringify(args[++a]);
                                    break;
                                case 100: // 'd'
                                    const tempNum = args[++a];
                                    // eslint-disable-next-line valid-typeof
                                    if (typeof tempNum === 'bigint') {
                                        tempStr = `${tempNum}n`;
                                    } else if (typeof tempNum === 'symbol') {
                                        tempStr = 'NaN';
                                    } else {
                                        tempStr = `${Number(tempNum)}`;
                                    }
                                    break;
                                case 79: // 'O'
                                    tempStr = inspect(args[++a], inspectOptions);
                                    break;
                                case 111: // 'o'
                                    {
                                        tempStr = inspect(args[++a], {
                                            ...inspectOptions,
                                            showHidden: true,
                                            showProxy: true,
                                            depth: 4
                                        });
                                        break;
                                    }
                                case 105: // 'i'
                                    const tempInteger = args[++a];
                                    // eslint-disable-next-line valid-typeof
                                    if (typeof tempInteger === 'bigint') {
                                        tempStr = `${tempInteger}n`;
                                    } else if (typeof tempInteger === 'symbol') {
                                        tempStr = 'NaN';
                                    } else {
                                        tempStr = `${parseInt(tempInteger)}`;
                                    }
                                    break;
                                case 102: // 'f'
                                    const tempFloat = args[++a];
                                    if (typeof tempFloat === 'symbol') {
                                        tempStr = 'NaN';
                                    } else {
                                        tempStr = `${parseFloat(tempFloat)}`;
                                    }
                                    break;
                                case 37: // '%'
                                    str += first.slice(lastPos, i);
                                    lastPos = i + 1;
                                    continue;
                                default: // Any other character is not a correct placeholder
                                    continue;
                            }
                            if (lastPos !== i - 1) {
                                str += first.slice(lastPos, i - 1);
                            }
                            str += tempStr;
                            lastPos = i + 1;
                        } else if (nextChar === 37) {
                            str += first.slice(lastPos, i);
                            lastPos = i + 1;
                        }
                    }
                }
                if (lastPos !== 0) {
                    a++;
                    join = ' ';
                    if (lastPos < first.length) {
                        str += first.slice(lastPos);
                    }
                }
            }
            while (a < args.length) {
                const value = args[a];
                str += join;
                str += typeof value !== 'string' ? inspect(value, inspectOptions) : value;
                join = ' ';
                a++;
            }
            return str;
        }

        // *** End of code under Node.js LICENSE

        return {
            log: (...args) => host.Console.Log(formatWithOptions(null, ...args)),
            info: (...args) => host.Console.Info(formatWithOptions(null, ...args)),
            warn: (...args) => host.Console.Warn(formatWithOptions(null, ...args)),
            error: (...args) => host.Console.Error(formatWithOptions(null, ...args)),
            puts: s => host.Console.Puts(s && s.toString()),
        };
    })();

    function setTimeout(callback, delay, ...args) {
        return host.Timer.SetTimeout(host.proc(0, () => callback(...args)), delay || 0);
    }

    function clearTimeout(timeoutID) {
        return +timeoutID
            ? host.Timer.ClearTimeout(timeoutID)
            : void 0;
    }

    $.process = { argv };
    $.load = load;
    $.console = console;
    $.setTimeout = setTimeout;
    $.clearTimeout = clearTimeout;
});
