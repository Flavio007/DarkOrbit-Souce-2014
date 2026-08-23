using Ow.Game;
using Ow.Game.Objects;
using Ow.Game.Objects.Players;

namespace Ow.Net.netty.handlers
{
    abstract class QuestRequestHandlerBase
    {
        protected static Player GetPlayer(GameSession gameSession)
        {
            return gameSession == null ? null : gameSession.Player;
        }

        protected static void Refresh(Player player)
        {
            player?.Quests?.Sync();
        }

        protected static void NotifyRequest(Player player, string packetName, short packetId)
        {
            PacketDebug.NotifyIncoming(player, packetName, packetId);
        }
    }
}
