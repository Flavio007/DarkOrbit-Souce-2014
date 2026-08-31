using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class QuestGiverRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var request = new QuestGiverRequest();
            request.readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "QuestGiverRequest", QuestGiverRequest.ID);
            player?.Quests?.OpenQuestGiver(request.QuestGiverId);
        }
    }
}
