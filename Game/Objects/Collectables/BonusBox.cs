using Ow.Game.Movements;
using Ow.Game.Objects.Players.Managers;
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
    class BonusBox : Collectable
    {
        private static readonly int[] NormalX1Ammo = { 50, 20, 10 };
        private static readonly int[] NormalX2Ammo = { 20, 10, 5 };
        private static readonly int[] NormalX3Ammo = { 20, 10, 5 };
        private static readonly int[] NormalJackpotCents = { 25, 50, 100 };
        private static readonly int[] NormalCredits = { 200, 500, 1000 };
        private static readonly int[] NormalUridium = { 20, 50, 100 };

        private static readonly int[] SpecialX1Ammo = { 75, 30, 15 };
        private static readonly int[] SpecialX2Ammo = { 8, 15, 30 };
        private static readonly int[] SpecialX3Ammo = { 8, 15, 30 };
        private static readonly int[] SpecialJackpotCents = { 38, 75, 150 };
        private static readonly int[] SpecialCredits = { 300, 750, 1500 };
        private static readonly int[] SpecialXenomit = { 25, 50, 75, 100, 150 };
        private static readonly int[] SpecialUridium = { 30, 75, 150 };

        public BonusBox(Position position, Spacemap spacemap, bool respawnable, Player toPlayer = null) : base(AssetTypeModule.BOXTYPE_BONUS_BOX, position, spacemap, respawnable, toPlayer) { }

        public override void Reward(Player player)
        {
            if (player == null) return;

            if (IsSpecialMap())
                RewardSpecialMap(player);
            else
                RewardNormalMap(player);
        }

        private void RewardNormalMap(Player player)
        {
            switch (Randoms.random.Next(0, 8))
            {
                case 0:
                    GiveAmmo(player, AmmunitionManager.LCB_10, Pick(NormalX1Ammo));
                    break;
                case 1:
                    GiveAmmo(player, AmmunitionManager.MCB_25, Pick(NormalX2Ammo));
                    break;
                case 2:
                    GiveAmmo(player, AmmunitionManager.MCB_50, Pick(NormalX3Ammo));
                    break;
                case 3:
                    GiveJackpot(player, Pick(NormalJackpotCents));
                    break;
                case 4:
                    GiveCredits(player, Pick(NormalCredits));
                    break;
                case 5:
                    GiveExtraEnergy(player, 1);
                    break;
                case 6:
                    GiveRepairCredits(player, 1);
                    break;
                case 7:
                    GiveUridium(player, Pick(NormalUridium));
                    break;
            }
        }

        private void RewardSpecialMap(Player player)
        {
            switch (Randoms.random.Next(0, 10))
            {
                case 0:
                    GiveAmmo(player, AmmunitionManager.LCB_10, Pick(SpecialX1Ammo));
                    break;
                case 1:
                    GiveAmmo(player, AmmunitionManager.MCB_25, Pick(SpecialX2Ammo));
                    break;
                case 2:
                    GiveAmmo(player, AmmunitionManager.MCB_50, Pick(SpecialX3Ammo));
                    break;
                case 3:
                    GiveJackpot(player, Pick(SpecialJackpotCents));
                    break;
                case 4:
                    GiveCredits(player, Pick(SpecialCredits));
                    break;
                case 5:
                    GiveXenomit(player, Pick(SpecialXenomit));
                    break;
                case 6:
                    GivePLT2021(player, Randoms.random.Next(6, 29));
                    break;
                case 7:
                    GiveExtraEnergy(player, 2);
                    break;
                case 8:
                    GiveRepairCredits(player, 2);
                    break;
                case 9:
                    GiveUridium(player, Pick(SpecialUridium));
                    break;
            }
        }

        private void GiveAmmo(Player player, string ammoId, int amount)
        {
            amount = ApplyBonusBoxScaling(player, amount);
            if (amount < 1) amount = 1;
            player.AddAmmo(ammoId, amount);
            QueryManager.SavePlayer.Ammo(player);
        }

        private void GivePLT2021(Player player, int amount)
        {
            amount = ApplyBonusBoxScaling(player, amount);
            if (amount < 1) amount = 1;
            player.AddAmmo(AmmunitionManager.PLT_2021, amount);
            QueryManager.SavePlayer.Ammo(player);
        }

        private void GiveCredits(Player player, int amount)
        {
            amount = ApplyBonusBoxScaling(player, amount);
            player.ChangeData(DataType.CREDITS, amount);
        }

        private void GiveUridium(Player player, int amount)
        {
            amount += Maths.GetPercentage(amount, player.GetSkillPercentage("Luck"));
            amount = ApplyBonusBoxScaling(player, amount);
            player.ChangeData(DataType.URIDIUM, amount);
        }

        private void GiveXenomit(Player player, int amount)
        {
            amount = ApplyBonusBoxScaling(player, amount);
            player.ChangeCargo(Ores.Xenomit, amount);
        }

        private void GiveJackpot(Player player, int jackpotCents)
        {
            jackpotCents = ApplyBonusBoxScaling(player, jackpotCents);
            if (jackpotCents < 1) jackpotCents = 1;
            player.ChangeData(DataType.JACKPOT, jackpotCents);
            //player.SendPacket($"0|A|STD| You received {jackpotCents / 100.0:0.##} jackpot dollars");
        }

        private void GiveExtraEnergy(Player player, int baseAmount)
        {
            var amount = ApplyBonusBoxScaling(player, baseAmount);
            if (amount < 1) amount = 1;
            if (player.Data == null) return;
            player.Data.extraEnergy += amount;
            QueryManager.SavePlayer.Information(player);
            player.SendPacket($"0|A|STD| You received {amount} galaxy gate extra energy");
        }

        private void GiveRepairCredits(Player player, int baseAmount)
        {
            var amount = ApplyBonusBoxScaling(player, baseAmount);
            if (amount < 1) amount = 1;
            if (player.Data == null) return;
            player.Data.repairCredits += amount;
            QueryManager.SavePlayer.Information(player);
            player.SendPacket($"0|A|STD| You received {amount} repair credits");
        }

        private int ApplyBonusBoxScaling(Player player, int amount)
        {
            if (amount <= 0) return 0;
            amount += Maths.GetPercentage(amount, player.GetSkillPercentage("Tractor Beam II"));
            return amount;
        }

        private bool IsSpecialMap()
        {
            return Spacemap != null && (Spacemap.Id == 13 || Spacemap.Id == 14 || Spacemap.Id == 15);
        }

        private int Pick(int[] values)
        {
            return values[Randoms.random.Next(0, values.Length)];
        }

        public override byte[] GetCollectableCreateCommand()
        {
            return CreateBoxCommand.write("BONUS_BOX", Hash, Position.Y, Position.X);
        }
    }
}

