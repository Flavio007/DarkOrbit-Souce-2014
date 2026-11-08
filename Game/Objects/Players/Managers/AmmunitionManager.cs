using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ow.Managers.MySQLManager;

namespace Ow.Game.Objects.Players.Managers
{
    sealed class QuickBuyOffer
    {
        public readonly string Id, AmmoProperty, Name, Currency;
        public readonly int Amount;
        public readonly long Price;
        public QuickBuyOffer(string id,string property,string name,string currency,int amount,long price)
        { Id=id;AmmoProperty=property;Name=name;Currency=currency;Amount=amount;Price=price; }
        // Work on detached snapshots; nothing is delivered until persistence commits.
        public bool TryApply(DataBase data, Ammo ammo, out string error)
        {
            var balance=Currency=="credits"?data.credits:data.uridium;
            if(balance<Price){error=Currency=="credits"?"Creditos insuficientes.":"Uridium insuficiente.";return false;}
            var property=typeof(Ammo).GetProperty(AmmoProperty);
            int current=(int)property.GetValue(ammo,null);
            if(current<0||current>int.MaxValue-Amount){error="Limite de municao atingido.";return false;}
            property.SetValue(ammo,current+Amount,null);
            if(Currency=="credits")data.credits-=Price;else data.uridium-=Price;
            error=null;return true;
        }
    }
    class AmmunitionManager
    {
        // Same permanent ammunition and unit prices as the website shop (Functions.GetShop).
        public static readonly QuickBuyOffer[] QuickBuyOffers = {
            new QuickBuyOffer(LCB_10,"lcb10","LCB-10","credits",1000,10000),
            new QuickBuyOffer(MCB_25,"mcb25","MCB-25","uridium",1000,500),
            new QuickBuyOffer(MCB_50,"mcb50","MCB-50","uridium",1000,1000),
            new QuickBuyOffer(SAB_50,"sab50","SAB-50","uridium",1000,1000),
            new QuickBuyOffer(RSB_75,"rsb75","RSB-75","uridium",1000,5000),
            new QuickBuyOffer(R_310,"r310","R-310","credits",100,10000),
            new QuickBuyOffer(PLT_2026,"plt26","PLT-2026","credits",100,50000),
            new QuickBuyOffer(PLT_2021,"plt21","PLT-2021","uridium",100,500),
            new QuickBuyOffer(PLT_3030,"plt3030","PLT-3030","uridium",100,700)
        };
        public static QuickBuyOffer GetQuickBuyOffer(string id) { return QuickBuyOffers.FirstOrDefault(o=>o.Id==id); }
        public const string LCB_10 = "ammunition_laser_lcb-10";
        public const string MCB_25 = "ammunition_laser_mcb-25";
        public const string MCB_50 = "ammunition_laser_mcb-50";
        public const string UCB_100 = "ammunition_laser_ucb-100";
        public const string SAB_50 = "ammunition_laser_sab-50";
        public const string CBO_100 = "ammunition_laser_cbo-100";
        public const string RSB_75 = "ammunition_laser_rsb-75";
        public const string JOB_100 = "ammunition_laser_job-100";
        public const string RB_214 = "ammunition_laser_rb-214";
        public const string R_310 = "ammunition_rocket_r-310";
        public const string PLT_2026 = "ammunition_rocket_plt-2026";
        public const string PLT_2021 = "ammunition_rocket_plt-2021";
        public const string PLT_3030 = "ammunition_rocket_plt-3030";
        public const string PLD_8 = "ammunition_specialammo_pld-8";
        public const string DCR_250 = "ammunition_specialammo_dcr-250";
        public const string WIZ_X = "ammunition_specialammo_wiz-x";
        public const string BDR_1211 = "ammunition_rocket_bdr-1211";
        public const string BDR_1212 = "ammunition_rocket_bdr-1212";
        public const string R_IC3 = "ammunition_specialammo_r-ic3";
        public const string ROCKET_LAUNCHER_HSTRM_01 = "ammunition_rocketlauncher_hstrm-01";
        public const string ROCKET_LAUNCHER_UBR_100 = "ammunition_rocketlauncher_ubr-100";
        public const string ROCKET_LAUNCHER_ECO_10 = "ammunition_rocketlauncher_eco-10";
        public const string ROCKET_LAUNCHER_SAR_01 = "ammunition_rocketlauncher_sar-01";
        public const string ROCKET_LAUNCHER_SAR_02 = "ammunition_rocketlauncher_sar-02";
        public const string ROCKET_LAUNCHER_CBR = "ammunition_rocketlauncher_cbr";
        public const string ROCKET_LAUNCHER_BDR_1212 = "ammunition_rocketlauncher_bdr1212";

        [Obsolete("Use ROCKET_LAUNCHER_HSTRM_01")]
        public const string HSTRM_01 = ROCKET_LAUNCHER_HSTRM_01;
        [Obsolete("Use ROCKET_LAUNCHER_UBR_100")]
        public const string UBR_100 = ROCKET_LAUNCHER_UBR_100;
        [Obsolete("Use ROCKET_LAUNCHER_ECO_10")]
        public const string ECO_10 = ROCKET_LAUNCHER_ECO_10;
        [Obsolete("Use ROCKET_LAUNCHER_SAR_01")]
        public const string SAR_01 = ROCKET_LAUNCHER_SAR_01;
        [Obsolete("Use ROCKET_LAUNCHER_SAR_02")]
        public const string SAR_02 = ROCKET_LAUNCHER_SAR_02;
        [Obsolete("Use ROCKET_LAUNCHER_CBR")]
        public const string CBR = ROCKET_LAUNCHER_CBR;
        [Obsolete("Use ROCKET_LAUNCHER_BDR_1212")]
        public const string BDR1212 = ROCKET_LAUNCHER_BDR_1212;
        public const string EMP_01 = "ammunition_specialammo_emp-01";
        public const string FWX_COM = "ammunition_firework_fwx-com";
        public const string FWX_L = "ammunition_firework_fwx-l";
        public const string FWX_M = "ammunition_firework_fwx-m";
        public const string FWX_RZ = "ammunition_firework_fwx-rz";
        public const string FWX_S = "ammunition_firework_fwx-s";
        public const string ACM_01 = "ammunition_mine_acm-01";
        public const string DDM_01 = "ammunition_mine_ddm-01";
        public const string EMPM_01 = "ammunition_mine_empm-01";
        public const string SABM_01 = "ammunition_mine_sabm-01";
        public const string SLM_01 = "ammunition_mine_slm-01";
        public const string IM_01 = "ammunition_mine_im-01";
        public const string SMB_01 = "ammunition_mine_smb-01";
        public const string ISH_01 = "equipment_extra_cpu_ish-01";
        public const string ROCKET_LAUNCHER = "equipment_weapon_rocketlauncher_hst-2";
        public const string FIREWORK_IGNITE = "ammunition_firework_ignite";
        public const string CLK_XL = "equipment_extra_cpu_cl04k-xl";
    }
}
