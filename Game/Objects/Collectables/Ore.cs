using Ow.Game.Movements;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;

namespace Ow.Game.Objects.Collectables
{
    class Ore : Collectable
    {
        public int TypeId { get; }
        public Ores OreType { get; }
        public int Amount { get; }
        public string ResourceKey { get; }

        private readonly int minX;
        private readonly int maxX;
        private readonly int minY;
        private readonly int maxY;

        public Ore(int typeId, Ores oreType, Position position, Spacemap spacemap, bool respawnable, int minX, int maxX, int minY, int maxY, int amount = 1)
            : base(typeId, position, spacemap, respawnable, null)
        {
            TypeId = typeId;
            OreType = oreType;
            Amount = Math.Max(1, amount);
            ResourceKey = GetResourceKey(typeId);
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }

        public override void Reward(Player player)
        {
            if (player == null) return;

            var amount = Amount;
            amount += Maths.GetPercentage(amount, player.GetSkillPercentage("Tractor Beam I"));
            if (amount < 1) amount = 1;

            var applied = player.ChangeCargo(OreType, amount, false);
            if (applied > 0)
                player.SendPacket($"0|{ServerCommands.BOX_COLLECT_RESPONSE}|{ServerCommands.BOX_CONTENT_ORE}|{ResourceKey}|{applied}");
        }

        public override byte[] GetCollectableCreateCommand()
        {
            return CreateBoxCommand.write("FROM_SHIP", Hash, Position.Y, Position.X);
        }

        protected override Position GetRespawnPosition()
        {
            return RandomInBounds();
        }

        public Position RandomInBounds()
        {
            if (minX == maxX && minY == maxY)
                return new Position(minX, minY);

            var x = minX == maxX ? minX : Randoms.random.Next(minX, maxX);
            var y = minY == maxY ? minY : Randoms.random.Next(minY, maxY);
            return new Position(x, y);
        }

        public static bool TryResolveOre(int typeId, out Ores oreType, out string resourceKey)
        {
            oreType = Ores.DUMMY;
            resourceKey = null;

            switch (typeId)
            {
                case 1:
                    oreType = Ores.Prometium;
                    resourceKey = "ore_prometium";
                    return true;
                case 2:
                    oreType = Ores.Endurium;
                    resourceKey = "ore_endurium";
                    return true;
                case 3:
                    oreType = Ores.Terbium;
                    resourceKey = "ore_terbium";
                    return true;
                case 4:
                    oreType = Ores.Xenomit;
                    resourceKey = "ore_xenomit";
                    return true;
                case 5:
                    oreType = Ores.Prometid;
                    resourceKey = "ore_prometid";
                    return true;
                case 6:
                    oreType = Ores.Duranium;
                    resourceKey = "ore_duranium";
                    return true;
                case 7:
                    oreType = Ores.Promerium;
                    resourceKey = "ore_promerium";
                    return true;
                default:
                    return false;
            }
        }

        private string GetResourceKey(int typeId)
        {
            return TryResolveOre(typeId, out _, out var resourceKey) ? resourceKey : "ore_prometium";
        }
    }
}
