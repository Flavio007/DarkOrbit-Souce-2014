using Ow.Utils;

namespace Ow.Net.netty.requests
{
    static class QuestRequestCodec
    {
        public static int ReadRotatedInt(ByteParser parser, int rightShift)
        {
            var value = (uint)parser.readInt();
            return unchecked((int)((value >> rightShift) | (value << (32 - rightShift))));
        }

        public static int ReadLeftRotatedInt(ByteParser parser, int leftShift)
        {
            var value = (uint)parser.readInt();
            return unchecked((int)((value << leftShift) | (value >> (32 - leftShift))));
        }
    }
}
