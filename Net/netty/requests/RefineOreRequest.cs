using Ow.Net.netty.commands;
using Ow.Utils;

namespace Ow.Net.netty.requests
{
    class RefineOreRequest
    {
        public const short ID = 14534;

        private const short TARGET_FIELD_MARKER = 29324;
        private const short SOURCE_FIELD_MARKER = -1628;

        public RefinementTypeModule Source { get; private set; }
        public OreStackCommand Target { get; private set; }
        public short TargetFieldMarker { get; private set; }
        public short SourceFieldMarker { get; private set; }
        public int BytesConsumed { get; private set; }
        public bool HasExpectedFieldMarkers
        {
            get
            {
                return TargetFieldMarker == TARGET_FIELD_MARKER &&
                       SourceFieldMarker == SOURCE_FIELD_MARKER;
            }
        }

        public void readCommand(byte[] bytes)
        {
            var parser = new ByteParser(bytes);

            // The client pcode writes this command in the following order:
            // target field marker, OreStackCommand, RefinementTypeModule,
            // source field marker. The two field markers are part of the
            // generated command envelope, not packet length or command IDs.
            TargetFieldMarker = parser.readShort();
            Target = OreStackCommand.readNested(parser);

            Source = RefinementTypeModule.readNested(parser);
            SourceFieldMarker = parser.readShort();
            BytesConsumed = parser.Buffer;
        }
    }
}
