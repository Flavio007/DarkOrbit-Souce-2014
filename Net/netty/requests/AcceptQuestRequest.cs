using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class AcceptQuestRequest
    {
        public const short ID = 23727;
        public int QuestId { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            QuestId = QuestRequestCodec.ReadLeftRotatedInt(parser, 12);
        }
    }
}
