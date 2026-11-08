using Ow.Game.Movements;
using Ow.Managers;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Collectables
{
    // A box owns its contents, independent of the shared ship definition and NPC respawn.
    sealed class CargoDrop
    {
        private readonly int[] remaining = new int[10];
        private bool bonusApplied;
        private static readonly Ores[] Priority = { Ores.Palladium, Ores.Seprom, Ores.Xenomit, Ores.Promerium, Ores.Duranium, Ores.Prometid, Ores.Terbium, Ores.Endurium, Ores.Prometium };
        public bool Empty => remaining.All(value => value == 0);

        public CargoDrop(Cargo source, double multiplier = 1)
        {
            source = source ?? new Cargo();
            var values = new[] { 0, source.Prometium, source.Endurium, source.Terbium, source.Prometid, source.Duranium, source.Promerium, source.Xenomit, source.Seprom, source.Palladium };
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier < 0) multiplier = 0;
            for (var i = 1; i < values.Length; i++)
                remaining[i] = (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0, values[i]) * multiplier));
        }

        public int Collect(int tractorBeamPercent, Func<Ores, int, int> grant)
        {
            // Apply a cargo skill once, including when a partially filled box is collected again.
            // Resource boosters affect map resources, not NPC cargo (wiki Cargo / Tractor Beam).
            if (!bonusApplied)
            {
                for (var i = 1; i < remaining.Length; i++)
                    remaining[i] = (int)Math.Min(int.MaxValue, remaining[i] + (long)remaining[i] * Math.Max(0, tractorBeamPercent) / 100);
                bonusApplied = true;
            }
            var collected = 0;
            foreach (var ore in Priority)
            {
                var amount = remaining[(int)ore];
                if (amount <= 0) continue;
                var applied = Math.Max(0, Math.Min(amount, grant(ore, amount)));
                remaining[(int)ore] -= applied;
                collected += applied;
            }
            return collected;
        }
    }

    class CargoBox : Collectable
    {
        private readonly CargoDrop contents;
        private bool questCollectionReported;
        private bool questTheftReported;
        private readonly int questOwnerFaction;
        private readonly int questOwnerShip;
        private bool Spaceball { get; set; }
        public bool NeedsCargoSpace => !Spaceball && !contents.Empty;
        public override bool CollectionComplete => Spaceball || contents.Empty;

        public CargoBox(Position position, Spacemap spacemap, bool respawnable, bool spaceball, Player toPlayer = null, CargoDrop contents = null, Player questOwner = null)
            : base(AssetTypeModule.BOXTYPE_FROM_SHIP, position, spacemap, respawnable, toPlayer)
        {
            Spaceball = spaceball;
            var owner = questOwner ?? toPlayer;
            questOwnerFaction = owner?.FactionId ?? 0;
            questOwnerShip = owner?.Ship?.Id ?? 0;
            this.contents = contents ?? new CargoDrop(null);
        }

        public override void Reward(Player player)
        {
            if (player == null) return;
            if (Spaceball)
            {
                player.ChangeData(DataType.EXPERIENCE, player.GetBoostedExperience(Randoms.random.Next(2500, 25600), true));
                player.ChangeData(DataType.HONOR, player.Ship.GetHonorBoost(Randoms.random.Next(25, 256)));
                player.ChangeData(DataType.URIDIUM, Randoms.random.Next(25, 256));
                player.ChangeData(DataType.CREDITS, Randoms.random.Next(150, 15000));
                return;
            }

            var received = contents.Collect(player.GetSkillPercentage("Tractor Beam I"), (ore, amount) =>
            {
                if (player.FreeCargo <= 0) return 0;
                var applied = player.ChangeCargo(ore, amount, true, false, false);
                if (applied > 0)
                {
                    player.Quests?.ReportProgress("collectOre", applied, targetOre: (int)ore);
                    player.Quests?.ReportProgress("collectCargoOre", applied, targetOre: (int)ore);
                    player.Achievements?.Report("ore", applied, (int)ore);
                }
                return applied;
            });
            if (received > 0)
            {
                if (!questCollectionReported)
                {
                    questCollectionReported = true;
                    player.Quests?.ReportProgress("collectCargoBox");
                }
                if (!questTheftReported && questOwnerFaction > 0 && questOwnerFaction != player.FactionId)
                {
                    questTheftReported = true;
                    player.Quests?.ReportProgress("stealCargoBox", targetShipId: questOwnerShip, targetFactionId: questOwnerFaction);
                }
                player.SendCargoStatus();
                player.SendOreCount();
                player.SendModernOreState();
                QueryManager.SavePlayer.Information(player);
            }
            if (!contents.Empty && player.FreeCargo <= 0) player.SendCargoFullWarning();
        }

        public override byte[] GetCollectableCreateCommand()
        {
            return CreateBoxCommand.write("FROM_SHIP", Hash, Position.Y, Position.X);
        }
    }
}
