using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ow.Chat;
using Ow.Game;
using Ow.Game.Objects;
using Ow.Game.Objects.Players.Managers;
using Ow.Managers;
using Ow.Managers.MySQLManager;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using static Ow.Game.GameSession;

public class StateObject
{
    public Socket workSocket = null;
    public const int BufferSize = 1024;
    public byte[] buffer = new byte[BufferSize];
    public StringBuilder sb = new StringBuilder();
}

class SocketServer
{
    public static ManualResetEvent allDone = new ManualResetEvent(false);
    public static int Port = 4301;

    public static void StartListening()
    {  
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, Port);

        Socket listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                allDone.Reset();

                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                allDone.WaitOne();
            }

        }
        catch (Exception e)
        {
            Logger.Log("error_log", $"- [SocketServer.cs] StartListening void exception: {e}");
        }
    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        try
        {
            allDone.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            Connection(handler);
        } 
        catch (Exception e)
        {
            Logger.Log("error_log", $"- [SocketServer.cs] AcceptCallback void exception: {e}");
        }
    }

    public static void Connection(Socket handler)
    {
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }

    public static void Execute(JObject json, JObject parameters, Socket handler)
    {
        var action = String(json?["Action"]);
        var syncRevision = String(parameters?["SyncRevision"]);

        if (RequiresSyncRevisionValidation(action))
        {
            var syncPlayer = GameManager.GetPlayerById(Int(parameters?["UserId"]));
            if (!IsSyncRevisionValid(syncPlayer, syncRevision))
            {
                var current = syncPlayer?.GameSession != null ? BuildPlayerSyncRevision(syncPlayer) : "offline";
                Logger.Log("sync_log", $"Sync mismatch user={syncPlayer?.Id} action={action} expected={syncRevision} current={current}");
                ForceReloadFromDatabase(syncPlayer);
                return;
            }
        }

        switch (action)
        {
            case "OnlineIds":
                Send(handler, JsonConvert.SerializeObject(GameManager.GameSessions.Keys).ToString());
                break;
            case "OnlineCount":
                Send(handler, GameManager.GameSessions.Count.ToString());
                break;
            case "IsOnline":
                var player = GameManager.GetPlayerById(Int(parameters["UserId"]));
                var online = player?.GameSession != null ? true : false;
                Send(handler, online.ToString());
                break;
            case "IsInEquipZone":
                player = GameManager.GetPlayerById(Int(parameters["UserId"]));
                var inEquipZone = player?.GameSession != null ? player.Storage.IsInEquipZone : false;
                Send(handler, inEquipZone.ToString());
                break;
            case "GetPosition":
                player = GameManager.GetPlayerById(Int(parameters["UserId"]));
                var spacemapName = player?.GameSession != null ? player.Spacemap.Name : "";
                Send(handler, spacemapName.ToString());
                break;
            case "AvailableToChangeShip":
                player = GameManager.GetPlayerById(Int(parameters["UserId"]));
                var available = player?.Storage.lastChangeShipTime.AddSeconds(5) < DateTime.Now ? true : false;
                Send(handler, available.ToString());
                break;
            case "BanUser":
                BanUser(GameManager.GetPlayerById(Int(parameters["UserId"])));
                break;
            case "BuyItem":
                BuyItem(GameManager.GetPlayerById(Int(parameters["UserId"])), String(parameters["ItemType"]), (DataType)Short(parameters["DataType"]), Int(parameters["Amount"]));
                break;
            case "ChangeClanData":
                ChangeClanData(GameManager.GetClan(Int(parameters["ClanId"])), String(parameters["Name"]), String(parameters["Tag"]), Int(parameters["FactionId"]));
                break;
            case "ChangeShip":
                ChangeShip(GameManager.GetPlayerById(Int(parameters["UserId"])), GameManager.GetShip(Int(parameters["ShipId"])));
                break;
            case "ChangeCompany":
                ChangeCompany(GameManager.GetPlayerById(Int(parameters["UserId"])), Int(parameters["UridiumPrice"]), Int(parameters["HonorPrice"]));
                break;
            case "UpdateStatus":
                UpdateStatus(GameManager.GetPlayerById(Int(parameters["UserId"])));
                break;
            case "JoinToClan":
                JoinToClan(GameManager.GetPlayerById(Int(parameters["UserId"])), GameManager.GetClan(Int(parameters["ClanId"])));
                break;
            case "LeaveFromClan":
                LeaveFromClan(GameManager.GetPlayerById(Int(parameters["UserId"])));
                break;
            case "CreateClan":
                CreateClan(GameManager.GetPlayerById(Int(parameters["UserId"])), Int(parameters["ClanId"]), Int(parameters["FactionId"]), String(parameters["Name"]), String(parameters["Tag"]));
                break;
            case "DeleteClan":
                DeleteClan(GameManager.GetClan(Int(parameters["ClanId"])));
                break;
            case "StartDiplomacy":
                StartDiplomacy(GameManager.GetClan(Int(parameters["SenderClanId"])), GameManager.GetClan(Int(parameters["TargetClanId"])), Short(parameters["DiplomacyType"]));
                break;
            case "EndDiplomacy":
                EndDiplomacy(GameManager.GetClan(Int(parameters["SenderClanId"])), GameManager.GetClan(Int(parameters["TargetClanId"])));
                break;
            case "UpgradeSkillTree":
                UpgradeSkillTree(GameManager.GetPlayerById(Int(parameters["UserId"])), String(parameters["Skill"]));
                break;
            case "ResetSkillTree":
                ResetSkillTree(GameManager.GetPlayerById(Int(parameters["UserId"])));
                break;
            case "KickPlayer":
                KickPlayer(GameManager.GetPlayerById(Int(parameters["UserId"])), String(parameters["Reason"]));
                break;
            case "RepairDrone":
                RepairDrone(GameManager.GetPlayerById(Int(parameters["UserId"])), Int(parameters["DroneId"]));
                break;
            case "SellDrone":
                SellDrone(GameManager.GetPlayerById(Int(parameters["UserId"])), Int(parameters["DroneId"]));
                break;
        }
    }

    private static bool RequiresSyncRevisionValidation(string action)
    {
        return action == "BuyItem" ||
               action == "UpdateStatus" ||
               action == "RepairDrone" ||
               action == "SellDrone";
    }

    private static bool IsSyncRevisionValid(Player player, string expected)
    {
        if (player?.GameSession == null)
            return false;

        if (string.IsNullOrWhiteSpace(expected))
            return true; // Backward compatibility for old payloads.

        var current = BuildPlayerSyncRevision(player);
        return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPlayerSyncRevision(Player player)
    {
        if (player == null)
            return string.Empty;

        var config = player.Equipment?.Configs;
        var items = player.Equipment?.Items;
        var drones = player.DroneManager?.DronesList?
            .Where(x => x != null)
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.DroneType}:{x.Level}:{x.Experience}:{x.Damage}")
            .ToList() ?? new List<string>();

        var config1Designs = player.DroneManager?.Config1Designs ?? new List<int>();
        var config2Designs = player.DroneManager?.Config2Designs ?? new List<int>();

        var seed = string.Join("|", new[]
        {
            player.Id.ToString(),
            player.Ship?.Id.ToString() ?? "0",
            player.Data?.credits.ToString() ?? "0",
            player.Data?.uridium.ToString() ?? "0",
            player.Data?.honor.ToString() ?? "0",
            player.Data?.experience.ToString() ?? "0",
            player.Data?.jackpot.ToString() ?? "0",
            config?.Config1Hitpoints.ToString() ?? "0",
            config?.Config1Damage.ToString() ?? "0",
            config?.Config1Shield.ToString() ?? "0",
            config?.Config1Speed.ToString() ?? "0",
            config?.Config2Hitpoints.ToString() ?? "0",
            config?.Config2Damage.ToString() ?? "0",
            config?.Config2Shield.ToString() ?? "0",
            config?.Config2Speed.ToString() ?? "0",
            items?.BootyKeys.ToString() ?? "0",
            string.Join(",", config1Designs),
            string.Join(",", config2Designs),
            (player.DroneManager?.Apis ?? false) ? "1" : "0",
            (player.DroneManager?.Zeus ?? false) ? "1" : "0",
            string.Join(";", drones)
        });

        using (var sha = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(seed);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    private static void ForceReloadFromDatabase(Player player)
    {
        if (player?.GameSession == null)
            return;

        try
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var accountRow = mySqlClient.ExecuteQueryRow($"SELECT data FROM player_accounts WHERE userId = {player.Id}");
                if (accountRow != null && accountRow.Table.Columns.Contains("data") && accountRow["data"] != null)
                    player.Data = JsonConvert.DeserializeObject<DataBase>(accountRow["data"].ToString());

                var equipmentRow = mySqlClient.ExecuteQueryRow($"SELECT boosters FROM player_equipment WHERE userId = {player.Id}");
                if (equipmentRow != null && equipmentRow.Table.Columns.Contains("boosters") && equipmentRow["boosters"] != null)
                {
                    var boosters = JsonConvert.DeserializeObject<Dictionary<short, List<BoosterBase>>>(equipmentRow["boosters"].ToString());
                    if (boosters != null)
                    {
                        player.BoosterManager.Boosters = boosters;
                        player.BoosterManager.Update();
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Log("error_log", $"- [SocketServer.cs] ForceReloadFromDatabase({player.Id}) DB reload exception: {e}");
        }

        try
        {
            QueryManager.SetEquipment(player);
            player.DroneManager.UpdateDrones(true);
            player.UpdateStatus();
        }
        catch (Exception e)
        {
            Logger.Log("error_log", $"- [SocketServer.cs] ForceReloadFromDatabase({player.Id}) refresh exception: {e}");
        }
    }

    public static void RepairDrone(Player player, int droneId)
    {
        if (player?.GameSession != null)
        {
            var drone = player.DroneManager.GetDroneById(droneId);
            if (drone != null)
            {
                drone.Damage = 0;
                drone.Level = Math.Max(1, drone.Level - 1);
                drone.Experience = GetDroneBaseXpForLevel(drone.Level);

                QueryManager.SavePlayer.Drones(player);

                player.DroneManager.UpdateDrones(true);
                UpdateStatus(player);
            }
        }
    }

    public static void SellDrone(Player player, int droneId)
    {
        if (player?.GameSession != null)
        {
            var drone = player.DroneManager.GetDroneById(droneId);
            if (drone != null)
            {
                player.DroneManager.RemoveDrone(droneId);
                UpdateStatus(player);
            }
        }
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        try
        {
            String content = string.Empty;

            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                content = Encoding.UTF8.GetString(
                    state.buffer, 0, bytesRead);

                if (!string.IsNullOrEmpty(content))
                {
                        try
                        {
                            Console.WriteLine($"[SocketServer] Received from {handler.RemoteEndPoint}: {content}");
                        }
                        catch { }

                    var json = Parse(content);
                    var parameters = Parse(json["Parameters"]);

                    Execute(json, parameters, handler);

                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                }
            }
            else
            {
                Close(handler);
            }
        }
        catch { }
    }

    public static void Close(Socket handler)
    {
        try
        {
            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }
        catch { }
    }

    private static void Send(Socket handler, String data)
    {
        try
        {
            byte[] byteData = Encoding.UTF8.GetBytes(data);

            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }
        catch (Exception e)
        {
            Logger.Log("error_log", $"- [SocketServer.cs] Send void exception: {e}");
        }
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket handler = (Socket)ar.AsyncState;

            handler.EndSend(ar);

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }
        catch (Exception e)
        {
            //Logger.Log("error_log", $"- [SocketServer.cs] SendCallback void exception: {e}");
        }
    }

    public static void KickPlayer(Player player, string reason)
    {
        if (player?.GameSession != null)
        {
            player.SendPacket($"0|A|STD|{reason}");
            player.GameSession.Disconnect(DisconnectionType.NORMAL);
        }
    }

    public static void UpgradeSkillTree(Player player, string skill)
    {
        if (player?.GameSession != null)
        {
            if (skill == "engineering")
                player.SkillTree.engineering++;
            else if (skill == "shieldEngineering")
                player.SkillTree.shieldEngineering++;
            else if (skill == "detonation1")
                player.SkillTree.detonation1++;
            else if (skill == "detonation2")
                player.SkillTree.detonation2++;
            else if (skill == "heatseekingMissiles")
                player.SkillTree.heatseekingMissiles++;
            else if (skill == "rocketFusion")
                player.SkillTree.rocketFusion++;
            else if (skill == "cruelty1")
                player.SkillTree.cruelty1++;
            else if (skill == "cruelty2")
                player.SkillTree.cruelty2++;
            else if (skill == "explosives")
                player.SkillTree.explosives++;
            else if (skill == "luck1")
                player.SkillTree.luck1++;
            else if (skill == "luck2")
                player.SkillTree.luck2++;
            else if (skill == "bountyhunter1")
                player.SkillTree.bountyhunter1++;
            else if (skill == "bountyhunter2")
                player.SkillTree.bountyhunter2++;
            else if (skill == "electroOptics")
                player.SkillTree.electroOptics++;
            else if (skill == "shieldMechanics")
                player.SkillTree.shieldMechanics++;
            else if (skill == "shiphull1")
                player.SkillTree.shiphull1++;
            else if (skill == "shiphull2")
                player.SkillTree.shiphull2++;
            else if (skill == "greed")
                player.SkillTree.greed++;
            else if (skill == "evasiveManeuvers1")
                player.SkillTree.evasiveManeuvers1++;
            else if (skill == "evasiveManeuvers2")
                player.SkillTree.evasiveManeuvers2++;
            else if (skill == "logistics")
                player.SkillTree.logistics++;
            else if (skill == "tactics")
                player.SkillTree.tactics++;
            else if (skill == "tractorBeam1")
                player.SkillTree.tractorBeam1++;
            else if (skill == "tractorBeam2")
                player.SkillTree.tractorBeam2++;
            else if (skill == "alienHunter")
                player.SkillTree.alienHunter++;
        }
    }

    public static void ResetSkillTree(Player player)
    {
        if (player?.GameSession != null)
        {
            player.SkillTree.engineering = 0;
            player.SkillTree.shieldEngineering = 0;
            player.SkillTree.detonation1 = 0;
            player.SkillTree.detonation2 = 0;
            player.SkillTree.heatseekingMissiles = 0;
            player.SkillTree.rocketFusion = 0;
            player.SkillTree.cruelty1 = 0;
            player.SkillTree.cruelty2 = 0;
            player.SkillTree.explosives = 0;
            player.SkillTree.luck1 = 0;
            player.SkillTree.luck2 = 0;
            player.SkillTree.bountyhunter1 = 0;
            player.SkillTree.bountyhunter2 = 0;
            player.SkillTree.electroOptics = 0;
            player.SkillTree.shieldMechanics = 0;
            player.SkillTree.shiphull1 = 0;
            player.SkillTree.shiphull2 = 0;
            player.SkillTree.greed = 0;
            player.SkillTree.evasiveManeuvers1 = 0;
            player.SkillTree.evasiveManeuvers2 = 0;
            player.SkillTree.logistics = 0;
            player.SkillTree.tactics = 0;
            player.SkillTree.tractorBeam1 = 0;
            player.SkillTree.tractorBeam2 = 0;
            player.SkillTree.alienHunter = 0;
        }
    }

    public static void BanUser(Player player)
    {
        if (player == null) return;

        var client = GameManager.ChatClients[player.Id];
        client.Send($"{ChatConstants.CMD_BANN_USER}%#");
        client.Close();

        player.GameSession.Disconnect(DisconnectionType.NORMAL);
        GameManager.SendChatSystemMessage($"{player.Name} has banned.");
    }

    public static void BuyItem(Player player, string itemType, DataType dataType, int amount)
    {
        if (player?.GameSession != null)
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var result = mySqlClient.ExecuteQueryRow($"SELECT data FROM player_accounts WHERE userId = {player.Id}");
                player.Data = JsonConvert.DeserializeObject<DataBase>(result["data"].ToString());
            }

            player.SendPacket($"0|LM|ST|{(dataType == DataType.URIDIUM ? "URI" : "CRE")}|-{amount}|{(dataType == DataType.URIDIUM ? player.Data.uridium : player.Data.credits)}");

            switch (itemType)
            {
                case "drones":
                    player.DroneManager.UpdateDrones(true);
                    break;
                case "booster":
                    var oldBoosters = player.BoosterManager.Boosters;

                    using (var mySqlClient = SqlDatabaseManager.GetClient())
                    {
                        var result = mySqlClient.ExecuteQueryRow($"SELECT boosters FROM player_equipment WHERE userId = {player.Id}");
                        var newBoosters = JsonConvert.DeserializeObject<Dictionary<short, List<BoosterBase>>>(result["boosters"].ToString());
                        player.BoosterManager.Boosters = newBoosters.Concat(oldBoosters).GroupBy(b => b.Key).ToDictionary(b => b.Key, b => b.First().Value);
                    }

                    player.BoosterManager.Update();
                    break;
            }
        }
    }

    public static void ChangeClanData(Clan clan, string name, string tag, int factionId)
    {
        if (clan.Id != 0)
        {
            clan.Tag = tag;
            clan.Name = name;
            //clan.FactionId = factionId;

            foreach (GameSession gameSession in GameManager.GameSessions.Values.Where(x => x.Player.Clan.Id == clan.Id))
            {
                var player = gameSession.Player;
                if (player != null)
                    GameManager.SendCommandToMap(player.Spacemap.Id, ClanChangedCommand.write(clan.Tag, clan.Id, player.Id));
            }
        }
    }

    public static void JoinToClan(Player player, Clan clan)
    {
        if (player?.GameSession != null && clan != null)
        {
            player.Clan = clan;
            player.Quests?.TryCompleteClanQuest();

            var command = ClanChangedCommand.write(clan.Tag, clan.Id, player.Id);
            player.SendCommand(command);
            player.SendCommandToInRangePlayers(command);
        }
    }

    public static void EndDiplomacy(Clan senderClan, Clan targetClan)
    {
        if (senderClan != null && targetClan != null)
        {
            senderClan.Diplomacies.Remove(targetClan.Id);
            targetClan.Diplomacies.Remove(senderClan.Id);
        }
    }

    public static void StartDiplomacy(Clan senderClan, Clan targetClan, short diplomacyType)
    {
        if (senderClan != null && targetClan != null)
        {
            senderClan.Diplomacies.Add(targetClan.Id, (Diplomacy)diplomacyType);
            targetClan.Diplomacies.Add(senderClan.Id, (Diplomacy)diplomacyType);
        }
    }

    public static void LeaveFromClan(Player player)
    {
        foreach (var battleStation in GameManager.BattleStations.Values)
        {
            if (battleStation.EquippedStationModule.ContainsKey(player.Clan.Id))
                battleStation.EquippedStationModule[player.Clan.Id].ForEach(x => { if (x.OwnerId == player.Id) { x.Destroy(null, DestructionType.MISC); } });
        }

        if (player?.GameSession != null)
        {
            if (player.Clan.Id != 0)
            {
                player.Clan = GameManager.GetClan(0);

                var command = ClanChangedCommand.write(player.Clan.Tag, player.Clan.Id, player.Id);
                player.SendCommand(command);
                player.SendCommandToInRangePlayers(command);
            }
        }
    }

    public static void DeleteClan(Clan deletedClan)
    {
        if (deletedClan != null)
        {
            foreach (var battleStation in GameManager.BattleStations.Values.Where(x => x.Clan.Id == deletedClan.Id))
                battleStation.Destroy(null, DestructionType.MISC);

            GameManager.Clans.TryRemove(deletedClan.Id, out deletedClan);

            foreach (var gameSession in GameManager.GameSessions.Values)
            {
                var member = gameSession?.Player;

                if (member != null && member.Clan.Id == deletedClan.Id)
                {
                    member.Clan = GameManager.GetClan(0);

                    var command = ClanChangedCommand.write(member.Clan.Tag, member.Clan.Id, member.Id);
                    member.SendCommand(command);
                    member.SendCommandToInRangePlayers(command);
                }
            }

            foreach (var clan in GameManager.Clans.Values)
                clan.Diplomacies.Remove(deletedClan.Id);
        }
    }

    public static void CreateClan(Player player, int clanId, int factionId, string name, string tag)
    {
        if (!GameManager.Clans.ContainsKey(clanId))
        {
            var clan = new Clan(clanId, name, tag, factionId);
            GameManager.Clans.TryAdd(clan.Id, clan);

            if (player?.GameSession != null)
            {
                player.Clan = clan;
                player.Quests?.TryCompleteClanQuest();

                var command = ClanChangedCommand.write(clan.Tag, clan.Id, player.Id);
                player.SendCommand(command);
                player.SendCommandToInRangePlayers(command);
            }
        }
    }

    public static void ChangeCompany(Player player, int uridiumPrice, int honorPrice)
    {
        if (player?.GameSession != null)
        {
            using (var mySqlClient = SqlDatabaseManager.GetClient())
            {
                var result = mySqlClient.ExecuteQueryRow($"SELECT data, factionId FROM player_accounts WHERE userId = {player.Id}");
                player.Data = JsonConvert.DeserializeObject<DataBase>(result["data"].ToString());
                player.FactionId = Convert.ToInt32(result["factionId"]);
            }

            player.SendPacket($"0|LM|ST|URI|-{uridiumPrice}|{player.Data.uridium}");

            if (honorPrice > 0)
                player.SendPacket($"0|LM|ST|HON|-{honorPrice}|{player.Data.honor}");

            player.Jump(player.GetBaseMapId(), player.GetBasePosition());
        }
    }

    public static void ChangeShip(Player player, Ship ship)
    {
        if (player?.GameSession != null && ship != null)
        {
            player.ChangeShip(ship.Id);
            player.Storage.lastChangeShipTime = DateTime.Now;
        }
    }

    public static void UpdateStatus(Player player)
    {
        if (player?.GameSession != null)
        {
            QueryManager.SetEquipment(player);   

            player.DroneManager.UpdateDrones(true);
            player.UpdateStatus();
        }
    }

    public static int Int(object value)
    {
        try
        {
            return Convert.ToInt32(value.ToString());

        }
        catch (Exception e)
        {
            return 0;
        }
    }

    public static short Short(object value)
    {
        try
        {
            return Convert.ToInt16(value.ToString());

        }
        catch (Exception e)
        {
            return 0;
        }
        
    }

    public static string String(object value)
    {
        try
        {
            return value.ToString();

        }
        catch (Exception e)
        {
            return "";
        }
        
    }

    public static JObject Parse(object value)
    {
        try
        {
            return JObject.Parse(value.ToString());

        }
        catch (Exception e)
        {
            return null;
        }
       
    }

    private static int GetDroneBaseXpForLevel(int level)
    {
        switch (level)
        {
            case 1:
                return 0;
            case 2:
                return 100;
            case 3:
                return 300;
            case 4:
                return 700;
            case 5:
                return 1500;
            case 6:
                return 3100;
            default:
                return 0;
        }
    }
}
