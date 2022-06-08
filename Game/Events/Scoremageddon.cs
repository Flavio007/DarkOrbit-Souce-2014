using Ow.Game.Objects;
using Ow.Managers;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;




namespace Ow.Game.Events
{
    class Scoremageddon
    {

        public bool Active = false;
        public List<Portal> Portals = new List<Portal>();
        public int Limit = 20;
        public static int MAX_LIVES = 5; // Player max lives
        public static int MAX_COMBO = 10; // Max multiple
        public static int MAX_COMBO_TIME = 60; // Max combo time "Sec"

        public int Lives { get; set; }

        public int Combo { get; set; }

        public void Start()
        {
            if (Active) return;
            Active = true;
            
            foreach (var gameSession in GameManager.GameSessions.Values)
            {
                GameManager.SendPacketToAll("0|A|STD|Scoremageddon event started!");
                GameManager.SendPacketToAll($"0 | A | SCE | 5 |{MAX_LIVES}| 0 |{MAX_COMBO}| 5000 |{MAX_COMBO_TIME}|{100}");
                //GameManager.SendPacketToAll($"0 | A | SCE | 5 |{MAX_LIVES}| 0 |{MAX_COMBO}| 5000 |{MAX_COMBO_TIME}|{100}");
                //GameManager.SendPacketToAll("0|n|KSCE");
                //GameManager.SendPacketToAll("0|n|SCEKL|1");
                var player = gameSession.Player;
                player.SettingsManager.SendMenuBarsCommand();
                player.SendPacket($"0|A|SCE|5|{MAX_LIVES}|0|{MAX_COMBO}|5000|{MAX_COMBO_TIME}|{100}");
                //player.SendPacket($"0 | A | SCE | 5 |{MAX_LIVES}| 0 |{MAX_COMBO}| 5000 |{MAX_COMBO_TIME}|{ 100}");
                //player.SendPacket($"0|n|SCEL|{player.Id}|{Lives}|{MAX_LIVES}|{Combo}|{MAX_COMBO}");
            }
        }

        /*public void Destroy(Player target)
        {
            target.SendPacket($"0 | A | SCE | 5 |{MAX_LIVES}| 0 |{MAX_COMBO}| 5000 |{MAX_COMBO_TIME}|{ 100}");
        }*/


        public void Stop()
        {
            if (!Active) return;
            GameManager.SendPacketToAll("0|A|STD|Scoremageddon event ended!");
            Active = false;
            Limit = 20;
        }
    }
}
