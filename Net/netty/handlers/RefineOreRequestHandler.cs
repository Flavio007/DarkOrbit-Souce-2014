using Ow.Game;
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
            if (player == null || !OreRequestHandlerUtilities.TryGetOre(read.Target, out var targetOre) ||
                !OreRequestHandlerUtilities.TryGetAmount(read.Target, out var amount) ||
                !player.TryRefine(targetOre, amount))
            {
                if (player != null)
                    OreRequestHandlerUtilities.SendFailure(player, "Refinement failed: insufficient resources.");
                return;
            }

        }
    }
}
