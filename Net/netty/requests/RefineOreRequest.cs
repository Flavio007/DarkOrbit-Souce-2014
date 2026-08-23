using Ow.Net.netty.commands;
using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class RefineOreRequest
    {
        public const short ID = 14534;

        public RefinementTypeModule Source { get; private set; }
        public OreStackCommand Target { get; private set; }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);
            parser.readShort();
            parser.readShort();
            Source = RefinementTypeModule.readNested(parser);
            Target = OreStackCommand.readNested(parser);
        }
    }
}
