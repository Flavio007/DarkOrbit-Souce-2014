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

        public Socket Socket { get; set; }
        public int UserId { get; set; }

        public GameClient(Socket handler)
        {
            Socket = handler;

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        private void OnConnectionClosed()
        {
            var gameSession = GameManager.GetGameSession(UserId);
            if (gameSession != null)
                gameSession.Disconnect(GameSession.DisconnectionType.SOCKET_CLOSED);
        }

        public void Close()
        {
            try
            {
                lock (sendLock)
                {
                    sendQueue.Clear();
                    sendInProgress = false;
                }

                Socket.Shutdown(SocketShutdown.Both);
                Socket.Close();

                OnConnectionClosed();
            }
            catch (Exception e)
            {
                //ignored
                //Logger.Log("error_log", $"- [GameClient.cs] Close void exception: {e}");
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            try
            {
                if (Socket == null || !Socket.IsBound || !Socket.Connected) return;

                String content = String.Empty;

                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                int bytesRead = handler.EndReceive(ar);

                byte[] bytes = new byte[bytesRead];
                Buffer.BlockCopy(state.buffer, 0, bytes, 0, bytesRead);

                if (bytesRead > 0)
                {
                    content = Encoding.UTF8.GetString(
                        state.buffer, 0, bytesRead);

                    if (content.StartsWith("<policy-file-request/>"))
                    {
                        const string policyPacket = "<?xml version=\"1.0\"?>\r\n" +
                           "<!DOCTYPE cross-domain-policy SYSTEM \"/xml/dtds/cross-domain-policy.dtd\">\r\n" +
                           "<cross-domain-policy>\r\n" +
                           "<allow-access-from domain=\"*\" to-ports=\"*\" />\r\n" +
                           "</cross-domain-policy>";

                        Send(policyPacket + (char)0x00);
                    }
                    else
                    {
                        Handler.Execute(bytes, this);

                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    }
                }
                else
                {
                    Close();
                }
            } 
            catch
            {
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
                if (byteData == null || byteData.Length == 0 || Socket == null || !Socket.IsBound || !Socket.Connected)
                    return;

                PendingSend pendingSend;
                lock (sendLock)
                {
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
            }
        }

        private void BeginSend(PendingSend pendingSend)
        {
            try
            {
                if (Socket == null || !Socket.IsBound || !Socket.Connected)
                {
                    lock (sendLock)
                    {
                        sendQueue.Clear();
                        sendInProgress = false;
                    }
                    return;
                }

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
                    pendingSend.Offset += sent;
                    if (pendingSend.Offset >= pendingSend.Data.Length)
                        sendQueue.Dequeue();

                    if (sendQueue.Count == 0)
                    {
                        sendInProgress = false;
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
            }
        }
    }
}
