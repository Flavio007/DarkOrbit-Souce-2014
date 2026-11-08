using MySQLManager.Database.Session_Details.Interfaces;
using Newtonsoft.Json;
using Ow.Chat;
using Ow.Game;
using Ow.Game.Objects.AI;
using Ow.Game.Ticks;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Net;
using Ow.Net.netty;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ow
{
    class Program
    {
        public static TickManager TickManager = new TickManager();
        public static bool DebugEquippedItems = false;
        private static Timer saveTimer;
        private static readonly object saveSync = new object();
        public static void SavePlayers()
        {
            lock (saveSync)
                foreach (var session in GameManager.GameSessions.Values)
                    session?.SaveProgress();
        }
        private static void PeriodicSave(object state)
        {
            // A single worker: slow database requests never accumulate timer jobs.
            if (!Monitor.TryEnter(saveSync)) return;
            try { SavePlayers(); }
            catch (Exception e) { Logger.Log("error_log", "[Autosave] " + e); }
            finally { Monitor.Exit(saveSync); }
        }

        public static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                CheckMySQLConnection();
                LoadDatabase();
                AppDomain.CurrentDomain.ProcessExit += (sender, e) => SavePlayers();
                Console.CancelKeyPress += (sender, e) => { e.Cancel = true; SavePlayers(); Environment.Exit(0); };
                InitiateServer();
                saveTimer = new Timer(PeriodicSave, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
                KeepAlive();
            } 
            catch(Exception e)
            {
                Out.WriteLine("Main void exception: " + e, "Program.cs");
                Logger.Log("error_log", $"- [Program.cs] Main void exception: {e}");
            }
        }

        private static void KeepAlive()
        {
            while (true)
            {
                var l = Console.ReadLine();
                if (l == null) { Thread.Sleep(1000); continue; }
                if (l != "" && l.StartsWith("/"))
                    ExecuteCommand(l);
            }
        }

        public static bool CheckMySQLConnection()
        {
            if (!SqlDatabaseManager.Initialized)
            {
                int tries = 0;
                TRY:
                try
                {
                    SqlDatabaseManager.Initialize();
                    Out.WriteLine("DarkOrbit is started..", "EMU");
                    return true;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("MYSQL Connection failed: " + e);
                    Out.WriteLine("MYSQL Connection failed!");
                    if (tries < 6)
                    {
                        Out.WriteLine("Trying to reconnect in .. " + tries + " seconds.");
                        Thread.Sleep(tries * 1000);
                        tries++;
                        goto TRY;
                    } else Environment.Exit(0);
                }
            }
            return false;
        }

        public static void LoadDatabase()
        {
            QueryManager.LoadClans();
            QueryManager.LoadShips();
            QueryManager.LoadMaps();
        }

        public static void InitiateServer()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            Handler.AddCommands();
            Room.AddRooms();
            EventManager.InitiateEvents();
            //AIShips.LoadConfiguredFakeShips();
            StartListening();
        }

        public static void StartListening()
        {
            Task.Factory.StartNew(GameServer.StartListening);
            Out.WriteLine("Listening on port " + GameServer.Port + ".", "GameServer");
            Task.Factory.StartNew(ChatServer.StartListening);
            Out.WriteLine("Listening on port " + ChatServer.Port + ".", "ChatServer");
            Task.Factory.StartNew(SocketServer.StartListening);
            Out.WriteLine("Listening on port " + SocketServer.Port + ".", "SocketServer");

            WebSocketGateway.Start();
            Task.Factory.StartNew(TickManager.Tick);
        }

        public static void ExecuteCommand(string txt)
        {
            var packet = txt.Replace("/", "");
            var splitted = packet.Split(' ');
        
            switch (splitted[0])
            {
                case "save":
                    SavePlayers();
                    break;
                case "stop":
                    SavePlayers();
                    Environment.Exit(0);
                    break;
                case "restart":
                    GameManager.Restart(Convert.ToInt32(splitted[1]));
                    break;
                case "list_players":
                    foreach (var gameSession in GameManager.GameSessions.Values)
                    {
                        if (gameSession != null)
                            Out.WriteLine($"{gameSession.Player.Name} ({gameSession.Player.Id})");
                    }
                    break;
            }        
        }
    }
}
