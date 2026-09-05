using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestStatusModule
    {
        public const short ID = 24033;
        private const short FIRST_MARKER = 11654;
        private const short SECOND_MARKER = -1488;

        public static byte[] write(short type)
        {
            var packet = new ByteArray(ID);
            // The client skips two shorts before reading the quest status.
            packet.writeShort(FIRST_MARKER);
            packet.writeShort(SECOND_MARKER);
            packet.writeShort(type);
            return packet.Message.ToArray();
        }
    }
}
