using Newtonsoft.Json;
using Ow.Game.Objects;
using Ow.Game.Objects.Stations;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Utils;
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

    public class BoosterDropDefinition
    {
        public BoosterType Type { get; }
        public int Hours { get; }
        public double Chance { get; }

        public BoosterDropDefinition(BoosterType type, int hours, double chance)
        {
            Type = type;
            Hours = hours;
            Chance = chance;
        }
    }

    class BoosterManager : AbstractManager
    {
        public Dictionary<short, List<BoosterBase>> Boosters = new Dictionary<short, List<BoosterBase>>();

        public static readonly Dictionary<int, List<BoosterDropDefinition>> NpcDropChances = new Dictionary<int, List<BoosterDropDefinition>>
        {
            {
                109,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.08),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.08)
                }
            },
            {
                110,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.05)
                }
            },
            {
                111,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.04)
                }
            },
            {
                112,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.01),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.01)
                }
            },
            {
                113,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.02),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.02)
                }
            },
            {
                114,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.03)
                }
            },
            {
                115,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.04)
                }
            },
            {
                116,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.06),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.06)
                }
            },
            {
                117,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.05),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.05)
                }
            },
            {
                118,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.03),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.03)
                }
            },
            {
                119,
                new List<BoosterDropDefinition>
                {
                    new BoosterDropDefinition(BoosterType.DMG_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.SHD_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.HP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.EP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.HON_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.SREG_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.REP_B02, 1, 0.04),
                    new BoosterDropDefinition(BoosterType.RES_B02, 1, 0.04)
                }
            }
        };

        public bool HasVisibleBoosters => GetVisibleBoosterTypes().Count > 0;

        public BoosterManager(Player player) : base(player) { }

        private DateTime boosterTime = new DateTime();
        public void Tick()
        {
            if (boosterTime.AddSeconds(5) < DateTime.Now)
            {
                var changed = false;

                for (short i = 0; i < Boosters.ToList().Count; i++)
                {
                    var boosters = Boosters.ToList()[i].Value;

                    for (short k = 0; k < boosters.Count; k++)
                    {
                        boosters[k].Seconds -= 5;
                        changed = true;

                        if (boosters[k].Seconds <= 0)
                            Remove((BoosterType)boosters[k].Type);
                    }
                }

                if (changed)
                    QueryManager.SavePlayer.Boosters(Player);

                boosterTime = DateTime.Now;
            }
        }

        public void Load(Dictionary<short, List<BoosterBase>> boosters, bool updateClient = true)
        {
            Boosters = NormalizeBoosters(boosters);

            if (updateClient)
                Update();
        }

        public bool TryRewardNpcDrop(Npc npc)
        {
            if (npc?.Ship == null)
                return false;

            if (!NpcDropChances.TryGetValue(npc.Ship.Id, out var dropDefinitions) || dropDefinitions == null)
                return false;

            foreach (var dropDefinition in dropDefinitions.OrderByDescending(x => x.Chance))
            {
                if (Randoms.random.NextDouble() > dropDefinition.Chance)
                    continue;

                Add(dropDefinition.Type, dropDefinition.Hours);
                return true;
            }

            return false;
        }

        public static Dictionary<short, List<BoosterBase>> Merge(Dictionary<short, List<BoosterBase>> first, Dictionary<short, List<BoosterBase>> second)
        {
            var merged = new Dictionary<short, Dictionary<short, BoosterBase>>();

            MergeInto(merged, NormalizeBoosters(first));
            MergeInto(merged, NormalizeBoosters(second));

            return merged.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Values.OrderByDescending(x => x.Seconds).ToList());
        }

        public static bool TryParseBoosterType(string value, out BoosterType boosterType)
        {
            boosterType = BoosterType.DMG_B01;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmedValue = NormalizeBoosterIdentifier(value);

            if (short.TryParse(trimmedValue, out var numericType) && IsSupportedBoosterType(numericType))
            {
                boosterType = (BoosterType)numericType;
                return true;
            }

            if (Enum.TryParse(trimmedValue, true, out BoosterType parsedType) && IsSupportedBoosterType((short)parsedType))
            {
                boosterType = parsedType;
                return true;
            }

            return false;
        }

        private static string NormalizeBoosterIdentifier(string value)
        {
            return (value ?? string.Empty).Trim().Replace('-', '_');
        }

        public static bool IsSupportedBoosterType(short boosterType)
        {
            return GetBoostedAttributeType(boosterType) != 0;
        }

        private static Dictionary<short, List<BoosterBase>> NormalizeBoosters(Dictionary<short, List<BoosterBase>> boosters)
        {
            var normalized = new Dictionary<short, List<BoosterBase>>();

            if (boosters == null)
                return normalized;

            foreach (var entry in boosters)
            {
                foreach (var booster in entry.Value ?? new List<BoosterBase>())
                {
                    if (booster == null || booster.Seconds <= 0 || !IsSupportedBoosterType(booster.Type))
                        continue;

                    var boostedAttributeType = GetBoostedAttributeType(booster.Type);
                    if (!normalized.ContainsKey(boostedAttributeType))
                        normalized[boostedAttributeType] = new List<BoosterBase>();

                    var currentBooster = normalized[boostedAttributeType].FirstOrDefault(x => x.Type == booster.Type);
                    if (currentBooster == null)
                        normalized[boostedAttributeType].Add(new BoosterBase(booster.Type, booster.Seconds));
                    else
                        currentBooster.Seconds = Math.Max(currentBooster.Seconds, booster.Seconds);
                }
            }

            return normalized;
        }

        private static void MergeInto(Dictionary<short, Dictionary<short, BoosterBase>> target, Dictionary<short, List<BoosterBase>> source)
        {
            foreach (var entry in source)
            {
                if (!target.ContainsKey(entry.Key))
                    target[entry.Key] = new Dictionary<short, BoosterBase>();

                foreach (var booster in entry.Value)
                {
                    if (!target[entry.Key].TryGetValue(booster.Type, out var currentBooster))
                        target[entry.Key][booster.Type] = new BoosterBase(booster.Type, booster.Seconds);
                    else
                        currentBooster.Seconds = Math.Max(currentBooster.Seconds, booster.Seconds);
                }
            }
        }

        public void Add(BoosterType boosterType, int hours)
        {
            Player.SendPacket($"0|A|STM|booster_found|%BOOSTERNAME%|{boosterType.ToString()}|%HOURS%|{hours}");

            var seconds = (int)TimeSpan.FromHours(hours).TotalSeconds;
            short boostedAttributeType = GetBoostedAttributeType((short)boosterType);

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
            short boostedAttributeType = GetBoostedAttributeType((short)boosterType);

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
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.SHIELDRECHARGE) && visibleBoosterTypes[(short)BoostedAttributeType.SHIELDRECHARGE].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.SHIELDRECHARGE), GetVisiblePercentage(BoostedAttributeType.SHIELDRECHARGE), visibleBoosterTypes[(short)BoostedAttributeType.SHIELDRECHARGE].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.RESOURCE) && visibleBoosterTypes[(short)BoostedAttributeType.RESOURCE].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.RESOURCE), GetVisiblePercentage(BoostedAttributeType.RESOURCE), visibleBoosterTypes[(short)BoostedAttributeType.RESOURCE].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.HONOUR) && visibleBoosterTypes[(short)BoostedAttributeType.HONOUR].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.HONOUR), GetVisiblePercentage(BoostedAttributeType.HONOUR), visibleBoosterTypes[(short)BoostedAttributeType.HONOUR].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.EP) && visibleBoosterTypes[(short)BoostedAttributeType.EP].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.EP), GetVisiblePercentage(BoostedAttributeType.EP), visibleBoosterTypes[(short)BoostedAttributeType.EP].Select(x => new BoosterTypeModule(x)).ToList()));
            if (visibleBoosterTypes.ContainsKey((short)BoostedAttributeType.ABILITY_COOLDOWN) && visibleBoosterTypes[(short)BoostedAttributeType.ABILITY_COOLDOWN].Count >= 1)
                boostedAttributes.Add(new BoosterUpdateModule(new BoostedAttributeTypeModule(BoostedAttributeTypeModule.ABILITY_COOLDOWN), GetVisiblePercentage(BoostedAttributeType.ABILITY_COOLDOWN), visibleBoosterTypes[(short)BoostedAttributeType.ABILITY_COOLDOWN].Select(x => new BoosterTypeModule(x)).ToList()));

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
                    AttributeType = (short)BoostedAttributeType.DAMAGE,
                    BoosterType = BoosterTypeModule.DMGM_1,
                    Percentage = GetCurrentMapStationBoostPercentage(BoostedAttributeType.DAMAGE)
                },
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
            if (Player == null)
                return 0;

            return BattleStation.GetPlayerBoostPercentage(Player, boostedAttributeType);
        }

        public static short GetBoostedAttributeType(short boosterType)
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
                case BoosterTypeModule.SREG_B01:
                case BoosterTypeModule.SREG_B02:
                    boostedAttributeType = (short)BoostedAttributeType.SHIELDRECHARGE;
                    break;
                case BoosterTypeModule.RES_B01:
                case BoosterTypeModule.RES_B02:
                    boostedAttributeType = (short)BoostedAttributeType.RESOURCE;
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
                case BoosterTypeModule.CD_B01:
                case BoosterTypeModule.CD_B02:
                    boostedAttributeType = (short)BoostedAttributeType.ABILITY_COOLDOWN;
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
                case BoosterTypeModule.HON_B01:
                case BoosterTypeModule.HON_B02:
                case BoosterTypeModule.EP_B01:
                case BoosterTypeModule.EP_B02:
                    percentage = 10;
                    break;
                case BoosterTypeModule.REP_B01:
                case BoosterTypeModule.REP_B02:
                case BoosterTypeModule.REP_S01:
                case BoosterTypeModule.SHD_B01:
                case BoosterTypeModule.SHD_B02:
                case BoosterTypeModule.RES_B01:
                case BoosterTypeModule.RES_B02:
                case BoosterTypeModule.SREG_B01:
                case BoosterTypeModule.SREG_B02:
                    percentage = 25;
                    break;
                case BoosterTypeModule.HON50:
                case BoosterTypeModule.EP50:
                    percentage = 50;
                    break;
                case BoosterTypeModule.CD_B01:
                    percentage = 30;
                    break;
                case BoosterTypeModule.CD_B02:
                    percentage = 20;
                    break;
                case BoosterTypeModule.KAPPA_B01:
                    percentage = 5;
                    break;
            }

            return percentage;
        }
    }
}
