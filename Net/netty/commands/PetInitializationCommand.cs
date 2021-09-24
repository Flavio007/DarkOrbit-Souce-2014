using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class PetInitializationCommand
    {
        public const short ID = 9174;

        public static byte[] write(bool var1, bool var2, bool var3)
        {
            var param1 = new ByteArray(ID);
            param1.writeBoolean(var1);
            param1.writeBoolean(var2);
            param1.writeBoolean(var3);
            return param1.ToByteArray();
        }
    }
}
