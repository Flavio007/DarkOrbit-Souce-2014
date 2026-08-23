using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class QuestListRequest
    {
        public const short ID = 28872;

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            parser.readShort();
        }
    }
}
