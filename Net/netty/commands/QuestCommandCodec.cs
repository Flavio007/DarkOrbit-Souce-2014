using Ow.Utils;

namespace Ow.Net.netty.commands
{
    static class QuestCommandCodec
    {
        public static int RotateLeft(int value, int shift)
        {
            var input = (uint)value;
            return unchecked((int)((input << shift) | (input >> (32 - shift))));
        }

        public static int RotateRight(int value, int shift)
        {
            var input = (uint)value;
            return unchecked((int)((input >> shift) | (input << (32 - shift))));
        }

        public static void WriteModules(ByteArray packet, System.Collections.Generic.IEnumerable<QuestNettyModule> modules)
        {
            foreach (var module in modules)
                packet.write(module.ToWireBytes());
        }
    }
}
