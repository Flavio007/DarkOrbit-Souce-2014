using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class QuestGiverRequest
    {
        public const short ID = 13518;
        public int QuestGiverId { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            parser.readShort();
            QuestGiverId = QuestRequestCodec.ReadRotatedInt(parser, 15);
        }
    }
}
