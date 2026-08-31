using Ow.Utils;
using System.Collections.Generic;

namespace Ow.Net.netty.commands
{
    class QuestConditionProgressModule
    {
        public const short ID = 29001;
        private const short MARKER = 17018;

        public static byte[] write(uint type = 0,
            double maxValue = 0,
            IEnumerable<QuestNettyModule> maxEntries = null,
            double minValue = 0,
            IEnumerable<QuestNettyModule> minEntries = null)
        {
            var packet = new ByteArray(ID);
            packet.writeShort(unchecked((short)type));
            packet.writeShort(MARKER);
            packet.writeDouble(maxValue);

            var maxEntryList = Normalize(maxEntries);
            packet.writeInt(maxEntryList.Count);
            QuestCommandCodec.WriteModules(packet, maxEntryList);

            packet.writeDouble(minValue);
            var minEntryList = Normalize(minEntries);
            packet.writeInt(minEntryList.Count);
            QuestCommandCodec.WriteModules(packet, minEntryList);
            return packet.Message.ToArray();
        }

        private static List<QuestNettyModule> Normalize(IEnumerable<QuestNettyModule> modules)
        {
            var result = new List<QuestNettyModule>();
            if (modules != null)
            {
                foreach (var module in modules)
                {
                    if (module != null)
                    {
                        result.Add(module);
                    }
                }
            }
            return result;
        }
    }
}
