using Ow.Game.Events;
using Ow.Game.Objects;
using Ow.Game.Movements;
using Ow.Managers;
using Ow.Net;
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
using Ow.Game;
using static Ow.Game.GameSession;
using System.Collections.Concurrent;
using Ow.Managers.MySQLManager;
using Ow.Game.Objects.Stations;
using Ow.Game.Objects.AI;
using Ow.Game.Objects.Collectables;
using Ow.Game.Objects.Mines;

namespace Ow.Chat
{
    public enum Permissions
    {
        NORMAL = 0,
        ADMINISTRATOR = 1,
        CHAT_MODERATOR = 2
    }

    class ChatClient
    {
        private static readonly Dictionary<string, short> TestAssetTypeByXmlId = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
        {
            { "headquarters_home_mmo", AssetTypeModule.BASE_COMPANY },
            { "headquarters_mmo_demolished", AssetTypeModule.BASE_COMPANY },
            { "headquarters_home_eic", AssetTypeModule.BASE_COMPANY },
            { "headquarters_eic_demolished", AssetTypeModule.BASE_COMPANY },
            { "headquarters_home_vru", AssetTypeModule.BASE_COMPANY },
            { "headquarters_vru_demolished", AssetTypeModule.BASE_COMPANY },
            { "headquarters_outpost_mmo", AssetTypeModule.BASE_COMPANY },
            { "headquarters_outpost_eic", AssetTypeModule.BASE_COMPANY },
            { "headquarters_outpost_vru", AssetTypeModule.BASE_COMPANY },
            { "hangar_home_1", AssetTypeModule.HANGAR_HOME },
            { "hangar_home_2", AssetTypeModule.HANGAR_HOME },
            { "hangar_home_3", AssetTypeModule.HANGAR_HOME },
            { "hangar_home_4", AssetTypeModule.HANGAR_HOME },
            { "hangar_home_5", AssetTypeModule.HANGAR_HOME },
            { "hangar_home_6", AssetTypeModule.HANGAR_HOME },
            { "hangar_outpost_1", AssetTypeModule.HANGAR_HOME },
            { "hangar_outpost_2", AssetTypeModule.HANGAR_HOME },
            { "hangar_outpost_3", AssetTypeModule.HANGAR_HOME },
            { "questgiver_3", AssetTypeModule.QUESTGIVER },
            { "questgiver_4", AssetTypeModule.QUESTGIVER },
            { "questgiver_5", AssetTypeModule.QUESTGIVER },
            { "questgiver_6", AssetTypeModule.QUESTGIVER },
            { "questgiver_7", AssetTypeModule.QUESTGIVER },
            { "questgiver_8", AssetTypeModule.QUESTGIVER },
            { "questgiver_9", AssetTypeModule.QUESTGIVER },
            { "questgiver_10", AssetTypeModule.QUESTGIVER },
            { "questgiver_11", AssetTypeModule.QUESTGIVER },
            { "repairstation_home_1", AssetTypeModule.REPAIR_STATION },
            { "repairstation_home_2", AssetTypeModule.REPAIR_STATION },
            { "repairstation_home_3", AssetTypeModule.REPAIR_STATION },
            { "repairstation_outpost_1", AssetTypeModule.REPAIR_STATION },
            { "repairstation_outpost_2", AssetTypeModule.REPAIR_STATION },
            { "repairstation_outpost_3", AssetTypeModule.REPAIR_STATION },
            { "refinery_home_1", AssetTypeModule.ORE_TRADE_STATION },
            { "refinery_home_2", AssetTypeModule.ORE_TRADE_STATION },
            { "refinery_home_3", AssetTypeModule.ORE_TRADE_STATION },
            { "refinery_outpost_1", AssetTypeModule.ORE_TRADE_STATION },
            { "refinery_outpost_2", AssetTypeModule.ORE_TRADE_STATION },
            { "refinery_outpost_3", AssetTypeModule.ORE_TRADE_STATION },
            { "boosterstation_1", AssetTypeModule.BOOSTER_STATION },
            { "boosterstation_2", AssetTypeModule.BOOSTER_STATION },
            { "boosterstation_3", AssetTypeModule.BOOSTER_STATION },
            { "boosterstation_4", AssetTypeModule.BOOSTER_STATION },
            { "turret_55_1", AssetTypeModule.varBa },
            { "turret_56_1", AssetTypeModule.varBa },
            { "turret_55_2", AssetTypeModule.varBa },
            { "turret_56_2", AssetTypeModule.varBa },
            { "turret_55_3", AssetTypeModule.varBa },
            { "turret_56_3", AssetTypeModule.varBa },
            { "turret_57_1", AssetTypeModule.var42v },
            { "turret_58_1", AssetTypeModule.var42v },
            { "turret_57_2", AssetTypeModule.var42v },
            { "turret_58_2", AssetTypeModule.var42v },
            { "turret_57_3", AssetTypeModule.var42v },
            { "turret_58_3", AssetTypeModule.var42v },
            { "relay", AssetTypeModule.RELAY_STATION },
            { "static_relay", AssetTypeModule.RELAY_STATION }
        };

        private static readonly Dictionary<string, short> VisualModifierByEffectAlias = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
        {
            { "battleRepairRobot1", VisualModifierCommand.BATTLE_REPAIR_BOT },
            { "construction", VisualModifierCommand.CONSTRUCTION_EFFECT },
            { "drawFireOwner", VisualModifierCommand.DRAW_FIRE_OWNER },
            { "drawFireTarget", VisualModifierCommand.DRAW_FIRE_TARGET },
            { "drawFireVictim", VisualModifierCommand.DRAW_FIRE_TARGET },
            { "emergencyRepair", VisualModifierCommand.EMERGENCY_REPAIR_EFFECT },
            { "fortify", VisualModifierCommand.FORTIFY },
            { "healEffect", VisualModifierCommand.HEAL_EFFECT },
            { "mirrorControls", VisualModifierCommand.MIRRORED_CONTROLS },
            { "moduleInstall", VisualModifierCommand.MODULE_INSTALL_EFFECT },
            { "moduleLevelUp", VisualModifierCommand.MODULE_LEVEL_UP_EFFECT },
            { "shield1", VisualModifierCommand.PRISMATIC_SHIELD },
            { "stickyBomb", VisualModifierCommand.STICKY_BOMB },
            { "warpAnimation", VisualModifierCommand.WARP_ANIMATION_EFFECT }
        };

        public Socket Socket { get; set; }
        public int UserId { get; set; }
        public Permissions Permission { get; set; }
        public List<Int32> ChatsJoined = new List<Int32>();

        public static List<string> Filter = new List<string>
        {
            "orospu",
            "çocuğu",
            "karını",
            "sülaleni",
            "dinini",
            "imanını",
            "kitabını",
            "siker",
            "lavuk",
            "ananı",
            "gavat",
            "oneultimate",
            "http",
            "bitch",
            "fuck",
            "restart",
            "amına",
            "piç",
            "yavşak",
            "siktir",
            "anne",
            "bacı",
            "sikerim",
            "pezevenk",
            ".ovh",
            "puto",
            "maldito",
            "perro"
        };

        public ChatClient(Socket handler)
        {
            Socket = handler;

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static void LoadChatRooms()
        {
            var rooms = new List<Room> {
                new Chat.Room(1, "Global", 0, -1),
                new Chat.Room(2, "MMO", 1, 1),
                new Chat.Room(3, "EIC", 2, 2),
                new Chat.Room(4, "VRU", 3, 3),
                new Chat.Room(5, "Clan Search", 5, -1)
            };

            foreach (var room in rooms)
                Chat.Room.Rooms.Add(room.Id, room);
        }

        public void Execute(string message)
        {
            try
            {
                string[] packet = message.Split(ChatConstants.MSG_SEPERATOR);
                switch (packet[0])
                {
                    case ChatConstants.CMD_USER_LOGIN:
                        var loginPacket = message.Replace("@", "%").Split('%');
                        UserId = Convert.ToInt32(loginPacket[3]);

                        if (!QueryManager.CheckSessionId(UserId, loginPacket[4]))
                        {
                            Close();
                            return;
                        }

                        var gameSession = GameManager.GetGameSession(UserId);
                        if (gameSession == null) return;

                        var chatPermission = QueryManager.GetChatPermission(gameSession.Player.Id);
                        if (gameSession.Player.RankId == 21 && chatPermission == (int)Permissions.NORMAL)
                            chatPermission = (int)Permissions.ADMINISTRATOR;

                        Permission = (Permissions)chatPermission;
                        Out.WriteLine($"[CHAT_LOGIN] user={gameSession.Player.Id} rank={gameSession.Player.RankId} chatPerm={chatPermission} effectivePerm={Permission}", "ChatClient.cs");

                        if (GameManager.ChatClients.ContainsKey(UserId))
                            GameManager.ChatClients[gameSession.Player.Id]?.Close();

                        GameManager.ChatClients.TryAdd(gameSession.Player.Id, this);

                        Send("bv%" + gameSession.Player.Id + "#");
                        var servers = Room.Rooms.Aggregate(String.Empty, (current, chat) => current + chat.Value.ToString());
                        servers = servers.Remove(servers.Length - 1);
                        Send("by%" + servers + "#");
                        Send($"dq%Use '/duel <Player nickname>' for invite someone to duel.#");
                        ChatsJoined.Add(Room.Rooms.FirstOrDefault().Value.Id);

                        if (QueryManager.ChatFunctions.Banned(UserId))
                        {
                            Send($"{ChatConstants.CMD_BANN_USER}%#");
                            Close();
                            return;
                        }
                        break;
                    case ChatConstants.CMD_USER_MSG:
                        SendMessage(message);
                        break;
                    case ChatConstants.CMD_USER_JOIN:
                        var roomId = Convert.ToInt32(message.Split('%')[2].Split('@')[0]);
                        gameSession = GameManager.GetGameSession(UserId);

                        if (Room.Rooms.ContainsKey(roomId))
                        {
                            if (!ChatsJoined.Contains(roomId))
                                ChatsJoined.Add(roomId);
                        }
                        else
                        {
                            if (gameSession.Player.Storage.DuelInvites.ContainsKey(roomId))
                                AcceptDuel(gameSession.Player.Storage.DuelInvites[roomId], roomId);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Out.WriteLine("Exception: " + e, "ChatClient.cs");
                Logger.Log("error_log", $"- [ChatClient.cs] Execute void exception: {e}");
            }
        }

        public void AcceptDuel(Player inviterPlayer, int duelId)
        {          
            var gameSession = GameManager.GetGameSession(UserId);

            if (!gameSession.Player.Storage.DuelInvites.ContainsKey(duelId))
            {
                Send($"dq%This invite is no longer available.#");
                return;
            }

            if (Duel.InDuel(gameSession.Player))
            {
                Send($"dq%You can't accept duels while you're in a duel.#");
                return;
            }

            if (Duel.InDuel(inviterPlayer))
            {
                Send($"dq%Your opponent is already fighting on duel with another player.#");
                return;
            }

            if (inviterPlayer != null && gameSession.Player != null)
            {
                if (gameSession.Player.Storage.IsInEquipZone && inviterPlayer.Storage.IsInEquipZone)
                {
                    gameSession.Player.Storage.DuelInvites.TryRemove(duelId, out inviterPlayer);
                    var players = new ConcurrentDictionary<int, Player>();
                    players.TryAdd(gameSession.Player.Id, gameSession.Player);
                    players.TryAdd(inviterPlayer.Id, inviterPlayer);

                    new Duel(players);
                }
            }          
        }

        public void SendMessage(string content)
        {
            var gameSession = GameManager.GetGameSession(UserId);
            if (gameSession == null) return;

            gameSession.LastActiveTime = DateTime.Now;
            string messagePacket = "";

            var packet = content.Replace("@", "%").Split('%');
            var roomId = packet[1];
            var message = packet[2];

            var cmd = message.Split(' ')[0];
            if (message.StartsWith("/reconnect"))
            {
                Close();
            }
            else if (cmd == "/w")
            {
                if (message.Split(' ').Length < 2)
                {
                    Send($"{ChatConstants.CMD_NO_WHISPER_MESSAGE}%#");
                    return;
                }

                var player = GameManager.GetPlayerByName(message.Split(' ')[1]);

                if (player == null || !GameManager.ChatClients.ContainsKey(player.Id))
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                if (player.Name == gameSession.Player.Name)
                {
                    Send($"dq%You can't whisper to yourself.#");
                    //Send($"{ChatConstants.CMD_CANNOT_WHISPER_YOURSELF}%#");
                    return;
                }

                message = message.Remove(0, player.Name.Length + 3);
                GameManager.ChatClients[player.Id].Send("cv%" + gameSession.Player.Name + "@" + message + "#");
                Send("cw%" + player.Name + "@" + message + "#");

                foreach (var client in GameManager.ChatClients.Values)
                {
                    if (gameSession.Player.Id != client.UserId && client.Permission == Permissions.ADMINISTRATOR && GameManager.ChatClients[player.Id].Permission != Permissions.ADMINISTRATOR)
                        client.Send($"dq%{gameSession.Player.Name} whispering to {player.Name}:{message}#");
                }

                Logger.Log("chat_log", $"{gameSession.Player.Name} ({gameSession.Player.Id}) whispering to {player.Name} ({player.Id}):{message}");
            }
            else if (cmd == "/kick" && (Permission == Permissions.ADMINISTRATOR || Permission == Permissions.CHAT_MODERATOR))
            {
                if (message.Split(' ').Length < 2) return;

                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var player = GameManager.GetPlayerById(userId);

                if (player != null && player.Name != gameSession.Player.Name)
                {
                    if (GameManager.ChatClients.ContainsKey(player.Id))
                    {
                        var client = GameManager.ChatClients[player.Id];
                        client.Send($"{ChatConstants.CMD_KICK_USER}%#");
                        client.Close();

                        GameManager.SendChatSystemMessage($"{player.Name} has kicked.");
                    }
                }
            }
            else if (cmd == "/duel")
            {
                if (message.Split(' ').Length < 2)
                {
                    Send($"dq%Use '/duel <Player nickname>' for invite someone to duel.#");
                    return;
                }

                var userName = message.Split(' ')[1];
                var duelPlayer = GameManager.GetPlayerByName(userName);

                if (duelPlayer == null || !GameManager.ChatClients.ContainsKey(duelPlayer.Id))
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                if (duelPlayer.Name == gameSession.Player.Name)
                {
                    Send($"{ChatConstants.CMD_CANNOT_INVITE_YOURSELF}%#");
                    return;
                }

                if (duelPlayer == null || duelPlayer == gameSession.Player || !GameManager.ChatClients.ContainsKey(duelPlayer.Id)) return;
                if (duelPlayer.Storage.DuelInvites.Any(x => x.Value == gameSession.Player))
                {
                    Send($"dq%{userName} already invited from you before.#");
                    return;
                }

                var duelId = Randoms.CreateRandomID();

                Send($"cr%{duelPlayer.Name}#");
                duelPlayer.Storage.DuelInvites.TryAdd(duelId, gameSession.Player);

                GameManager.ChatClients[duelPlayer.Id].Send("cj%" + duelId + "@" + "Duel" + "@" + 0 + "@" + gameSession.Player.Name + "#");
            }
            else if (cmd == "/msg" && Permission == Permissions.ADMINISTRATOR)
            {
                var msg = message.Remove(0, 4);
                GameManager.SendPacketToAll($"0|A|STD|{msg}");
            }
            else if (cmd == "/pk")
            {
                if (Permission != Permissions.ADMINISTRATOR && gameSession.Player.RankId != 21)
                {
                    Send("dq%You don't have permission to use '/pk'.#");
                    return;
                }

                if (message.Length <= 4)
                {
                    Send("dq%Use '/pk <packet>'.#");
                    return;
                }

                var packetToSendRaw = message.Substring(4).TrimStart();
                if (string.IsNullOrWhiteSpace(packetToSendRaw))
                {
                    Send("dq%Use '/pk <packet>'.#");
                    return;
                }

                var packetToSend = packetToSendRaw
                    .Replace("ATRIBUTE_SEPERATOR", ChatConstants.ATRIBUTE_SEPERATOR)
                    .Replace("PARAM_SEPERATOR", ChatConstants.PARAM_SEPERATOR)
                    .Replace("OBJECT_SEPERATOR", ChatConstants.OBJECT_SEPERATOR)
                    .Replace("LINE_SEPERATOR", ChatConstants.LINE_SEPERATOR)
                    .Replace("MSG_SEPERATOR", ChatConstants.MSG_SEPERATOR.ToString());

                Out.WriteLine($"[PK_CMD] Packet lido de {gameSession.Player.Name} ({gameSession.Player.Id}): {packetToSend}", "ChatClient.cs");
                if (gameSession.Client != null && gameSession.Client.Socket != null && gameSession.Client.Socket.IsBound && gameSession.Client.Socket.Connected)
                {
                    gameSession.Client.Send(LegacyModule.write(packetToSend));
                }
                else
                {
                    Send("dq%PK failed: game socket is not connected.#");
                }
            }
            else if (cmd == "/destroy" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var player = GameManager.GetPlayerById(userId);

                if (player == null)
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                player.Destroy(gameSession.Player, Game.DestructionType.PLAYER);
            }
            else if (cmd == "/ship" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var shipId = Convert.ToInt32(message.Split(' ')[1]);
                var ship = GameManager.GetShip(shipId);

                if (ship == null)
                {
                    Send($"dq%The ship that with entered doesn't exists.#");
                    return;
                }

                gameSession.Player.ChangeShip(shipId);
            }
            else if (cmd == "/jump" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var mapId = Convert.ToInt32(message.Split(' ')[1]);
                gameSession.Player.Jump(mapId, new Position(0, 0));
            }
            else if (cmd == "/move" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 3) return;

                var player = GameManager.GetPlayerById(Convert.ToInt32(message.Split(' ')[1]));
                var map = GameManager.GetSpacemap(Convert.ToInt32(message.Split(' ')[2]));

                if (player == null)
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                if (map == null)
                {
                    Send($"dq%The map that with entered doesn't exists.#");
                    return;
                }

                GameManager.GetPlayerById(player.Id)?.Jump(map.Id, new Position(0, 0));
            }
            else if (cmd == "/teleport" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 3) return;

                var player = GameManager.GetPlayerById(Convert.ToInt32(message.Split(' ')[1]));

                if (player == null)
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                gameSession.Player?.Jump(player.Spacemap.Id, player.Position);
            }
            else if (cmd == "/pull" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 3) return;

                var player = GameManager.GetPlayerById(Convert.ToInt32(message.Split(' ')[1]));

                if (player == null)
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                player?.Jump(gameSession.Player.Spacemap.Id, gameSession.Player.Position);
            }
            else if (cmd == "/speed+" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var speed = Convert.ToInt32(message.Split(' ')[1]);
                gameSession.Player.SetSpeedBoost(speed);
            }
            else if (cmd == "/damage+" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var damage = Convert.ToInt32(message.Split(' ')[1]);
                gameSession.Player.Storage.DamageBoost = damage;
            }
            else if (cmd == "/god" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var mod = message.Split(' ')[1];
                gameSession.Player.Storage.GodMode = mod == "on" ? true : mod == "off" ? false : false;
            }
            else if (cmd == "/upgrade" && Permission == Permissions.ADMINISTRATOR)
            {
                var args = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length < 2)
                {
                    Send("dq%Use '/upgrade <nivel>'.#");
                    return;
                }

                if (!int.TryParse(args[1], out var requestedLevel))
                {
                    Send("dq%Nivel invalido.#");
                    return;
                }

                BattleStation battleStation = null;
                if (gameSession.Player.Selected is BattleStation selectedBattleStation)
                    battleStation = selectedBattleStation;
                else if (gameSession.Player.Selected is Satellite selectedSatellite)
                    battleStation = selectedSatellite.BattleStation;

                if (battleStation == null && gameSession.Player.Spacemap != null)
                {
                    battleStation = gameSession.Player.Spacemap.Activatables.Values
                        .OfType<BattleStation>()
                        .Where(x => x != null && !x.Destroyed)
                        .OrderBy(x => x.Position.DistanceTo(gameSession.Player.Position))
                        .FirstOrDefault();
                }

                if (battleStation == null)
                {
                    Send("dq%Nenhuma estação de batalha encontrada no mapa atual.#");
                    return;
                }

                if (battleStation.FactionId == 0)
                {
                    Send("dq%A estação precisa estar capturada antes de receber upgrade.#");
                    return;
                }

                if (!battleStation.SetUpgradeLevel(requestedLevel, true))
                {
                    Send("dq%Nao foi possivel alterar o nivel da estação.#");
                    return;
                }

                var finalLevel = battleStation.GetEffectiveLevel();
                GameManager.SendPacketToMap(battleStation.Spacemap.Id, $"0|A|STD|Battle station {battleStation.Name} was set to level {finalLevel} by {gameSession.Player.Name}.");
                Send($"dq%Estação {battleStation.Name} ajustada para o nível {finalLevel}.#");
            }
            else if (cmd == "/fake")
            {
                if (Permission != Permissions.ADMINISTRATOR)
                {
                    Send("dq%You don't have permission to use '/fake'.#");
                    return;
                }

                var args = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length < 3)
                {
                    Send("dq%Use '/fake <ShipId> <FactionId>'.#");
                    return;
                }

                int shipId;
                if (!int.TryParse(args[1], out shipId))
                {
                    Send("dq%Invalid ShipId.#");
                    return;
                }

                int factionId;
                if (!int.TryParse(args[2], out factionId) || factionId < 1 || factionId > 3)
                {
                    Send("dq%Invalid FactionId. Use 1, 2 or 3.#");
                    return;
                }

                var ship = GameManager.GetShip(shipId);
                if (ship == null)
                {
                    Send("dq%Ship not found.#");
                    return;
                }

                var fake = AIShips.CreateStationaryFakePlayer(ship, gameSession.Player.Spacemap, gameSession.Player.Position, factionId);
                Send($"dq%Fake ship created. Id={fake.Id}, Name={fake.Name}, Ship={shipId}, Faction={factionId}.#");
            }
            else if (cmd == "/effect" && Permission == Permissions.ADMINISTRATOR)
            {
                var args = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length < 2)
                {
                    Send("dq%Use '/effect <VISUAL_MODIFIER_ID|NAME|EFFECT_ALIAS> [attribute] [count] [shipLootId]' or '/effect remove <...>'. XML effect IDs are different.#");
                    return;
                }

                var removeEffect = args[1].Equals("remove", StringComparison.OrdinalIgnoreCase) || args[1].Equals("off", StringComparison.OrdinalIgnoreCase);
                var effectIndex = removeEffect ? 2 : 1;

                if (args.Length <= effectIndex)
                {
                    Send("dq%Use '/effect <VISUAL_MODIFIER_ID|NAME|EFFECT_ALIAS> [attribute] [count] [shipLootId]' or '/effect remove <...>'. XML effect IDs are different.#");
                    return;
                }

                if (!TryResolveVisualModifier(args[effectIndex], out var modifier))
                {
                    Send("dq%Invalid effect. Use a visual modifier ID, a VisualModifierCommand constant, or an effect alias like healEffect.#");
                    return;
                }

                if (removeEffect)
                {
                    gameSession.Player.RemoveVisualModifier(modifier);
                    Send($"dq%Effect {modifier} removed from {gameSession.Player.Name}.#");
                    return;
                }

                var attribute = 0;
                var count = 0;
                var shipLootId = gameSession.Player.Ship?.LootId ?? "";

                if (args.Length > effectIndex + 1 && !int.TryParse(args[effectIndex + 1], out attribute))
                {
                    Send("dq%Invalid attribute value.#");
                    return;
                }

                if (args.Length > effectIndex + 2 && !int.TryParse(args[effectIndex + 2], out count))
                {
                    Send("dq%Invalid count value.#");
                    return;
                }

                if (args.Length > effectIndex + 3)
                    shipLootId = args[effectIndex + 3];

                gameSession.Player.RemoveVisualModifier(modifier);
                gameSession.Player.AddVisualModifier(modifier, attribute, shipLootId, count, true);
                Send($"dq%Effect {modifier} applied to {gameSession.Player.Name}. Attribute={attribute}, Count={count}, ShipLootId={shipLootId}.#");
            }
            else if (cmd == "/clear" && Permission == Permissions.ADMINISTRATOR)
            {
                var removedEffects = ClearVisualModifiers(gameSession.Player);
                Send($"dq%Removed {removedEffects} effect(s) from {gameSession.Player.Name}.#");
            }
            else if (cmd == "/test")
            {
                var args = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Out.WriteLine($"[TEST_CMD] Received from {gameSession.Player.Name} ({gameSession.Player.Id}): {message}", "ChatClient.cs");

                if (Permission != Permissions.ADMINISTRATOR)
                {
                    Send("dq%You don't have permission to use '/test'.#");
                    Out.WriteLine($"[TEST_CMD] Denied (no permission) user={gameSession.Player.Id}", "ChatClient.cs");
                    return;
                }

                if (args.Length < 2)
                {
                    Send("dq%Use '/test <TypeId|TYPE_NAME|ASSET_ID> [FactionId]'.#");
                    Out.WriteLine($"[TEST_CMD] Invalid usage user={gameSession.Player.Id}", "ChatClient.cs");
                    return;
                }

                if (!TryResolveTestAssetType(args[1], out var typeId, out var visualAssetName))
                {
                    Send("dq%Invalid asset type. Use a numeric ID, an AssetTypeModule constant name, or an XML asset id like boosterstation_1.#");
                    Out.WriteLine($"[TEST_CMD] Invalid type value='{args[1]}' user={gameSession.Player.Id}", "ChatClient.cs");
                    return;
                }

                int? factionId = null;
                if (args.Length >= 3)
                {
                    if (!int.TryParse(args[2], out var parsedFactionId))
                    {
                        Send("dq%Invalid FactionId.#");
                        Out.WriteLine($"[TEST_CMD] Invalid FactionId value='{args[2]}' user={gameSession.Player.Id}", "ChatClient.cs");
                        return;
                    }

                    factionId = parsedFactionId;
                }

                const int testMapId = 58;
                var testMap = GameManager.GetSpacemap(testMapId);
                if (testMap == null)
                {
                    Send("dq%Map 58 not found.#");
                    Out.WriteLine("[TEST_CMD] Map 58 not found", "ChatClient.cs");
                    return;
                }

                var homeStations = testMap.Activatables.Values.OfType<HomeStation>().ToList();
                if (homeStations.Count == 0)
                {
                    Send("dq%No home station found in map 58.#");
                    Out.WriteLine("[TEST_CMD] No HomeStation found in map 58", "ChatClient.cs");
                    return;
                }

                foreach (var homeStation in homeStations)
                {
                    var oldAssetType = homeStation.GetAssetType();
                    GameManager.SendCommandToMap(testMapId, AssetRemoveCommand.write(oldAssetType, homeStation.Id));
                    homeStation.AssetTypeId = typeId;
                    homeStation.VisualAssetNameOverride = visualAssetName;
                    if (factionId.HasValue)
                        homeStation.FactionId = factionId.Value;
                    GameManager.SendCommandToMap(testMapId, homeStation.GetAssetCreateCommand());
                }

                var finalFaction = homeStations.FirstOrDefault()?.FactionId;
                var resolvedAsset = string.IsNullOrEmpty(visualAssetName) ? typeId.ToString() : visualAssetName;
                Send($"dq%Map 58 station updated. Asset={resolvedAsset}, TypeId={typeId}, FactionId={finalFaction}.#");
                Out.WriteLine($"[TEST_CMD] Success user={gameSession.Player.Id} typeId={typeId} visualAsset={(string.IsNullOrEmpty(visualAssetName) ? "<default>" : visualAssetName)} requestedFaction={(factionId.HasValue ? factionId.Value.ToString() : "unchanged")} stations={homeStations.Count} finalFaction={finalFaction}", "ChatClient.cs");
            }

            else if (cmd == "/testbox")
            {
                if (Permission != Permissions.ADMINISTRATOR)
                {
                    Send("dq%You don't have permission to use '/testbox'.#");
                    return;
                }

                var args = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length < 2)
                {
                    Send("dq%Use '/testbox <ID|TYPE_NAME>'.#");
                    return;
                }

                var spawnPosition = new Position(gameSession.Player.Position.X, gameSession.Player.Position.Y + 200);

                if (short.TryParse(args[1], out var boxId))
                {
                    new BonusBox(boxId, spawnPosition, gameSession.Player.Spacemap, false);
                    Send($"dq%Bonus box criada com ID {boxId} em X={spawnPosition.X}, Y={spawnPosition.Y}.#");
                    return;
                }

                if (!BonusBox.TryNormalizeBoxTypeName(args[1], out var visualTypeName))
                {
                    Send("dq%Invalid box type. Use an AssetType ID or a client XML type name like HELIX_BOX.#");
                    return;
                }

                new BonusBox(AssetTypeModule.BOXTYPE_BONUS_BOX, visualTypeName, spawnPosition, gameSession.Player.Spacemap, false);
                Send($"dq%Bonus box criada com tipo {visualTypeName} em X={spawnPosition.X}, Y={spawnPosition.Y}.#");
            }

            else if (cmd == "/testmine")
            {
                if (Permission != Permissions.ADMINISTRATOR)
                {
                    Send("dq%You don't have permission to use '/testmine'.#");
                    return;
                }

                var args = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length < 2)
                {
                    Send("dq%Use '/testmine <ID>'.#");
                    return;
                }

                if (!int.TryParse(args[1], out var mineTypeId))
                {
                    Send("dq%Invalid ID.#");
                    return;
                }

                new ACM_01(gameSession.Player, gameSession.Player.Spacemap, new Position(gameSession.Player.Position.X, gameSession.Player.Position.Y), mineTypeId);
                Send($"dq%Mina normal criada com ID {mineTypeId} em X={gameSession.Player.Position.X}, Y={gameSession.Player.Position.Y}.#");
            }

            else if (cmd == "/start_spaceball" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length >= 2)
                {
                    var limit = Convert.ToInt32(message.Split(' ')[1]);
                    EventManager.Spaceball.Limit = limit;
                }

                EventManager.Spaceball.Start();
            }
            else if (cmd == "/stop_spaceball" && Permission == Permissions.ADMINISTRATOR)
            {
                EventManager.Spaceball.Stop();
            }
            else if (cmd == "/start_jpb" && Permission == Permissions.ADMINISTRATOR)
            {
                EventManager.JackpotBattle.Start();
            }
            else if (cmd == "/give_booster" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 3) return;

                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var boosterType = Convert.ToInt32(message.Split(' ')[2]);
                var hours = message.Split(' ').Length == 4 ? Convert.ToInt32(message.Split(' ')[3]) : 10;

                if (!new int[] { 0, 1, 2, 3, 8, 9, 10, 11, 12, 5, 6, 15, 16, 7, 4 }.Contains(boosterType)) return;

                var player = GameManager.GetPlayerById(userId);

                if (player != null)
                    player.BoosterManager.Add((BoosterType)boosterType, hours);
            }
            else if (cmd == "/ban" && (Permission == Permissions.ADMINISTRATOR || Permission == Permissions.CHAT_MODERATOR))
            {
                /*
                0 CHAT BAN
                1 OYUN BANI
                */
                if (message.Split(' ').Length < 4) return;

                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var typeId = Convert.ToInt32(message.Split(' ')[2]);
                var hours = Convert.ToInt32(message.Split(' ')[3]);
                var reason = message.Remove(0, (userId.ToString().Length + typeId.ToString().Length + hours.ToString().Length) + 7);

                if (typeId == 1 && Permission == Permissions.CHAT_MODERATOR) return;

                if (typeId == 0 || typeId == 1)
                {
                    QueryManager.ChatFunctions.AddBan(userId, gameSession.Player.Id, reason, typeId, (DateTime.Now.AddHours(hours)).ToString("yyyy-MM-dd HH:mm:ss"));

                    var player = GameManager.GetPlayerById(userId);

                    if (player != null)
                    {
                        if (GameManager.ChatClients.ContainsKey(player.Id))
                        {
                            var client = GameManager.ChatClients[player.Id];

                            if (client != null)
                            {
                                client.Send($"{ChatConstants.CMD_BANN_USER}%#");
                                client.Close();
                            }
                        }

                        if (typeId == 1)
                        {
                            player.Destroy(null, DestructionType.MISC);
                            player.GameSession.Disconnect(DisconnectionType.NORMAL);
                        }
                    }
                }
            }
            else if (cmd == "/unban" && (Permission == Permissions.ADMINISTRATOR || Permission == Permissions.CHAT_MODERATOR))
            {
                /*
                0 CHAT BAN
                1 OYUN BANI
                */
                if (message.Split(' ').Length < 4) return;

                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var typeId = Convert.ToInt32(message.Split(' ')[2]);

                if (typeId == 1 && Permission == Permissions.CHAT_MODERATOR) return;

                if (typeId == 0 || typeId == 1)
                    QueryManager.ChatFunctions.UnBan(userId, gameSession.Player.Id, typeId);
            }
            else if (cmd == "/restart" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var seconds = Convert.ToInt32(message.Split(' ')[1]);
                GameManager.Restart(seconds);
            }
            else if (cmd == "/users")
            {
                var users = GameManager.GameSessions.Values.Where(x => x.Player.RankId != 21).Aggregate(String.Empty, (current, user) => current + user.Player.Name + ", ");
                users = users.Remove(users.Length - 2);

                Send($"dq%Users online {GameManager.GameSessions.Values.Where(x => x.Player.RankId != 21).Count()}: {users}#");
            }
            else if (cmd == "/system" && Permission == Permissions.ADMINISTRATOR)
            {
                message = message.Remove(0, 8);
                GameManager.SendChatSystemMessage(message);
            }
            else if (cmd == "/title" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 3) return;
                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var title = message.Split(' ')[2];
                var permanent = Convert.ToBoolean(Convert.ToInt32(message.Split(' ')[3]));

                var player = GameManager.GetPlayerById(userId);
                if (player == null || !GameManager.ChatClients.ContainsKey(player.Id))
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                player.SetTitle(title, permanent);
            }
            else if (cmd == "/rmtitle" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 3) return;
                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var permanent = Convert.ToBoolean(Convert.ToInt32(message.Split(' ')[2]));

                var player = GameManager.GetPlayerById(userId);
                if (player == null || !GameManager.ChatClients.ContainsKey(player.Id))
                {
                    Send($"{ChatConstants.CMD_USER_NOT_EXIST}%#");
                    return;
                }

                player.SetTitle("", permanent);
            }
            else if (cmd == "/id" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 2) return;

                var userName = message.Split(' ')[1];

                using (var mySqlClient = SqlDatabaseManager.GetClient())
                {
                    var query = $"SELECT userId FROM player_accounts WHERE pilotName = '{userName}'";

                    var result = (DataTable)mySqlClient.ExecuteQueryTable(query);
                    if (result.Rows.Count >= 1)
                    {
                        var userId = mySqlClient.ExecuteQueryRow(query)["userId"].ToString();

                        Send($"dq%{userName} id is: {userId}#");
                    }
                }
            }
            else if (cmd == "/start_scoreevent")
            {
                if (message.Split(' ').Length >= 2)
                {
                    var limit = Convert.ToInt32(message.Split(' ')[1]);
                    EventManager.Scoremageddon.Limit = limit;
                }

                EventManager.Scoremageddon.Start();
            }
            else if (cmd == "/start_invasiongate")
            {
                EventManager.InvasionGate.Startup();
            }
            /*
            else if (cmd == "/reward" && Permission == Permissions.ADMINISTRATOR)
            {
                if (message.Split(' ').Length < 4) return;
                var userId = Convert.ToInt32(message.Split(' ')[1]);
                var typeId = Convert.ToInt32(message.Split(' ')[2]); //1 uridium / 2 credits / 3 honor / 4 experience
                var amount = Convert.ToInt32(message.Split(' ')[3]);

                var player = GameManager.GetPlayerById(userId);
                
                if (player != null && new[] {1,2,3,4}.Contains(typeId))
                {
                    var rewardName = "";
                    switch (typeId)
                    {
                        case 1:
                            player.ChangeData(DataType.URIDIUM, amount);
                            rewardName = "uridium";
                            break;
                        case 2:
                            player.ChangeData(DataType.CREDITS, amount);
                            rewardName = "credits";
                            break;
                        case 3:
                            player.ChangeData(DataType.HONOR, amount);
                            rewardName = "honor";
                            break;
                        case 4:
                            player.ChangeData(DataType.EXPERIENCE, amount);
                            rewardName = "experience";
                            break;
                    }
                    player.SendPacket($"0|A|STD|You got {amount} {rewardName} from {gameSession.Player.Name}.");
                    Send($"dq%{player.Name} has got {amount} {rewardName} from you.#");
                    GameManager.ChatClients[player.Id].Send($"dq%You got {amount} {rewardName} from {gameSession.Player.Name}.#");
                }
            }
            */
            else
            {
                if (!cmd.StartsWith("/"))
                {
                    foreach (var m in Filter)
                    {
                        if (message.ToLower().Contains(m.ToLower()) && Permission == Permissions.NORMAL)
                        {
                            Send($"{ChatConstants.CMD_KICK_BY_SYSTEM}%#");
                            Close();
                            return;
                        }
                    }

                    foreach (var pair in GameManager.ChatClients.Values)
                    {
                        if (pair.ChatsJoined.Contains(Convert.ToInt32(roomId)))
                        {
                            var name = gameSession.Player.Name + (pair.Permission == Permissions.ADMINISTRATOR || pair.Permission == Permissions.CHAT_MODERATOR ? $" ({gameSession.Player.Id})" : "");
                            var color = (Permission == Permissions.ADMINISTRATOR || Permission == Permissions.CHAT_MODERATOR) ? "j" : "a";
                            messagePacket = $"{color}%" + roomId + "@" + name + "@" + message;

                            if (gameSession.Player.Clan.Tag != "")
                                messagePacket += "@" + gameSession.Player.Clan.Tag;

                            pair.Send(messagePacket + "#");
                        }
                    }

                    Logger.Log("chat_log", $"{gameSession.Player.Name} ({gameSession.Player.Id}): {message}");
                }
            }
        }

        private static bool TryResolveTestAssetType(string value, out short typeId, out string visualAssetName)
        {
            visualAssetName = null;

            if (short.TryParse(value, out typeId))
                return true;

            var normalizedValue = value.Trim();
            var fields = typeof(AssetTypeModule).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(short) || !field.Name.Equals(normalizedValue, StringComparison.OrdinalIgnoreCase))
                    continue;

                typeId = (short)field.GetValue(null);
                return true;
            }

            if (TestAssetTypeByXmlId.TryGetValue(normalizedValue, out typeId))
            {
                visualAssetName = normalizedValue;
                return true;
            }

            return false;
        }

        private static bool TryResolveVisualModifier(string value, out short modifier)
        {
            if (short.TryParse(value, out modifier))
                return true;

            var normalizedValue = value.Trim();

            if (VisualModifierByEffectAlias.TryGetValue(normalizedValue, out modifier))
                return true;

            var fields = typeof(VisualModifierCommand).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(short) || !field.Name.Equals(normalizedValue, StringComparison.OrdinalIgnoreCase))
                    continue;

                modifier = (short)field.GetValue(null);
                return true;
            }

            modifier = 0;
            return false;
        }

        private static int ClearVisualModifiers(Player player)
        {
            if (player == null)
                return 0;

            var modifiers = player.VisualModifiers.Keys.ToList();
            foreach (var modifier in modifiers)
                player.RemoveVisualModifier(modifier);

            return modifiers.Count;
        }

        public void Close()
        {
            try
            {
                Socket.Shutdown(SocketShutdown.Both);
                Socket.Close();

                GameManager.ChatClients.TryRemove(UserId, out var value);
            }
            catch (Exception)
            {
                //ignore
                //Logger.Log("error_log", $"- [ChatClient.cs] Close void exception: {e}");
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            try
            {
                if (Socket == null || !Socket.IsBound || !Socket.Connected) return;

                String content = String.Empty;

                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    content = Encoding.UTF8.GetString(
                        state.buffer, 0, bytesRead);

                    if (content.Trim() != "")
                    {
                        Execute(content);

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
                if (Socket == null || !Socket.IsBound || !Socket.Connected) return;

                byte[] byteData = Encoding.UTF8.GetBytes(data);

                Socket.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), Socket);
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [ChatClient.cs] Send void exception: {e}");
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;

                handler.EndSend(ar);
            }
            catch (Exception)
            {
                //Logger.Log("error_log", $"- [ChatClient.cs] SendCallback void exception: {e}");
            }
        }
    }
}
