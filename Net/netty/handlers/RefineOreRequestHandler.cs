using Ow.Game;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class RefineOreRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new RefineOreRequest();
            read.readCommand(bytes);

            var player = gameSession?.Player;
            PacketDebug.NotifyIncoming(player, "RefineOreRequest", RefineOreRequest.ID);
            if (player == null || read.Source == null ||
                !OreRequestHandlerUtilities.TryGetOre(read.Target, out var targetOre) ||
                !OreRequestHandlerUtilities.TryGetAmount(read.Target, out var amount))
            {
                if (player != null)
                    OreRequestHandlerUtilities.SendFailure(player, "Refinement failed: invalid request.");
                return;
            }

            var isUpgrade = read.Source.TypeValue >= RefinementTypeModule.LASER &&
                            read.Source.TypeValue <= RefinementTypeModule.SHIELD;
            var isRefinement = read.Source.TypeValue == RefinementTypeModule.REFINING;
            if (!isUpgrade && !isRefinement)
            {
                OreRequestHandlerUtilities.SendFailure(player, "Refinement failed: invalid request.");
                return;
            }

            var success = isUpgrade
                ? player.TryUpgrade(read.Source.TypeValue, targetOre, amount)
                : player.TryRefine(targetOre, amount);

            if (success)
                player.SendModernOreRefinementState();
            else
                OreRequestHandlerUtilities.SendFailure(player, isUpgrade
                    ? "Upgrade failed: incompatible ore or insufficient resources."
                    : "Refinement failed: insufficient resources.");
        }
    }
}
