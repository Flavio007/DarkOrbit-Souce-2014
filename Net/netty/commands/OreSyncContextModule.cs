using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class OreSyncContextModule
    {
        public const short ID = 23579;
        public const short STANDARD = 0;
        private const short WIRE_MARKER = -12033;

        public short TypeValue { get; }

        public OreSyncContextModule(short typeValue)
        {
            TypeValue = typeValue;
        }

        public byte[] write()
        {
            var param1 = new ByteArray(ID);
            param1.writeShort(WIRE_MARKER);
            param1.writeShort(TypeValue);
            return param1.Message.ToArray();
        }
    }
}
