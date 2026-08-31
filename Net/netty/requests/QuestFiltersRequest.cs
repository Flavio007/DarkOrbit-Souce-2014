using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class QuestFiltersRequest
    {
        public const short ID = 5503;

        public bool QuestsAvailableFilter { get; private set; }
        public bool QuestsUnavailableFilter { get; private set; }
        public bool QuestsCompletedFilter { get; private set; }
        public bool ChallengesAttemptedFilter { get; private set; }
        public bool ChallengesUnattemptedFilter { get; private set; }
        public bool QuestsLevelOrderDescending { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            // Request 5503 has a different wire order from the y2t settings module.
            QuestsLevelOrderDescending = parser.readBoolean();
            parser.readShort();
            ChallengesAttemptedFilter = parser.readBoolean();
            ChallengesUnattemptedFilter = parser.readBoolean();
            QuestsCompletedFilter = parser.readBoolean();
            QuestsUnavailableFilter = parser.readBoolean();
            QuestsAvailableFilter = parser.readBoolean();
        }
    }
}
