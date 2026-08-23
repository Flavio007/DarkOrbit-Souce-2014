using Ow.Game;
using Ow.Game.Objects.Collectables;
using Ow.Net.netty.requests;
using System.Linq;

namespace Ow.Net.netty.handlers
{
    class CollectOreRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new CollectOreRequest();
            read.readCommand(bytes);

            var player = gameSession?.Player;
            PacketDebug.NotifyIncoming(player, "CollectOreRequest", CollectOreRequest.ID);
            if (player?.Spacemap == null || string.IsNullOrEmpty(read.Hash))
                return;

            var ore = player.Spacemap.Objects.Values
                .OfType<Ore>()
                .FirstOrDefault(x => x.Hash == read.Hash);
            ore?.Collect(player);
        }
    }
}
