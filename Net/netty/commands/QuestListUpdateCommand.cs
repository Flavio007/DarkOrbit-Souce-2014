using System.Collections.Generic;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestListUpdateCommand
    {
        public const short ID = 16203;

        public int FirstValue { get; private set; }
        public int SecondValue { get; private set; }
        public bool HasMore { get; private set; }
        public List<QuestNettyModule> Quests { get; private set; }

        public QuestListUpdateCommand(int firstValue, int secondValue, bool hasMore, List<QuestNettyModule> quests)
        {
            FirstValue = firstValue;
            SecondValue = secondValue;
            HasMore = hasMore;
            Quests = quests ?? new List<QuestNettyModule>();
        }

        public byte[] write()
        {
            var packet = new ByteArray(ID);
            packet.writeInt(QuestCommandCodec.RotateRight(FirstValue, 10));
            packet.writeInt(QuestCommandCodec.RotateLeft(SecondValue, 6));
            packet.writeBoolean(HasMore);
            packet.writeInt(Quests.Count);
            QuestCommandCodec.WriteModules(packet, Quests);
            return packet.ToByteArray();
        }
    }
}
