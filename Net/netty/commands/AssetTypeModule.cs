using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class AssetTypeModule
    {
        public static short ID = 15758;

        public const short varG4T = 56;
        public const short ASTEROID = 35;
        public const short BOXTYPE_MINI_PUMPKIN = 6;
        public const short WRECK = 32;
        public const short BOXTYPE_SUMMERGAMES_2011 = 19;
        public const short varX1u = 49;
        public const short BOXTYPE_ITALY = 11;
        public const short varDg = 44;
        public const short BATTLESTATION = 36;
        public const short SECTOR_CONTROL_EXIT_POINT = 40;
        public const short BASE_COMPANY = 46;
        public const short FIREWORK_SIZE_LARGE = 29;
        public const short BOXTYPE_TURKEY = 7;
        public const short varuK = 59;
        public const short BOOSTER_STATION = 38;
        public const short varR1B = 51;
        public const short varBa = 55;
        public const short BEACON_VRU_FOR_EIC = 26;
        public const short HANGAR_HOME = 48;
        public const short varW14 = 47;
        public const short GENERIC_SHIP = 39;
        public const short BOXTYPE_STAR_BIG = 8;
        public const short var34E = 58;
        public const short SECTOR_CONTROL_SECTOR_ZONE = 41;
        public const short BEACON_MMO_FOR_VRU = 22;
        public const short BOXTYPE_CARNIVAL = 17;
        public const short ORE_TRADE_STATION = 50;
        public const short BOXTYPE_STAR_SMALL = 9;
        public const short BOXTYPE_FROM_SPACEBALL = 12;
        public const short FIREWORK_SIZE_SMALL = 27;
        public const short HEALING_POD = 33;
        public const short BEACON_EIC_FOR_MMO = 23;
        public const short BEACON_EIC_FOR_VRU = 24;
        public const short BOXTYPE_FUELCAN = 18;
        public const short BEACON_VRU_FOR_MMO = 25;
        public const short SATELLITE = 37;
        public const short BOXTYPE_GIANT_PUMPKIN = 5;
        public const short vara2M = 45;
        public const short BEACON_MMO_FOR_EIC = 21;
        public const short FIREWORK_SIZE_MEDIUM = 28;
        public const short BOXTYPE_UNIQUE_COLLECTABLE = 4;
        public const short vart2r = 54;
        public const short BOXTYPE_GIFT_BOX = 16;
        public const short BOXTYPE_ALIEN_EGG = 3;
        public const short BILLBOARD_ASTEROID = 30;
        public const short BOXTYPE_INDEPENDANCE_POLAND = 15;
        public const short BOXTYPE_BONUS_BOX = 2;
        public const short RELAY_STATION = 31;
        public const short vartN = 43;
        public const short var23C = 52;
        public const short BOXTYPE_VUELTA_TSHIRT = 13;
        public const short SECTOR_CONTROL_BATTLEMASTER = 42;
        public const short BOXTYPE_PIRATE_BOOTY = 20;
        public const short BOXTYPE_FROM_SHIP = 1;
        public const short BOXTYPE_CRESCENT_STAR = 14;
        public const short BOXTYPE_FROM_SHIP_BLOCKED = 0;
        public const short var42v = 57;
        public const short REPAIR_STATION = 53;
        public const short QUESTGIVER = 34;
        public const short BOXTYPE_FLOWER = 10;

        public static class BoxTypeNames
        {
            public const string FROM_SHIP_BLOCKED = "FROM_SHIP_BLOCKED";
            public const string FROM_SHIP = "FROM_SHIP";
            public const string FROM_SPACEBALL = "FROM_SPACEBALL";
            public const string FUELCAN = "FUELCAN";
            public const string ICY_BOX = "ICY_BOX";
            public const string BONUS_BOX = "BONUS_BOX";
            public const string RAZER_BOX = "RAZER_BOX";
            public const string ONE_PLAYER = "ONE_PLAYER";
            public const string ALIEN_EGG = "ALIEN_EGG";
            public const string GIANT_PUMPKIN = "GIANT_PUMPKIN";
            public const string MINI_PUMPKIN = "MINI_PUMPKIN";
            public const string TURKEY_CARGO = "TURKEY_CARGO";
            public const string STAR_SMALL = "STAR_SMALL";
            public const string FLOWER = "FLOWER";
            public const string MAY_FIRST = "MAY_FIRST";
            public const string ITALIAN_FLAG = "ITALIAN_FLAG";
            public const string TURKISH_FLAG = "TURKISH_FLAG";
            public const string POLISH_FLAG = "POLISH_FLAG";
            public const string GIFT_BOXES = "GIFT_BOXES";
            public const string CARNIVAL = "CARNIVAL";
            public const string SOLAR_CLASH = "SOLAR_CLASH";
            public const string PET_ONLY_BOX = "PET_ONLY_BOX";
            public const string PIRATE_BOOTY = "PIRATE_BOOTY";
            public const string PIRATE_BOOTY_GOLD = "PIRATE_BOOTY_GOLD";
            public const string HUNGARIAN_FLAG = "HUNGARIAN_FLAG";
            public const string ST_PATRICKS = "ST_PATRICKS";
            public const string BRAZIL = "BRAZIL";
            public const string FRENCH_FLAG = "FRENCH_FLAG";
            public const string RUSSIAN_FLAG = "RUSSIAN_FLAG";
            public const string CZECH_FLAG = "CZECH_FLAG";
            public const string USA_FLAG = "USA_FLAG";
            public const string PIRATE_BOOTY_RED = "PIRATE_BOOTY_RED";
            public const string PIRATE_BOOTY_BLUE = "PIRATE_BOOTY_BLUE";
            public const string BASTILLE_DAY = "BASTILLE_DAY";
            public const string ID_MEXICO = "ID_MEXICO";
            public const string HITAC = "HITAC";
            public const string HITAC_MINION = "HITAC_MINION";
            public const string GERMAN_FLAG = "GERMAN_FLAG";
            public const string SPANISH_FLAG = "SPANISH_FLAG";
            public const string CANDY_CARGO = "CANDY_CARGO";
            public const string BIRTHDAY = "BIRTHDAY";
            public const string TREASURE = "TREASURE";
            public const string BOLLERWAGEN = "BOLLERWAGEN";
            public const string CONFLICT_RISING = "CONFLICT_RISING";
            public const string PIRATE_BOOTY_SILVER = "PIRATE_BOOTY_SILVER";
            public const string BRITISH_FLAG = "BRITISH_FLAG";
            public const string FOOTBALL_BOX = "FOOTBALL_BOX";
            public const string DEMANER_BOX = "DEMANER_BOX";
            public const string PLAGUE_BOX = "PLAGUE_BOX";
            public const string HELIX_BOX = "HELIX_BOX";
            public const string UBA_EXPO_BOX = "UBA_EXPO_BOX";
        }

        public short typeValue = 0;

        public AssetTypeModule(short TypeValue)
        {
            typeValue = TypeValue;
        }

        public byte[] write()
        {
            ByteArray param1 = new ByteArray(ID);
            param1.writeShort(-2023);
            param1.writeShort(-17738);
            param1.writeShort(this.typeValue);
            return param1.Message.ToArray();
        }
    }
}
