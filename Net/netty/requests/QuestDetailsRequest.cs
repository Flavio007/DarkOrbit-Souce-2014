using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class QuestDetailsRequest
    {
        public const short ID = 21421;
        public int QuestId { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            QuestId = QuestRequestCodec.ReadRotatedInt(parser, 16);
        }
    }
}
