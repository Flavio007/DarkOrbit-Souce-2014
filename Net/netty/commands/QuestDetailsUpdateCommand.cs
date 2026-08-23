using System.Collections.Generic;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestDetailsUpdateCommand
    {
        public const short ID = 26745;

        public QuestNettyModule Definition { get; private set; }
        public List<QuestNettyModule> Ratings { get; private set; }
        public QuestNettyModule SelectedRating { get; private set; }

        public QuestDetailsUpdateCommand(QuestNettyModule definition, List<QuestNettyModule> ratings,
                                         QuestNettyModule selectedRating)
        {
            Definition = definition ?? QuestNettyModule.FromWire(QuestDefinitionModule.write(0, "", ""));
            Ratings = ratings ?? new List<QuestNettyModule>();
            SelectedRating = selectedRating ?? QuestNettyModule.FromWire(QuestRatingModule.write());
        }

        public byte[] write()
        {
            var packet = new ByteArray(ID);
            packet.write(Definition.ToWireBytes());
            packet.writeInt(Ratings.Count);
            QuestCommandCodec.WriteModules(packet, Ratings);
            packet.writeShort(32712);
            packet.write(SelectedRating.ToWireBytes());
            return packet.ToByteArray();
        }
    }
}
