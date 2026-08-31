using Ow.Utils;
using System.Collections.Generic;

namespace Ow.Net.netty.commands
{
    class QuestConditionModule
    {
        public const short ID = 26664;

        public static byte[] write(int id = 0,
            IEnumerable<string> entries = null,
            uint type = 0,
            uint secondaryType = 0,
            double value = 0,
            bool flag = false,
            QuestNettyModule state = null,
            IEnumerable<QuestNettyModule> children = null)
        {
            var packet = new ByteArray(ID);
            packet.writeShort(unchecked((short)secondaryType));
            packet.writeShort(unchecked((short)type));

            var stateModule = state ?? QuestNettyModule.FromWire(QuestConditionStateModule.write());
            packet.write(stateModule.ToWireBytes());

            var childList = Normalize(children);
            packet.writeInt(childList.Count);
            QuestCommandCodec.WriteModules(packet, childList);

            packet.writeInt(QuestCommandCodec.RotateLeft(id, 10));

            var entryList = entries == null ? new List<string>() : new List<string>(entries);
            packet.writeInt(entryList.Count);
            foreach (var entry in entryList)
            {
                packet.writeUTF(entry);
            }

            packet.writeBoolean(flag);
            packet.writeDouble(value);
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
