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
            QuestsLevelOrderDescending = parser.readBoolean();
            ChallengesAttemptedFilter = parser.readBoolean();
            ChallengesUnattemptedFilter = parser.readBoolean();
            QuestsAvailableFilter = parser.readBoolean();
            parser.readShort();
            QuestsCompletedFilter = parser.readBoolean();
            QuestsUnavailableFilter = parser.readBoolean();
        }
    }
}
