using Ow.Game.Objects;
using Ow.Game.Ticks;
using Ow.Managers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Game.Events
{
    class Scoremageddon : Tick
    {
        public bool Active=false;
        public List<Portal> Portals=new List<Portal>();
        public int Limit=20;
        public static int MAX_LIVES=5,MAX_COMBO=10,MAX_COMBO_TIME=60;
        public int Lives{get;set;}
        public int Combo{get;set;}
        private readonly object sync=new object();
        private readonly Dictionary<int,ScoremageddonState> participants=new Dictionary<int,ScoremageddonState>();
        public void Start(){
            lock(sync){if(Active)return;Active=true;participants.Clear();}
            GameManager.SendPacketToAll("0|A|STD|Scoremageddon event started!");
            foreach(var session in GameManager.GameSessions.Values){session.Player.SettingsManager.SendMenuBarsCommand();SendState(session.Player);}
            Program.TickManager.AddTick(this);
        }
        private ScoremageddonState State(Player p){ScoremageddonState value;if(!participants.TryGetValue(p.Id,out value)){value=new ScoremageddonState();participants[p.Id]=value;}return value;}
        private void Send(Player p,ScoremageddonState state){
            p.Storage.ScoreEventLives=state.Lives;p.Storage.ScoreEventCombo=state.Combo;p.Storage.ScoreEventPoints=state.Points;
            p.SendPacket($"0|A|SCE|{state.Lives}|{MAX_LIVES}|{state.Combo}|{MAX_COMBO}|{state.Points}|{MAX_COMBO_TIME}|{state.RemainingPercent(DateTime.UtcNow)}");
            GameManager.SendPacketToAll($"0|n|SCEL|{p.Id}|{state.Lives}|{MAX_LIVES}|{state.Combo}|{MAX_COMBO}");
        }
        public void SendState(Player player){lock(sync){if(!Active||player==null)return;Send(player,State(player));
            foreach(var entry in participants)if(entry.Key!=player.Id){var state=entry.Value;player.SendPacket($"0|n|SCEL|{entry.Key}|{state.Lives}|{MAX_LIVES}|{state.Combo}|{MAX_COMBO}");}
        }}
        public void RecordKill(Player killer,Player victim){
            if(killer==null||victim==null||killer.Id==victim.Id||killer.FactionId==victim.FactionId)return;
            lock(sync){if(!Active)return;var v=State(victim);if(v.Lives<=0)return;v.Die();Send(victim,v);
                var k=State(killer);if(k.Lives>0){k.Kill(DateTime.UtcNow);Send(killer,k);}}
        }
        public void Tick(){lock(sync){if(!Active)return;foreach(var entry in participants){var value=entry.Value;if(value.Combo>0&&value.RemainingPercent(DateTime.UtcNow)==0){value.Combo=0;var p=GameManager.GetPlayerById(entry.Key);if(p!=null)Send(p,value);}}}}
        public void Stop(){
            lock(sync){if(!Active)return;Active=false;}
            Program.TickManager.RemoveTick(this);GameManager.SendPacketToAll("0|n|KSCE");GameManager.SendPacketToAll("0|A|STD|Scoremageddon event ended!");
            foreach(var session in GameManager.GameSessions.Values)session.Player.SettingsManager.SendMenuBarsCommand();
        }
    }
}
