using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class AbortQuestRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var request = new AbortQuestRequest();
            request.readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "AbortQuestRequest", AbortQuestRequest.ID);
            player?.Quests?.HandleAbortQuest(request.QuestId);
        }
    }
}
