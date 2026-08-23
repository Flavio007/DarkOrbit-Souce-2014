using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class QuestDetailsRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var request = new QuestDetailsRequest();
            request.readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "QuestDetailsRequest", QuestDetailsRequest.ID);
            player?.Quests?.SendModernQuestDetails(request.QuestId);
        }
    }
}
