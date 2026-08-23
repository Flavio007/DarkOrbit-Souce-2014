using Ow.Net.netty.commands;
using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class SellOreRequest
    {
        public const short ID = 25203;

        public OreStackCommand Stack { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            parser.readShort();
            Stack = OreStackCommand.readNested(parser);
        }
    }
}
