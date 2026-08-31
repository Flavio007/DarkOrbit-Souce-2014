using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class AcceptQuestRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var request = new AcceptQuestRequest();
            request.readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "AcceptQuestRequest", AcceptQuestRequest.ID);
            player?.Quests?.HandleAcceptQuest(request.QuestId);
        }
    }
}
