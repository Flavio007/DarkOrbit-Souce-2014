using Ow.Game;
using Ow.Game.Objects;
using Ow.Net.netty.commands;
using System;

namespace Ow.Net.netty.handlers
{
    static class OreRequestHandlerUtilities
    {
        public static bool TryGetOre(OreStackCommand stack, out Ores ore)
        {
            ore = Ores.DUMMY;
            if (stack == null || stack.Resource == null)
                return false;

            return OreResourceTypeModule.TryToServerOre(stack.Resource.TypeValue, out ore);
        }

        public static bool TryGetAmount(OreStackCommand stack, out int amount)
        {
            amount = 0;
            if (stack == null || double.IsNaN(stack.Count) || double.IsInfinity(stack.Count))
                return false;

            var truncated = Math.Truncate(stack.Count);
            if (truncated <= 0 || truncated > int.MaxValue)
                return false;

            amount = (int)truncated;
            return true;
        }

        public static void SendModernOreCount(Player player)
        {
            player.SendCommand(OreCountUpdateCommand.write(
                player.Prometium,
                player.Endurium,
                player.Terbium,
                player.Xenomit,
                player.Prometid,
                player.Duranium,
                player.Promerium,
                player.Seprom,
                player.Palladium));
        }

        public static void SendFailure(Player player, string message)
        {
            player.SendPacket($"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.SERVER_MSG}|{message}");
        }
    }
}
