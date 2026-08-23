using System;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    // Nested quest modules are polymorphic. Keep their class ID and already encoded body intact.
    class QuestNettyModule
    {
        public short TypeId { get; private set; }
        public byte[] Body { get; private set; }

        public QuestNettyModule(short typeId, byte[] body)
        {
            TypeId = typeId;
            Body = body ?? new byte[0];
        }

        public static QuestNettyModule FromWire(byte[] wire)
        {
            if (wire == null || wire.Length < 2)
                return new QuestNettyModule(0, new byte[0]);

            var typeId = (short)((wire[0] << 8) | (wire[1] & 0xff));
            var body = new byte[wire.Length - 2];
            Buffer.BlockCopy(wire, 2, body, 0, body.Length);
            return new QuestNettyModule(typeId, body);
        }

        public byte[] ToWireBytes()
        {
            var module = new ByteArray(TypeId);
            module.write(Body);
            return module.Message.ToArray();
        }
    }
}
