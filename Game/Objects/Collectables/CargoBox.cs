using Ow.Game.Movements;
using Ow.Managers;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.Collectables
{
    class CargoBox : Collectable
    {
        public CargoBox(Position position, Spacemap spacemap, bool respawnable, bool spaceball, Player toPlayer = null) : base(AssetTypeModule.BOXTYPE_FROM_SHIP, position, spacemap, respawnable, toPlayer) { Spaceball = spaceball; }

        private bool Spaceball { get; set; }

        private Cargo cargo { get; set; }

        public override void Reward(Player player)
        {
            int experience = 0;
            int honor = 0;
            int uridium = 0;
            int credits = 0;
            int Prometium = 0;
            int Endurium = 0;
            int Terbium = 0;
            int Prometid = 0;
            int Duranium = 0;
            int Promerium = 0;
            int Xenomit = 0;
            int Seprom = 0;
            int Palladium = 0;

            if (Spaceball)
            {
                experience = player.Ship.GetExperienceBoost(Randoms.random.Next(2500, 25600));
                honor = player.Ship.GetHonorBoost(Randoms.random.Next(25, 256));
                uridium = Randoms.random.Next(25, 256);
                credits = Randoms.random.Next(150, 15000);
            }
            else
            {
                Prometium = 1;
                Endurium = 2;
                Terbium = 3;
                Prometid = 4;
                Duranium = 5;
                Promerium = 6;
                Xenomit = 7;
                Seprom = 8;
                Palladium = 9;
            }

            if(experience > 0)
                player.ChangeData(DataType.EXPERIENCE, experience);
            if(honor > 0)
                player.ChangeData(DataType.HONOR, honor);
            if(uridium > 0)
                player.ChangeData(DataType.URIDIUM, uridium);
            if(credits > 0)
                player.ChangeData(DataType.CREDITS, credits);
            if (Prometium > 0)
                player.ChangeCargo(Ores.Prometium, Prometium);
            if (Endurium > 0)
                player.ChangeCargo(Ores.Endurium, Endurium);
            if (Terbium > 0)
                player.ChangeCargo(Ores.Terbium, Terbium);
            if (Prometid > 0)
                player.ChangeCargo(Ores.Prometid, Prometid);
            if (Duranium > 0)
                player.ChangeCargo(Ores.Duranium, Duranium);
            if (Promerium > 0)
                player.ChangeCargo(Ores.Promerium, Promerium);
            if (Xenomit > 0)
                player.ChangeCargo(Ores.Xenomit, Xenomit);
            if (Seprom > 0)
                player.ChangeCargo(Ores.Seprom, Seprom);
            if (Palladium > 0)
                player.ChangeCargo(Ores.Palladium, Palladium);

        }

        public override byte[] GetCollectableCreateCommand()
        {
            return CreateBoxCommand.write("FROM_SHIP", Hash, Position.Y, Position.X);
        }
    }
}
