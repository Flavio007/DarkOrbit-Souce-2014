using Newtonsoft.Json;
using Ow.Game.Objects;
using Ow.Game.Objects.Stations;
using Ow.Managers;
using Ow.Net.netty.commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Players.Managers
{
    public class BoosterBase
    {
        public short Type { get; set; }
        public int Seconds { get; set; }

        public BoosterBase(short type, int seconds)
        {
            Type = type;
            Seconds = seconds;
        }
    }

    class BoosterManager : AbstractManager
    {
        public Dictionary<short, List<BoosterBase>> Boosters = new Dictionary<short, List<BoosterBase>>();

        public bool HasVisibleBoosters => GetVisibleBoosterTypes().Count > 0;

        public BoosterManager(Player player) : base(player) { }

        private DateTime boosterTime = new DateTime();
        public void Tick()
        {
            if (boosterTime.AddSeconds(5) < DateTime.Now)
            {
                for (short i = 0; i < Boosters.ToList().Count; i++)
                {
                    var boosters = Boosters.ToList()[i].Value;

                    for (short k = 0; k < boosters.Count; k++)
                    {
                        boosters[k].Seconds -= 5;

                        if (boosters[k].Seconds <= 0)
                            Remove((BoosterType)boosters[k].Type);
                    }
                }
                boosterTime = DateTime.Now;
            }
        }

        public void Add(BoosterType boosterType, int hours)
        {
            Player.SendPacket($"0|A|STM|booster_found|%BOOSTERNAME%|{boosterType.ToString()}|%HOURS%|{hours}");

            var seconds = (int)TimeSpan.FromHours(hours).TotalSeconds;
            short boostedAttributeType = GetBoosterType((short)boosterType);

            if (boostedAttributeType != 0)
            {
                if (!Boosters.ContainsKey((short)boostedAttributeType))
                    Boosters[boostedAttributeType] = new List<BoosterBase>();

                if (Boosters[boostedAttributeType].Where(x => x.Type == (short)boosterType).Count() <= 0)
                    Boosters[boostedAttributeType].Add(new BoosterBase((short)boosterType, seconds));
                else
                    Boosters[boostedAttributeType].Where(x => x.Type == (short)boosterType).FirstOrDefault().Seconds += seconds;

                Update();
                QueryManager.SavePlayer.Boosters(Player);
            }
        }

        public void Remove(BoosterType boosterType)
        {
            short boostedAttributeType = GetBoosterType((short)boosterType);

            if (boostedAttributeType != 0)
            {
                if (Boosters.ContainsKey(boostedAttributeType))
                    Boosters[boostedAttributeType].Remove(Boosters[boostedAttributeType].Where(x => x.Type == (short)boosterType).FirstOrDefault());

                if (Boosters[boostedAttributeType].Count == 0)
                    Boosters.Remove(boostedAttributeType);

                Update();
                QueryManager.SavePlayer.Boosters(Player);
            }
        }

        public void Update()
        {
            var boostedAttributes = new List<BoosterUpdateModule>();
            var visibleBoosterTypes = GetVisibleBoosterTypes();

            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.DAMAGE) && visibleBoosterTypes[(short)BoostedAttributeType.DAMAGE].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.DAMAGE), GetVisiblePercentage(BoostedAttributeType.DAMAGE), visibleBoosterTypes[(short)BoostedAttributeType.DAMAGE].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.SHIELD) && visibleBoosterTypes[(short)BoostedAttributeType.SHIELD].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.SHIELD), GetVisiblePercentage(BoostedAttributeType.SHIELD), visibleBoosterTypes[(short)BoostedAttributeType.SHIELD].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.MAXHP) && visibleBoosterTypes[(short)BoostedAttributeType.MAXHP].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.MAXHP), GetVisiblePercentage(BoostedAttributeType.MAXHP), visibleBoosterTypes[(short)BoostedAttributeType.MAXHP].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.REPAIR) && visibleBoosterTypes[(short)BoostedAttributeType.REPAIR].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.REPAIR), GetVisiblePercentage(BoostedAttributeType.REPAIR), visibleBoosterTypes[(short)BoostedAttributeType.REPAIR].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.HONOUR) && visibleBoosterTypes[(short)BoostedAttributeType.HONOUR].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.HONOUR), GetVisiblePercentage(BoostedAttributeType.HONOUR), visibleBoosterTypes[(short)BoostedAttributeType.HONOUR].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.EP) && visibleBoosterTypes[(short)BoostedAttributeType.EP].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.EP), GetVisiblePercentage(BoostedAttributeType.EP), visibleBoosterTypes[(short)BoostedAttributeType.EP].Select(x => new BoosterTypeModule(x)).ToList()));

            Player.SendCommand(AttributeBoosterUpdateCommand.write(boostedAttributes));
            Player.SendCommand(AttributeHitpointUpdateCommand.write(Player.CurrentHitPoints, Player.MaxHitPoints, Player.CurrentNanoHull, Player.MaxNanoHull));
            Player.SendCommand(AttributeShieldUpdateCommand.write(Player.CurrentShieldPoints, Player.MaxShieldPoints));

            //TODO dont need every time
            Player.SettingsManager.SendMenuBarsCommand();
        }

        public int GetPercentage(BoostedAttributeType boostedAttributeType)
        {
            var percentage = 0;

            if (Boosters.ContainsKey((short)boostedAttributeType))
                foreach (var booster in Boosters[(short)boostedAttributeType])
                    percentage += GetBoosterPercentage(booster.Type);

            return percentage;
        }

        private int GetVisiblePercentage(BoostedAttributeType boostedAttributeType)
        {
            return GetPercentage(boostedAttributeType) + GetCurrentMapStationBoostPercentage(boostedAttributeType);
        }

        private Dictionary<short, List<short>> GetVisibleBoosterTypes()
        {
            var visibleBoosters = Boosters.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Select(x => x.Type).Distinct().ToList());

            foreach (var stationBooster in GetCurrentMapStationBoosterTypes())
            {
                if (!visibleBoosters.ContainsKey(stationBooster.Key))
                    visibleBoosters[stationBooster.Key] = new List<short>();

                foreach (var boosterType in stationBooster.Value.Where(x => !visibleBoosters[stationBooster.Key].Contains(x)))
                    visibleBoosters[stationBooster.Key].Add(boosterType);
            }

            return visibleBoosters;
        }

        private Dictionary<short, List<short>> GetCurrentMapStationBoosterTypes()
        {
            var stationBoosters = new Dictionary<short, List<short>>();

            if (Player.Spacemap == null || Player.FactionId <= 0)
                return stationBoosters;

            var currentMapBoosters = new[]
            {
                new
                {
                    AttributeType = (short)BoostedAttributeType.HONOUR,
                    BoosterType = BoosterTypeModule.HONM_1,
                    Percentage = GetCurrentMapStationBoostPercentage(BoostedAttributeType.HONOUR)
                },
                new
                {
                    AttributeType = (short)BoostedAttributeType.EP,
                    BoosterType = BoosterTypeModule.XPM_1,
                    Percentage = GetCurrentMapStationBoostPercentage(BoostedAttributeType.EP)
                }
            };

            foreach (var stationBooster in currentMapBoosters.Where(x => x.Percentage > 0))
            {
                if (!stationBoosters.ContainsKey(stationBooster.AttributeType))
                    stationBoosters[stationBooster.AttributeType] = new List<short>();

                stationBoosters[stationBooster.AttributeType].Add(stationBooster.BoosterType);
            }

            return stationBoosters;
        }

        private int GetCurrentMapStationBoostPercentage(BoostedAttributeType boostedAttributeType)
        {
            if (Player.Spacemap == null || Player.FactionId <= 0)
                return 0;

            return GameManager.BattleStations.Values
                .Where(x => x != null
                    && !x.Destroyed
                    && x.FactionId == Player.FactionId
                    && x.Spacemap != null
                    && x.Spacemap.Id == Player.Spacemap.Id)
                .Sum(x => x.GetBoostPercentage(boostedAttributeType));
        }

        private short GetBoosterType(short boosterType)
        {
            short boostedAttributeType = 0;

            switch (boosterType)
            {
                case BoosterTypeModule.DMG_B01:
                case BoosterTypeModule.DMG_B02:
                    boostedAttributeType = (short)BoostedAttributeType.DAMAGE;
                    break;
                case BoosterTypeModule.SHD_B01:
                case BoosterTypeModule.SHD_B02:
                    boostedAttributeType = (short)BoostedAttributeType.SHIELD;
                    break;
                case BoosterTypeModule.HP_B01:
                case BoosterTypeModule.HP_B02:
                    boostedAttributeType = (short)BoostedAttributeType.MAXHP;
                    break;
                case BoosterTypeModule.REP_B01:
                case BoosterTypeModule.REP_B02:
                case BoosterTypeModule.REP_S01:
                    boostedAttributeType = (short)BoostedAttributeType.REPAIR;
                    break;
                case BoosterTypeModule.HON_B01:
                case BoosterTypeModule.HON_B02:
                case BoosterTypeModule.HON50:
                    boostedAttributeType = (short)BoostedAttributeType.HONOUR;
                    break;
                case BoosterTypeModule.EP_B01:
                case BoosterTypeModule.EP_B02:
                case BoosterTypeModule.EP50:
                    boostedAttributeType = (short)BoostedAttributeType.EP;
                    break;
            }

            return boostedAttributeType;
        }

        private int GetBoosterPercentage(short boosterTypeModule)
        {
            var percentage = 0;

            switch (boosterTypeModule)
            {
                case BoosterTypeModule.DMG_B01:
                case BoosterTypeModule.DMG_B02:
                case BoosterTypeModule.HP_B01:
                case BoosterTypeModule.HP_B02:
                case BoosterTypeModule.REP_B01:
                case BoosterTypeModule.REP_B02:
                case BoosterTypeModule.REP_S01:
                case BoosterTypeModule.HON_B01:
                case BoosterTypeModule.HON_B02:
                case BoosterTypeModule.EP_B01:
                case BoosterTypeModule.EP_B02:
                    percentage = 10;
                    break;
                case BoosterTypeModule.SHD_B01:
                case BoosterTypeModule.SHD_B02:
                    percentage = 25;
                    break;
                case BoosterTypeModule.HON50:
                case BoosterTypeModule.EP50:
                    percentage = 50;
                    break;
            }

            return percentage;
        }
    }
}
