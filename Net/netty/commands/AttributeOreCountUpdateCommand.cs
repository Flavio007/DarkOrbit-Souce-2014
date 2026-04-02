using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class AttributeOreCountUpdateCommand
    {
        public const short ID = 106;

        public static byte[] write(int oreCount)
        {
            ByteArray param1 = new ByteArray(ID);
            param1.writeInt(oreCount >> 11 | oreCount << 21);
            return param1.ToByteArray();
        }
    }
}
