using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class TradeOreRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new TradeOreRequest();
            read.readCommand(bytes);

            var player = gameSession?.Player;
            PacketDebug.NotifyIncoming(player, "TradeOreRequest", TradeOreRequest.ID);
            if (player == null || !OreRequestHandlerUtilities.TryGetOre(read.Stack, out var ore) ||
                !OreRequestHandlerUtilities.TryGetAmount(read.Stack, out var amount) ||
                !player.TrySellOre(ore, amount))
            {
                if (player != null)
                    OreRequestHandlerUtilities.SendFailure(player, "Unable to trade ore.");
                return;
            }

        }
    }
}
