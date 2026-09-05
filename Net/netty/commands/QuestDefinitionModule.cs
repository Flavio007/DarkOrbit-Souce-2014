using Ow.Utils;
using System.Collections.Generic;

namespace Ow.Net.netty.commands
{
    class QuestDefinitionModule
    {
        public const short ID = 10794;

        public static byte[] write(int questId, string category, string titleKey)
        {
            return write(questId, category, titleKey,
                QuestNettyModule.FromWire(QuestRestrictionModule.write()),
                new List<QuestNettyModule>(),
                new List<QuestNettyModule>(),
                new List<QuestNettyModule>());
        }

        public static byte[] write(int questId, string category, string titleKey,
            QuestNettyModule restriction,
            IEnumerable<QuestNettyModule> icons,
            IEnumerable<QuestNettyModule> rewards,
            IEnumerable<QuestNettyModule> conditions)
        {
            var packet = new ByteArray(ID);
            packet.writeInt(QuestCommandCodec.RotateRight(questId, 15));
            packet.writeUTF(category);
            packet.write((restriction ?? QuestNettyModule.FromWire(QuestRestrictionModule.write())).ToWireBytes());
            var iconList = Normalize(icons);
            packet.writeInt(iconList.Count);
            QuestCommandCodec.WriteModules(packet, iconList);
            packet.writeUTF(titleKey);
            var rewardList = Normalize(rewards);
            packet.writeInt(rewardList.Count);
            QuestCommandCodec.WriteModules(packet, rewardList);
            var conditionList = Normalize(conditions);
            packet.writeInt(conditionList.Count);
            QuestCommandCodec.WriteModules(packet, conditionList);
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
