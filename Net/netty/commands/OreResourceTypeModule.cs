using Ow.Game;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class OreResourceTypeModule
    {
        public const short ID = 5452;

        // Values used by main_current, which differ from the server Ores enum.
        public const short PROMETIUM = 0;
        public const short ENDURIUM = 1;
        public const short TERBIUM = 2;
        public const short XENOMIT = 3;
        public const short PROMETID = 4;
        public const short DURANIUM = 5;
        public const short PROMERIUM = 6;
        public const short SEPROM = 7;
        public const short PALLADIUM = 8;

        public short TypeValue { get; }

        public OreResourceTypeModule(short typeValue)
        {
            TypeValue = typeValue;
        }

        public static bool TryFromServerOre(Ores ore, out OreResourceTypeModule module)
        {
            module = null;
            short typeValue;
            switch (ore)
            {
                case Ores.Prometium: typeValue = PROMETIUM; break;
                case Ores.Endurium: typeValue = ENDURIUM; break;
                case Ores.Terbium: typeValue = TERBIUM; break;
                case Ores.Xenomit: typeValue = XENOMIT; break;
                case Ores.Prometid: typeValue = PROMETID; break;
                case Ores.Duranium: typeValue = DURANIUM; break;
                case Ores.Promerium: typeValue = PROMERIUM; break;
                case Ores.Seprom: typeValue = SEPROM; break;
                case Ores.Palladium: typeValue = PALLADIUM; break;
                default: return false;
            }

            module = new OreResourceTypeModule(typeValue);
            return true;
        }

        public static bool TryToServerOre(short typeValue, out Ores ore)
        {
            switch (typeValue)
            {
                case PROMETIUM: ore = Ores.Prometium; return true;
                case ENDURIUM: ore = Ores.Endurium; return true;
                case TERBIUM: ore = Ores.Terbium; return true;
                case XENOMIT: ore = Ores.Xenomit; return true;
                case PROMETID: ore = Ores.Prometid; return true;
                case DURANIUM: ore = Ores.Duranium; return true;
                case PROMERIUM: ore = Ores.Promerium; return true;
                case SEPROM: ore = Ores.Seprom; return true;
                case PALLADIUM: ore = Ores.Palladium; return true;
                default:
                    ore = Ores.DUMMY;
                    return false;
            }
        }

        public byte[] write()
        {
            var param1 = new ByteArray(ID);
            param1.writeShort(TypeValue);
            return param1.Message.ToArray();
        }

        public static OreResourceTypeModule readNested(ByteParser parser)
        {
            parser.readShort();
            return new OreResourceTypeModule(parser.readShort());
        }
    }
}
