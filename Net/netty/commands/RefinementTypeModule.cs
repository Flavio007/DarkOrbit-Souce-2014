using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class RefinementTypeModule
    {
        public const short ID = 30075;

        public const short LASER = 0;
        public const short ROCKET = 1;
        public const short DRIVING = 2;
        public const short SHIELD = 3;

        public short TypeValue { get; }

        public RefinementTypeModule(short typeValue)
        {
            TypeValue = typeValue;
        }

        public byte[] write()
        {
            var param1 = new ByteArray(ID);
            param1.writeShort(TypeValue);
            return param1.Message.ToArray();
        }

        public static RefinementTypeModule readNested(ByteParser parser)
        {
            parser.readShort();
            return new RefinementTypeModule(parser.readShort());
        }
    }
}
