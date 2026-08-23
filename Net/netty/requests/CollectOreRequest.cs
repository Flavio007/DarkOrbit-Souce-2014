using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class CollectOreRequest
    {
        public const short ID = 24146;

        public string Hash { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            Hash = parser.readUTF();
            parser.readShort();
        }
    }
}
