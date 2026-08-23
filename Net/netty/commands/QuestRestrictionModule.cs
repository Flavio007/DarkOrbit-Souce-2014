using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestRestrictionModule
    {
        public const short ID = 15763;

        public static byte[] write()
        {
            var packet = new ByteArray(ID);
            packet.writeInt(0);
            packet.writeBoolean(false);
            packet.writeBoolean(false);
            packet.writeBoolean(false);
            packet.writeInt(QuestCommandCodec.RotateLeft(0, 7));
            packet.writeInt(QuestCommandCodec.RotateLeft(0, 6));
            return packet.Message.ToArray();
        }
    }
}
