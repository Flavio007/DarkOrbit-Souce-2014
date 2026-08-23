using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestRatingModule
    {
        public const short ID = 16508;
        private const short WIRE_MARKER = -10417;

        public static byte[] write(int id = 0, int rank = 0, string name = "", string comment = "")
        {
            var packet = new ByteArray(ID);
            packet.writeInt(QuestCommandCodec.RotateLeft(id, 5));
            packet.writeShort(WIRE_MARKER);
            packet.writeInt(QuestCommandCodec.RotateLeft(rank, 5));
            packet.writeUTF(name);
            packet.writeInt(QuestCommandCodec.RotateRight(0, 6));
            packet.writeUTF(comment);
            return packet.Message.ToArray();
        }
    }
}
