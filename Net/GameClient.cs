using Ow.Game;
using Ow.Managers;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Net
{
    class GameClient
    {
        private sealed class PendingSend
        {
            public byte[] Data { get; }
            public int Offset { get; set; }

            public PendingSend(byte[] data)
            {
                Data = data;
            }
        }

        private readonly object sendLock = new object();
        private readonly Queue<PendingSend> sendQueue = new Queue<PendingSend>();
        private bool sendInProgress;
        private bool closeAfterSend;
        private readonly ClientPacketBuffer receiveBuffer = new ClientPacketBuffer();
        private int closing;
        private readonly System.Threading.Timer diagnosticsTimer;
        private long handlerStarted;
        private int activeCommand = -1;
        private long sendStarted;
        private long lastDiagnostic;

        public Socket Socket { get; set; }
        public int UserId { get; set; }

        public GameClient(Socket handler)
        {
            Socket = handler;
            diagnosticsTimer = new System.Threading.Timer(CheckProgress, null, 5000, 5000);

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        private void OnConnectionClosed()
        {
            var gameSession = GameManager.GetGameSession(UserId);
            if (gameSession != null && ReferenceEquals(gameSession.Client, this))
                gameSession.Disconnect(GameSession.DisconnectionType.SOCKET_CLOSED);
        }

        // A normal logout must deliver the final confirmation before closing TCP.
        public void CloseAfterSending()
        {
            bool pending;
            lock (sendLock) { closeAfterSend = true; pending = sendInProgress || sendQueue.Count != 0; }
            if (!pending) Task.Run((Action)Close);
            else Task.Delay(2000).ContinueWith(_ => Close());
        }

        public void Close()
        {
            if (System.Threading.Interlocked.Exchange(ref closing, 1) != 0) return;
            diagnosticsTimer.Dispose();
            try
            {
                lock (sendLock)
                {
                    sendQueue.Clear();
                    sendInProgress = false;
                }

                try { Socket?.Shutdown(SocketShutdown.Both); }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
            catch (Exception e)
            {
                Logger.Log("error_log", "[GameClient] Close failed: " + e.GetType().Name);
            }
            finally
            {
                try { Socket?.Close(); }
                finally { OnConnectionClosed(); }
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            try
            {
                if (Socket == null || !Socket.Connected) return;

                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                int bytesRead = handler.EndReceive(ar);

                byte[] bytes = new byte[bytesRead];
                Buffer.BlockCopy(state.buffer, 0, bytes, 0, bytesRead);

                if (bytesRead > 0)
                {
                    receiveBuffer.Feed(bytes, bytesRead, ExecutePacket, () =>
                    {
                        const string policyPacket = "<?xml version=\"1.0\"?>\r\n" +
                           "<!DOCTYPE cross-domain-policy SYSTEM \"/xml/dtds/cross-domain-policy.dtd\">\r\n" +
                           "<cross-domain-policy>\r\n" +
                           "<allow-access-from domain=\"*\" to-ports=\"*\" />\r\n" +
                           "</cross-domain-policy>";

                        Send(policyPacket + (char)0x00);
                    });
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                }
                else
                {
                    Close();
                }
            } 
            catch (Exception error)
            {
                Logger.Log("error_log", "[GameClient] Receive failed: " + error.GetType().Name + ": " + error.Message);
                Close();
            }
        }

        public void Send(String data)
        {
            try
            {
                byte[] byteData = Encoding.UTF8.GetBytes(data);
                Send(byteData);
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [GameClient.cs] Send(string) void exception: {e}");
            }
        }

        public void Send(byte[] byteData)
        {
            try
            {
                if (byteData == null || byteData.Length == 0 || Socket == null || !Socket.Connected)
                    return;

                PendingSend pendingSend;
                lock (sendLock)
                {
                    if (System.Threading.Volatile.Read(ref closing) != 0) return;
                    sendQueue.Enqueue(new PendingSend(byteData));
                    if (sendInProgress)
                        return;

                    sendInProgress = true;
                    pendingSend = sendQueue.Peek();
                }

                BeginSend(pendingSend);
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [GameClient.cs] Send(byte[]) void exception: {e}");
                Close();
            }
        }

        private void BeginSend(PendingSend pendingSend)
        {
            try
            {
                if (Socket == null || !Socket.Connected)
                {
                    lock (sendLock)
                    {
                        sendQueue.Clear();
                        sendInProgress = false;
                    }
                    Close();
                    return;
                }

                System.Threading.Interlocked.Exchange(ref sendStarted, Stopwatch.GetTimestamp());
                Socket.BeginSend(pendingSend.Data, pendingSend.Offset,
                    pendingSend.Data.Length - pendingSend.Offset, 0,
                    new AsyncCallback(SendCallback), pendingSend);
            }
            catch (Exception e)
            {
                lock (sendLock)
                {
                    sendQueue.Clear();
                    sendInProgress = false;
                }

                Logger.Log("error_log", $"- [GameClient.cs] BeginSend void exception: {e}");
                Close();
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            PendingSend pendingSend = (PendingSend)ar.AsyncState;
            PendingSend nextSend = null;

            try
            {
                var sent = Socket.EndSend(ar);
                if (sent <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                lock (sendLock)
                {
                    if (System.Threading.Volatile.Read(ref closing) != 0) return;
                    pendingSend.Offset += sent;
                    if (pendingSend.Offset >= pendingSend.Data.Length)
                        sendQueue.Dequeue();

                    if (sendQueue.Count == 0)
                    {
                        sendInProgress = false;
                        System.Threading.Interlocked.Exchange(ref sendStarted, 0);
                        if (closeAfterSend) Task.Run((Action)Close);
                        return;
                    }

                    nextSend = sendQueue.Peek();
                }

                BeginSend(nextSend);
            }
            catch (Exception e)
            {
                lock (sendLock)
                {
                    sendQueue.Clear();
                    sendInProgress = false;
                }

                Logger.Log("error_log", $"- [GameClient.cs] SendCallback void exception: {e}");
                Close();
            }
        }

        private void ExecutePacket(byte[] frame)
        {
            activeCommand = (frame[2] << 8) | frame[3];
            System.Threading.Interlocked.Exchange(ref handlerStarted, Stopwatch.GetTimestamp());
            try { Handler.Execute(frame, this); }
            finally { System.Threading.Interlocked.Exchange(ref handlerStarted, 0); }
        }

        // Runs independently of packet handlers, so a blocked handler is recorded
        // while it is blocked, rather than only after it eventually returns.
        private void CheckProgress(object state)
        {
            if (System.Threading.Volatile.Read(ref closing) != 0) return;
            var now = Stopwatch.GetTimestamp();
            var handling = System.Threading.Interlocked.Read(ref handlerStarted);
            var sending = System.Threading.Interlocked.Read(ref sendStarted);
            var handlerMs = handling == 0 ? 0 : (now - handling) * 1000 / Stopwatch.Frequency;
            var sendMs = sending == 0 ? 0 : (now - sending) * 1000 / Stopwatch.Frequency;
            if (handlerMs < 5000 && sendMs < 5000) return;
            var previous = System.Threading.Interlocked.Read(ref lastDiagnostic);
            if (previous != 0 && (now - previous) / Stopwatch.Frequency < 15) return;
            System.Threading.Interlocked.Exchange(ref lastDiagnostic, now);
            int queued;
            lock (sendLock) queued = sendQueue.Count;
            Logger.Log("network_log", $"[GameClient] user={UserId} command={activeCommand} handlerMs={handlerMs} sendMs={sendMs} queued={queued} tick={Ow.Game.Ticks.TickManager.CurrentTickId}");
        }
    }
}
