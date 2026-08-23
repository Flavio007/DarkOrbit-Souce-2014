using Ow.Game;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    class QuestFiltersRequestHandler : QuestRequestHandlerBase, IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var request = new QuestFiltersRequest();
            request.readCommand(bytes);
            var player = GetPlayer(gameSession);
            NotifyRequest(player, "QuestFiltersRequest", QuestFiltersRequest.ID);
            player?.Quests?.ApplyModernFilters(request);
            Refresh(player);
        }
    }
}
