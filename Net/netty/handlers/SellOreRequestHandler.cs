using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class SellOreRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            // The client emits ID 25203 from the Refining window's Refine button.
            // The legacy packet/class name is misleading; the payload is the target ore and amount.
            var read = new SellOreRequest();
            read.readCommand(bytes);

            var player = gameSession?.Player;
            PacketDebug.NotifyIncoming(player, "RefineOreRequest", SellOreRequest.ID);
            if (player == null || !OreRequestHandlerUtilities.TryGetOre(read.Stack, out var ore) ||
                !OreRequestHandlerUtilities.TryGetAmount(read.Stack, out var amount) ||
                !player.TryRefine(ore, amount))
            {
                if (player != null)
                    OreRequestHandlerUtilities.SendFailure(player, "Refinement failed: insufficient resources.");
                return;
            }

        }
    }
}
