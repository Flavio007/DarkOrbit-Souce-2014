using Ow.Game.Events;
using Ow.Game.GalaxyGates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Managers
{
    class EventManager
    {
        public static JackpotBattle JackpotBattle { get; set; }
        public static Spaceball Spaceball { get; set; }
        public static UltimateBattleArena UltimateBattleArena { get; set; }
        public static Scoremageddon Scoremageddon { get; set; }
        public static InvasionGate InvasionGate { get; set; }
        public static GalaxyGateManager GalaxyGate { get; set; }

        public static void InitiateEvents()
        {
            JackpotBattle = new JackpotBattle();
            Spaceball = new Spaceball();
            UltimateBattleArena = new UltimateBattleArena();
            Scoremageddon = new Scoremageddon();
            InvasionGate = new InvasionGate();
            GalaxyGate = new GalaxyGateManager();
            GalaxyGate.Initialize();
        }
    }
}
