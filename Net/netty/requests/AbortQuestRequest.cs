using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class AbortQuestRequest
    {
        public const short ID = 21988;
        public int QuestId { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            QuestId = QuestRequestCodec.ReadRotatedInt(parser, 2);
            parser.readShort();
        }
    }
}
