using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestStatusModule
    {
        public const short ID = 24033;
        private const short BEFORE_TYPE_MARKER = 11654;
        private const short AFTER_TYPE_MARKER = -1488;

        public static byte[] write(short type)
        {
            var packet = new ByteArray(ID);
            packet.writeShort(BEFORE_TYPE_MARKER);
            packet.writeShort(type);
            packet.writeShort(AFTER_TYPE_MARKER);
            return packet.Message.ToArray();
        }
    }
}
