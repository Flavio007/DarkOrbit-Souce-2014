using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class QuestWindowCloseRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var request = new QuestWindowCloseRequest();
            request.readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "QuestWindowCloseRequest", QuestWindowCloseRequest.ID);
            player?.Quests?.CloseQuestGiver(request.QuestGiverId);
        }
    }
}
