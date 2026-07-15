using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace AIGameBuilder
{
    public class ClaudeCodeProcess
    {
        private readonly string _repoRoot;
        private readonly Queue<StreamEvent> _events = new Queue<StreamEvent>();
        private readonly object _lock = new object();
        private Process _process;

        public ClaudeCodeProcess(string repoRoot) { _repoRoot = repoRoot; }

        public bool IsRunning { get { return _process != null && !_process.HasExited; } }

        public void Send(string prompt, bool continueConversation)
        {
            if (IsRunning) Cancel();
            var mcpConfig = System.IO.Path.Combine(_repoRoot, ".mcp.json");
            var psi = new ProcessStartInfo
            {
                FileName = "claude",
                WorkingDirectory = _repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add(prompt);
            psi.ArgumentList.Add("--output-format"); psi.ArgumentList.Add("stream-json");
            psi.ArgumentList.Add("--verbose");
            psi.ArgumentList.Add("--mcp-config"); psi.ArgumentList.Add(mcpConfig);
            psi.ArgumentList.Add("--strict-mcp-config");
            psi.ArgumentList.Add("--permission-mode"); psi.ArgumentList.Add("bypassPermissions");
            if (continueConversation)
            {
                psi.ArgumentList.Add("--continue");
            }

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                var parsedList = StreamJsonParser.ParseLine(e.Data);
                lock (_lock) { foreach (var ev in parsedList) _events.Enqueue(ev); }
            };
            _process.Start();
            _process.BeginOutputReadLine();
            // Drain stderr on a background thread so it never blocks.
            new Thread(() => { try { _process.StandardError.ReadToEnd(); } catch { } })
                { IsBackground = true }.Start();
        }

        public Queue<StreamEvent> DrainEvents()
        {
            var drained = new Queue<StreamEvent>();
            lock (_lock)
            {
                while (_events.Count > 0) drained.Enqueue(_events.Dequeue());
            }
            return drained;
        }

        public void Cancel()
        {
            try { if (IsRunning) _process.Kill(); } catch { }
        }
    }
}
