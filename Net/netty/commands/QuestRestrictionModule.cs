using Ow.Utils;
using System.Collections.Generic;

namespace Ow.Net.netty.commands
{
    class QuestRestrictionModule
    {
        public const short ID = 15763;

        public static byte[] write()
        {
            return write(0, false, false, 0, false, new List<QuestNettyModule>());
        }

        public static byte[] write(int id, bool active, bool flag, int level,
            bool ordered, IEnumerable<QuestNettyModule> conditions)
        {
            var packet = new ByteArray(ID);
            var conditionList = Normalize(conditions);
            packet.writeInt(conditionList.Count);
            QuestCommandCodec.WriteModules(packet, conditionList);
            packet.writeBoolean(active);
            packet.writeInt(QuestCommandCodec.RotateLeft(id, 7));
            packet.writeBoolean(ordered);
            packet.writeBoolean(flag);
            packet.writeInt(QuestCommandCodec.RotateLeft(level, 6));
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
