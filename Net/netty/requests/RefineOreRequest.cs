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

            // The client writes the two nested commands immediately after
            // the request header: RefinementTypeModule followed by OreStack.
            // ByteParser has already consumed the packet length and the
            // request ID, so skipping two more shorts shifts the whole
            // request and makes every upgrade look invalid.
            Source = RefinementTypeModule.readNested(parser);
            Target = OreStackCommand.readNested(parser);
        }
    }
}
