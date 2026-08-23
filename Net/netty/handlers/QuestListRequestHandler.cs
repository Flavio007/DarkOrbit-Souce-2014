using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class QuestListRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            new QuestListRequest().readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "QuestListRequest", QuestListRequest.ID);
            Refresh(player);
        }
    }
}
