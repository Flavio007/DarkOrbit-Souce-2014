using System;
namespace Ow.Game.Events
{
    class ScoremageddonState
    {
        public int Lives=Scoremageddon.MAX_LIVES,Combo,Points;
        public DateTime LastKill=DateTime.MinValue;
        public void Kill(DateTime now){if((now-LastKill).TotalSeconds>=Scoremageddon.MAX_COMBO_TIME)Combo=0;Combo=Math.Min(Scoremageddon.MAX_COMBO,Combo+1);Points=checked(Points+100*Combo);LastKill=now;}
        public void Die(){Lives=Math.Max(0,Lives-1);Combo=0;LastKill=DateTime.MinValue;}
        public int RemainingPercent(DateTime now){return Combo==0?0:(int)Math.Max(0,Math.Min(100,100*(1-(now-LastKill).TotalSeconds/Scoremageddon.MAX_COMBO_TIME)));}
    }
}
