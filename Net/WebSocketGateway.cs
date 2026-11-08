using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Ow.Utils;

namespace Ow.Net
{
    // Owns the WASM bridge for the lifetime of this server. An already running
    // bridge is reused; it is never terminated by this instance.
    internal static class WebSocketGateway
    {
        private static readonly object Sync = new object();
        private static Process process;
        private static int started;
        private static volatile bool stopping;

        public static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => Stop();
            Task.Run(async () =>
            {
                while (!stopping)
                {
                    try { EnsureRunning(); }
                    catch (Exception error) { Out.WriteLine("Gateway startup failed: " + error.Message, "WebSocket"); }
                    await Task.Delay(3000);
                }
            });
        }

        private static void EnsureRunning()
        {
            lock (Sync)
            {
                if (stopping || (process != null && !process.HasExited)) return;
                if (process != null) { process.Dispose(); process = null; }
                var port = int.Parse(Environment.GetEnvironmentVariable("DO_WS_PORT") ?? "8082");
                if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == port)) return;

                var script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "gateway", "gateway.js");
                if (!File.Exists(script)) throw new FileNotFoundException("Gateway script missing", script);
                var node = Environment.GetEnvironmentVariable("DO_NODE_PATH");
                if (string.IsNullOrEmpty(node))
                {
                    node = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
                    if (!File.Exists(node)) node = "node.exe";
                }
                var start = new ProcessStartInfo(node, "\"" + script + "\"")
                {
                    WorkingDirectory = Path.GetDirectoryName(script),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                start.EnvironmentVariables["DO_PARENT_PID"] = Process.GetCurrentProcess().Id.ToString();
                if (string.IsNullOrEmpty(start.EnvironmentVariables["DO_TCP_PORT"]))
                    start.EnvironmentVariables["DO_TCP_PORT"] = GameServer.Port.ToString();
                if (string.IsNullOrEmpty(start.EnvironmentVariables["DO_CHAT_PORT"]))
                    start.EnvironmentVariables["DO_CHAT_PORT"] = ChatServer.Port.ToString();
                var child = new Process { StartInfo = start };
                child.OutputDataReceived += (sender, args) => { if (args.Data != null) Out.WriteLine(args.Data, "WebSocket"); };
                child.ErrorDataReceived += (sender, args) => { if (args.Data != null) Out.WriteLine(args.Data, "WebSocket"); };
                try
                {
                    if (!child.Start()) throw new InvalidOperationException("Could not start Node.js gateway.");
                }
                catch { child.Dispose(); throw; }
                process = child;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
        }

        public static void Stop()
        {
            lock (Sync)
            {
                stopping = true;
                if (process == null) return;
                try { if (!process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); process = null; }
            }
        }
    }
}
