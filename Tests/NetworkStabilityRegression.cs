using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ow.Net;

// Standalone host: compile with GameClient.cs, ClientPacketBuffer.cs and Logger.cs.
namespace Ow.Utils { static class Out { public static void WriteLine(string value, string source) { throw new Exception(value); } } }
namespace Ow.Game { class GameSession { public GameClient Client; public int Disconnections; public enum DisconnectionType { SOCKET_CLOSED } public void Disconnect(DisconnectionType kind) { Disconnections++; } } }
namespace Ow.Game.Ticks { static class TickManager { public static long CurrentTickId = 42; } }
namespace Ow.Managers { static class GameManager { public static Ow.Game.GameSession Session; public static Ow.Game.GameSession GetGameSession(int id) { return Session; } } }
namespace Ow.Net.netty.commands { }
namespace Ow.Net.netty { static class Handler { public static void Execute(byte[] frame, GameClient client) { } } }
namespace Ow.Net { class StateObject { public Socket workSocket; public const int BufferSize = 1024; public byte[] buffer = new byte[BufferSize]; } }
class NetworkStabilityRegression
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static GameClient Connect(out Socket remote)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        remote.Connect(listener.LocalEndpoint);
        var client = new GameClient(listener.AcceptSocket()); listener.Stop(); return client;
    }
    static void Main()
    {
        var log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "regression_" + DateTime.Now.ToString("dd.MM.yyyy") + ".txt");
        Ow.Utils.Logger.Log("regression", "first");
        Parallel.For(0, 100, i => Ow.Utils.Logger.Log("regression", "entry-" + i));
        var lines = File.ReadAllLines(log);
        Check(lines.Length == 101 && lines[0].EndsWith("first"), "Logs must retain history under concurrent appends");
        Socket oldRemote, newRemote;
        var oldClient = Connect(out oldRemote); var newClient = Connect(out newRemote);
        var session = new Ow.Game.GameSession { Client = newClient }; Ow.Managers.GameManager.Session = session;
        oldClient.Close(); Check(session.Disconnections == 0, "Old socket must not disconnect replacement session");
        var expected = Enumerable.Range(0, 200).SelectMany(i => BitConverter.GetBytes(i)).ToArray();
        for (int i = 0; i < 200; i++) newClient.Send(BitConverter.GetBytes(i));
        newRemote.ReceiveTimeout = 3000;
        var actual = new byte[expected.Length]; int received = 0;
        while (received < actual.Length) received += newRemote.Receive(actual, received, actual.Length - received, SocketFlags.None);
        Check(actual.SequenceEqual(expected), "Queued sends must retain byte order");
        typeof(GameClient).GetField("handlerStarted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newClient, System.Diagnostics.Stopwatch.GetTimestamp() - 6 * System.Diagnostics.Stopwatch.Frequency);
        typeof(GameClient).GetMethod("CheckProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(newClient, new object[] { null });
        var diagnostic = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "network_log_" + DateTime.Now.ToString("dd.MM.yyyy") + ".txt");
        Check(File.ReadAllText(diagnostic).Contains("tick=42"), "Blocked handlers must produce diagnostics before returning");
        newClient.Socket.Dispose();
        typeof(GameClient).GetMethod("BeginSend", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(newClient, new object[] { null });
        Check(session.Disconnections == 1, "Send failure must close broken connection");
        newClient.Close(); Check(session.Disconnections == 1, "Close must be idempotent");
        Socket gracefulRemote;var graceful=Connect(out gracefulRemote);
        gracefulRemote.ReceiveTimeout=3000;
        var finalBytes=Enumerable.Range(0,131072).Select(i=>(byte)(i%251)).ToArray();
        graceful.Send(finalBytes);graceful.CloseAfterSending();
        var delivered=new byte[finalBytes.Length];int total=0;
        while(total<delivered.Length){var n=gracefulRemote.Receive(delivered,total,delivered.Length-total,SocketFlags.None);if(n==0)break;total+=n;}
        Check(total==finalBytes.Length&&delivered.SequenceEqual(finalBytes),"Graceful logout must flush queued confirmation bytes");
        Check(gracefulRemote.Receive(new byte[1])==0,"Graceful logout closes after queued bytes");
        gracefulRemote.Dispose();oldRemote.Dispose(); newRemote.Dispose();
        Console.WriteLine("Passed: concurrent log history, session ownership, ordered sends, blocked-handler diagnostics, failed-send close, idempotent close");
    }
}
