using System.Collections.Generic;
using Ow.Net.netty.requests;

namespace Ow.Net.netty.handlers
{
    // Kept separate because the central dispatcher is outside this task's write scope.
    static class QuestHandlerRegistration
    {
        public static void AddTo(IDictionary<short, IHandler> commands)
        {
            commands.Add(QuestListRequest.ID, new QuestListRequestHandler());
            commands.Add(QuestGiverRequest.ID, new QuestGiverRequestHandler());
            commands.Add(QuestDetailsRequest.ID, new QuestDetailsRequestHandler());
            commands.Add(AcceptQuestRequest.ID, new AcceptQuestRequestHandler());
            commands.Add(AbortQuestRequest.ID, new AbortQuestRequestHandler());
            commands.Add(QuestFiltersRequest.ID, new QuestFiltersRequestHandler());
            commands.Add(QuestWindowCloseRequest.ID, new QuestWindowCloseRequestHandler());
        }
    }
}
