using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class QuestWindowCloseRequest
    {
        public const short ID = 27259;
        public int QuestGiverId { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            QuestGiverId = QuestRequestCodec.ReadLeftRotatedInt(parser, 11);
            parser.readShort();
        }
    }
}
