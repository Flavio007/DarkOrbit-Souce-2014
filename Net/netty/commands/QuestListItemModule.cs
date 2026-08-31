using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestListItemModule
    {
        public const short ID = 31291;
        private const short WIRE_MARKER = 5563;

        public static byte[] write(int questId, int sortOrder, int minLevel, int priority,
            short statusType, string title, string description)
        {
            var packet = new ByteArray(ID);
            packet.write(QuestIconModule.write());
            packet.writeUTF(title);
            packet.writeUTF(description);
            packet.writeInt(0);
            packet.writeInt(QuestCommandCodec.RotateRight(sortOrder, 13));
            packet.writeInt(QuestCommandCodec.RotateRight(questId, 10));
            packet.writeInt(QuestCommandCodec.RotateLeft(priority, 16));
            packet.writeShort(WIRE_MARKER);
            packet.writeInt(0);
            packet.write(QuestStatusModule.write(statusType));
            packet.writeInt(QuestCommandCodec.RotateRight(minLevel, 7));
            return packet.Message.ToArray();
        }
    }
}
