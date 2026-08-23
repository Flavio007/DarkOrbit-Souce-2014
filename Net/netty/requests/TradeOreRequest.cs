using Ow.Net.netty.commands;
using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class TradeOreRequest
    {
        public const short ID = 5888;

        public OreStackCommand Stack { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            parser.readShort();
            parser.readShort();
            Stack = OreStackCommand.readNested(parser);
        }
    }
}
