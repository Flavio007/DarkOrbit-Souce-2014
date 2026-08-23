using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class QuestWindowCloseRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            new QuestWindowCloseRequest().readCommand(bytes);
            NotifyRequest(GetPlayer(gameSession), "QuestWindowCloseRequest", QuestWindowCloseRequest.ID);
        }
    }
}
