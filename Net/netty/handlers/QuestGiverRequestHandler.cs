using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class QuestGiverRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            new QuestGiverRequest().readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "QuestGiverRequest", QuestGiverRequest.ID);
            player?.Quests?.HandleQuestGiverClick();
        }
    }
}
