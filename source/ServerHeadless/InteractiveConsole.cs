using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ServerHeadless
{
    /// <summary>
    /// Pinned-prompt console frontend: log lines scroll above a persistent <c>"&gt; "</c> input
    /// line that tick output never tears up (the classic game-server console layout).
    ///
    /// All console output is rerouted (<see cref="Console.SetOut"/>/<see cref="Console.SetError"/>)
    /// through a synchronized line writer that erases the input line, writes the log line, and
    /// repaints the prompt — one choke point that keeps every writer in the process coordinated
    /// (tick log, Coop log callback, HeadlessDebugManager, game code) without touching any of them.
    /// A dedicated key thread implements line editing, history (up/down) and command-name tab
    /// completion; submitted lines go to the callback supplied by <see cref="HeadlessConsole"/>.
    ///
    /// Requires a real console on both ends: <see cref="TryStart"/> refuses when stdin or stdout is
    /// redirected (piped input, docker logs, CI) and the caller falls back to plain line reading.
    /// </summary>
    internal static class InteractiveConsole
    {
        private const string Prompt = "> ";
        private const int MaxHistory = 200;
        private const int MaxCompletionsShown = 30;

        /// <summary>
        /// Guards the editor state and ALL console painting. Log flushes triggered while already
        /// holding it (e.g. tab completion printing alternatives) rely on Monitor reentrancy.
        /// </summary>
        private static readonly object Sync = new object();

        /// <summary>The real console stdout — painting target and log sink for wrapped Console.Out.</summary>
        private static TextWriter _stdout;
        private static Action<string> _submit;
        private static Func<IEnumerable<string>> _commandNames;

        private static readonly StringBuilder Buffer = new StringBuilder();
        private static int _cursor;
        /// <summary>First visible buffer index; lines longer than the console scroll horizontally.</summary>
        private static int _windowStart;
        private static int _lastRenderLength;

        private static readonly List<string> History = new List<string>();
        private static int _historyIndex;
        /// <summary>The un-submitted line the operator was typing before recalling history.</summary>
        private static string _draft;

        /// <summary>
        /// Installs the console frontend and starts the key thread. Returns false when there is no
        /// real console to take over (redirected stdin/stdout); nothing is modified in that case.
        /// </summary>
        public static bool TryStart(Action<string> submit, Func<IEnumerable<string>> commandNames)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected) return false;

            _submit = submit;
            _commandNames = commandNames;
            _stdout = Console.Out;

            Console.SetOut(new LineWriter(_stdout));
            if (!Console.IsErrorRedirected)
            {
                Console.SetError(new LineWriter(Console.Error));
            }

            lock (Sync) RepaintInput();

            var thread = new Thread(KeyLoop)
            {
                Name = "InteractiveConsole",
                // Torn down with the process; never keeps a shutdown waiting on a blocked ReadKey.
                IsBackground = true,
            };
            thread.Start();
            return true;
        }

        private static void KeyLoop()
        {
            while (true)
            {
                ConsoleKeyInfo key;
                try
                {
                    key = Console.ReadKey(intercept: true);
                }
                catch (Exception)
                {
                    return; // console detached
                }

                lock (Sync)
                {
                    HandleKey(key);
                    RepaintInput();
                }
            }
        }

        private static void HandleKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    SubmitLine();
                    break;
                case ConsoleKey.Backspace:
                    if (_cursor > 0) Buffer.Remove(--_cursor, 1);
                    break;
                case ConsoleKey.Delete:
                    if (_cursor < Buffer.Length) Buffer.Remove(_cursor, 1);
                    break;
                case ConsoleKey.LeftArrow:
                    if (_cursor > 0) _cursor--;
                    break;
                case ConsoleKey.RightArrow:
                    if (_cursor < Buffer.Length) _cursor++;
                    break;
                case ConsoleKey.Home:
                    _cursor = 0;
                    break;
                case ConsoleKey.End:
                    _cursor = Buffer.Length;
                    break;
                case ConsoleKey.UpArrow:
                    RecallHistory(-1);
                    break;
                case ConsoleKey.DownArrow:
                    RecallHistory(+1);
                    break;
                case ConsoleKey.Tab:
                    CompleteCommand();
                    break;
                case ConsoleKey.Escape:
                    SetBuffer(string.Empty);
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        Buffer.Insert(_cursor++, key.KeyChar);
                    }
                    break;
            }
        }

        private static void SubmitLine()
        {
            string line = Buffer.ToString().Trim();

            // Move the submitted line into the scrollback so there is a record of what ran.
            EraseInput();
            if (line.Length > 0)
            {
                _stdout.WriteLine(Prompt + line);
            }
            SetBuffer(string.Empty);

            if (line.Length == 0) return;

            if (History.Count == 0 || History[History.Count - 1] != line)
            {
                History.Add(line);
                if (History.Count > MaxHistory) History.RemoveAt(0);
            }
            _historyIndex = History.Count;
            _draft = null;

            _submit(line);
        }

        private static void RecallHistory(int direction)
        {
            if (direction < 0)
            {
                if (_historyIndex == 0) return;
                if (_historyIndex == History.Count) _draft = Buffer.ToString();
                _historyIndex--;
                SetBuffer(History[_historyIndex]);
            }
            else
            {
                if (_historyIndex >= History.Count) return;
                _historyIndex++;
                SetBuffer(_historyIndex == History.Count ? _draft ?? string.Empty : History[_historyIndex]);
            }
        }

        private static void CompleteCommand()
        {
            string text = Buffer.ToString();
            // Only complete a command name still being typed: cursor at the end, no arguments yet.
            if (text.Length == 0 || _cursor != Buffer.Length || text.IndexOf(' ') >= 0) return;

            List<string> matches = _commandNames()
                .Where(n => n.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count == 0) return;

            if (matches.Count == 1)
            {
                SetBuffer(matches[0] + " ");
                return;
            }

            string common = CommonPrefix(matches);
            if (common.Length > text.Length)
            {
                SetBuffer(common);
                return;
            }

            // No further progress possible: show the alternatives in the scrollback.
            EraseInput();
            foreach (string name in matches.Take(MaxCompletionsShown))
            {
                _stdout.WriteLine("  " + name);
            }
            if (matches.Count > MaxCompletionsShown)
            {
                _stdout.WriteLine($"  ... and {matches.Count - MaxCompletionsShown} more ('list {text}' shows all)");
            }
        }

        /// <summary>Longest common prefix of the matches, compared case-insensitively.</summary>
        private static string CommonPrefix(List<string> names)
        {
            string first = names[0];
            int length = first.Length;
            foreach (string other in names)
            {
                int i = 0;
                while (i < length && i < other.Length
                       && char.ToLowerInvariant(first[i]) == char.ToLowerInvariant(other[i]))
                {
                    i++;
                }
                length = i;
            }
            return first.Substring(0, length);
        }

        private static void SetBuffer(string value)
        {
            Buffer.Clear();
            Buffer.Append(value);
            _cursor = Buffer.Length;
            _windowStart = 0;
        }

        private static int SafeWidth()
        {
            try
            {
                return Math.Max(Console.BufferWidth, 20);
            }
            catch (Exception)
            {
                return 120;
            }
        }

        /// <summary>Blanks the input row so a log line can take its place. Caller holds Sync.</summary>
        private static void EraseInput()
        {
            int erase = Math.Min(_lastRenderLength, SafeWidth() - 1);
            _stdout.Write('\r');
            if (erase > 0) _stdout.Write(new string(' ', erase));
            _stdout.Write('\r');
            _lastRenderLength = 0;
        }

        /// <summary>Redraws prompt + visible buffer slice on the current row. Caller holds Sync.</summary>
        private static void RepaintInput()
        {
            int width = SafeWidth();
            // Never write into the last column: that triggers the console's auto-wrap.
            int available = Math.Max(1, width - Prompt.Length - 1);

            // Slide the window so the cursor stays visible.
            if (_cursor < _windowStart) _windowStart = _cursor;
            if (_cursor > _windowStart + available) _windowStart = _cursor - available;
            if (_windowStart > 0 && Buffer.Length - _windowStart < available)
            {
                _windowStart = Math.Max(0, Buffer.Length - available);
            }

            int visibleLength = Math.Min(available, Buffer.Length - _windowStart);
            string rendered = Prompt + Buffer.ToString(_windowStart, visibleLength);

            _stdout.Write('\r');
            _stdout.Write(rendered);
            if (rendered.Length < _lastRenderLength)
            {
                _stdout.Write(new string(' ', _lastRenderLength - rendered.Length));
            }
            _lastRenderLength = rendered.Length;

            try
            {
                Console.SetCursorPosition(Prompt.Length + (_cursor - _windowStart), Console.CursorTop);
            }
            catch (Exception)
            {
                // Console resized/detached mid-paint; the next repaint recovers.
            }
        }

        /// <summary>
        /// Line-buffering writer installed over Console.Out/Error. Complete lines are written to the
        /// wrapped target with the input line erased first and repainted after; carriage returns and
        /// partial writes are held until their newline arrives.
        /// </summary>
        private sealed class LineWriter : TextWriter
        {
            private readonly TextWriter _target;
            private readonly StringBuilder _pending = new StringBuilder();

            public LineWriter(TextWriter target)
            {
                _target = target;
            }

            public override Encoding Encoding => _target.Encoding;

            public override void Write(char value)
            {
                lock (Sync) Append(value);
            }

            public override void Write(string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                lock (Sync)
                {
                    foreach (char c in value) Append(c);
                }
            }

            public override void Flush()
            {
                lock (Sync)
                {
                    // A held partial line is better surfaced than invisible.
                    if (_pending.Length > 0) FlushPendingLine();
                    _target.Flush();
                }
            }

            private void Append(char c)
            {
                if (c == '\n') FlushPendingLine();
                else if (c != '\r') _pending.Append(c);
            }

            private void FlushPendingLine()
            {
                EraseInput();
                _target.WriteLine(_pending.ToString());
                _pending.Clear();
                RepaintInput();
            }
        }
    }
}
