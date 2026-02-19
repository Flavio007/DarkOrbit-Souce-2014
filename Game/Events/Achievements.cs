using Newtonsoft.Json;
using Ow.Managers;
using Ow.Game.Objects;
using Ow.Net.netty;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Game.Events
{
    class AchievementEntry
    {
        public int id;
        public int done;
        public int bargainState;
    }

    class AchievementManager
    {
        private static readonly HashSet<int> AllowedIds = new HashSet<int>
        {
            2
        };

        private readonly object sync = new object();
        private readonly Dictionary<int, AchievementEntry> entries = new Dictionary<int, AchievementEntry>();
        private readonly Player player;

        public AchievementManager(Player player)
        {
            this.player = player;
        }

        public void Load(string json)
        {
            lock (sync)
            {
                entries.Clear();

                if (string.IsNullOrWhiteSpace(json))
                    return;

                try
                {
                    var list = JsonConvert.DeserializeObject<List<AchievementEntry>>(json);
                    if (list == null)
                        return;

                    foreach (var item in list)
                    {
                        if (item == null || !AllowedIds.Contains(item.id))
                            continue;

                        entries[item.id] = new AchievementEntry
                        {
                            id = item.id,
                            done = item.done > 0 ? 1 : 0,
                            bargainState = item.bargainState < 0 ? 0 : item.bargainState
                        };
                    }
                }
                catch
                {
                    // Ignore malformed payloads and keep an empty state.
                }
            }
        }

        public string Serialize()
        {
            lock (sync)
            {
                return JsonConvert.SerializeObject(entries.Values.OrderBy(x => x.id).ToList());
            }
        }

        public void EnsureDefaultSeed()
        {
            lock (sync)
            {
                if (entries.Count > 0)
                    return;

                // Minimal default to keep the legacy achievement window functional.
                // Use achievement 2 for compatibility with the current startup mission test.
                entries[2] = new AchievementEntry { id = 2, done = 0, bargainState = 1 };
            }

            Save();
        }

        public void SendAll()
        {
            List<AchievementEntry> snapshot;
            lock (sync)
                snapshot = entries.Values.Where(x => AllowedIds.Contains(x.id)).OrderBy(x => x.id).ToList();

            if (snapshot.Count > 0)
            {
                var packet = "0|" + ServerCommands.ACHIEVEMENTS + "|" + ServerCommands.ACHIEVEMENT_SET;
                var packetA = "0|" + ServerCommands.SET_ATTRIBUTE + "|" + ServerCommands.ACHIEVEMENTS + "|" + ServerCommands.ACHIEVEMENT_SET;
                foreach (var item in snapshot)
                {
                    packet += "|" + item.id + "|" + item.done + "|" + item.bargainState;
                    packetA += "|" + item.id + "|" + item.done + "|" + item.bargainState;
                }
                player.SendPacket(packet);
                player.SendPacket(packetA);
            }
        }

        public void Set(int achievementId, bool done, int bargainState = 1, bool sendGain = false)
        {
            if (!AllowedIds.Contains(achievementId))
                return;

            var doneInt = done ? 1 : 0;
            var gained = false;

            lock (sync)
            {
                AchievementEntry entry;
                if (!entries.TryGetValue(achievementId, out entry))
                {
                    entry = new AchievementEntry();
                    entries[achievementId] = entry;
                }

                gained = entry.done == 0 && doneInt == 1;
                entry.id = achievementId;
                entry.done = doneInt;
                entry.bargainState = bargainState < 0 ? 0 : bargainState;
            }

            var safeBargain = bargainState < 0 ? 0 : bargainState;
            player.SendPacket("0|" + ServerCommands.ACHIEVEMENTS + "|" + ServerCommands.ACHIEVEMENT_SET +
                            "|" + achievementId + "|" + doneInt + "|" + safeBargain);
            player.SendPacket("0|" + ServerCommands.SET_ATTRIBUTE + "|" + ServerCommands.ACHIEVEMENTS + "|" + ServerCommands.ACHIEVEMENT_SET +
                            "|" + achievementId + "|" + doneInt + "|" + safeBargain);

            if (sendGain && gained)
                player.SendPacket("0|" + ServerCommands.SET_ATTRIBUTE + "|" + ServerCommands.ACHIEVEMENT_GAIN + "|" + achievementId + "|1|0");

            Save();
        }

        public void Remove(int achievementId)
        {
            if (!AllowedIds.Contains(achievementId))
                return;

            var removed = false;
            lock (sync)
                removed = entries.Remove(achievementId);

            if (!removed)
                return;

            player.SendPacket("0|" + ServerCommands.ACHIEVEMENTS + "|" + ServerCommands.ACHIEVEMENT_REMOVE + "|" + achievementId);
            player.SendPacket("0|" + ServerCommands.SET_ATTRIBUTE + "|" + ServerCommands.ACHIEVEMENTS + "|" + ServerCommands.ACHIEVEMENT_REMOVE + "|" + achievementId);
            Save();
        }

        public void HandleBuyRequest(int achievementId)
        {
            if (!AllowedIds.Contains(achievementId))
                return;

            Set(achievementId, true, 1, true);
        }

        private void Save()
        {
            try
            {
                QueryManager.SavePlayer.Achievements(player);
            }
            catch (Exception e)
            {
                Logger.Log("error_log", $"- [Achievements.cs] Save({player.Id}) exception: {e}");
            }
        }
    }
}
