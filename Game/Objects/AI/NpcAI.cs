using Ow.Game.Movements;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects.AI
{
    class NpcAI
    {
        public Npc Npc { get; set; }

        public NpcAIOption AIOption = NpcAIOption.SEARCH_FOR_ENEMIES;
        private static int ALIEN_DISTANCE_TO_USER = 300;
        private static int RANDOM_MOVE_RANGE = 250;
        public int RespawnX = 0;
        public int RespawnY = 0;
        public InstanceNpc VIP { set; get; }
        private static int MINION_RANGE = 200;
        private static int SWARM_RANGE = 700;

        public NpcAI(Npc npc) { Npc = npc; }

        public DateTime lastMovement = new DateTime();

        public void TickAI()
        {
            if (Npc.Ship.Id == 80)
                AIOption = NpcAIOption.MOTHERSHIP_RANDOM;
            if (Npc.Ship.Id == 81)
                AIOption = NpcAIOption.MINION;
            if (Npc.Selected is Player user)
                if (user.UnderEmp > DateTime.Now)
                {
                    Npc.Selected = null;
                    Npc.Attacking = false;
                }
            if (lastMovement.AddSeconds(1) < DateTime.Now)
            {
                switch (AIOption)
                {
                    case NpcAIOption.SEARCH_FOR_ENEMIES:
                        foreach (var players in Npc.InRangeCharacters.Values)
                        {
                            if (players is Player)
                            {
                                var player = players as Player;

                                if ((player.Storage.IsInSafeZone && player.Selected != Npc) || player.Storage.IsInDemilitarizedZone || player.Invisible || Npc.Position.DistanceTo(player.Position) > Npc.RenderRange)
                                {
                                    Npc.Attacking = false;
                                    Npc.Selected = null;
                                    if (Npc.Ship.Id != 80)
                                        AIOption = NpcAIOption.SEARCH_FOR_ENEMIES;
                                }
                                else if(Npc.Position.DistanceTo(player.Position) < Npc.AgroRange)
                                {
                                    if (Npc.Ship.Aggressive && !player.Storage.IsInSafeZone)
                                        Npc.Attacking = true;

                                    Npc.Selected = player;
                                    if (Npc.Ship.Id != 80)
                                        AIOption = NpcAIOption.FLY_TO_ENEMY;
                                }
                            }
                        }

                        if (!Npc.Moving && Npc.Selected == null)
                        {
                            int nextPosX = Randoms.random.Next(Npc.Spacemap.Id == 29 ? 40000 : 20000);
                            int nextPosY = Randoms.random.Next(Npc.Spacemap.Id == 29 ? 25600 : 12800);

                            Movement.Move(Npc, new Position(nextPosX, nextPosY));
                        }
                        break;
                    case NpcAIOption.FLY_TO_ENEMY:
                        if (Npc.Selected != null && Npc.Selected is Player && !(Npc.Selected as Player).Storage.IsInDemilitarizedZone && Npc.Position.DistanceTo((Npc.Selected as Player).Position) < Npc.RenderRange)
                        {
                            var player = Npc.Selected as Player;

                            Movement.Move(Npc, Position.GetPosOnCircle(player.Position, ALIEN_DISTANCE_TO_USER));
                            AIOption = NpcAIOption.WAIT_PLAYER_MOVE;
                            if ((player.Storage.IsInSafeZone && player.Selected != Npc))
                            {
                                Npc.Attacking = false;
                                Npc.Selected = null;
                            }
                        } 
                        else
                        {
                            Npc.Attacking = false;
                            Npc.Selected = null;
                            AIOption = NpcAIOption.SEARCH_FOR_ENEMIES;
                        }
                        break;
                    case NpcAIOption.WAIT_PLAYER_MOVE:
                        if (Npc.Selected != null && Npc.Selected is Player && !(Npc.Selected as Player).Storage.IsInDemilitarizedZone)
                        {
                            var player = Npc.Selected as Player;

                            if (player.Moving && (!player.Storage.IsInSafeZone || (player.Selected == Npc)))
                                AIOption = NpcAIOption.FLY_TO_ENEMY;
                        }
                        else
                        {
                            Npc.Attacking = false;
                            Npc.Selected = null;
                            AIOption = NpcAIOption.SEARCH_FOR_ENEMIES;
                        }
                        break;
                    case NpcAIOption.MINION:
                        foreach (var players in Npc.InRangeCharacters.Values)
                            if (players is Player)
                            {
                                var player = players as Player;

                                if (player.Storage.IsInDemilitarizedZone || player.Invisible || Npc.Position.DistanceTo(player.Position) > Npc.RenderRange)
                                {
                                    Npc.Attacking = false;
                                    Npc.Selected = null;
                                }
                                else
                                {
                                    Npc.Selected = player;
                                    if (Npc.Ship.Aggressive)
                                        Npc.Attacking = true;
                                }
                            }
                        if (!Npc.Moving)
                        {
                            Movement.Move(Npc, new Position(RespawnX + Randoms.random.Next(-SWARM_RANGE, SWARM_RANGE), RespawnY + Randoms.random.Next(-SWARM_RANGE, SWARM_RANGE)));
                        }
                        break;
                    case NpcAIOption.ESCORT:
                        foreach (var players in Npc.InRangeCharacters.Values)
                            if (players is Player)
                            {
                                var player = players as Player;

                                if (player.Storage.IsInDemilitarizedZone || player.Invisible || Npc.Position.DistanceTo(player.Position) > Npc.RenderRange)
                                {
                                    Npc.Attacking = false;
                                    Npc.Selected = null;
                                }
                                else
                                {
                                    Npc.Selected = player;
                                    if (Npc.Ship.Aggressive)
                                        Npc.Attacking = true;
                                }
                            }
                        if (!Npc.Moving)
                        {
                            Movement.Move(Npc, new Position(VIP.Position.X + Randoms.random.Next(-MINION_RANGE, MINION_RANGE), VIP.Position.Y + Randoms.random.Next(-MINION_RANGE, MINION_RANGE))); ;
                        }
                        break;
                    case NpcAIOption.MOTHERSHIP_PATH:
                        //TODO
                        break;
                    case NpcAIOption.MOTHERSHIP_RANDOM:
                        if (!Npc.Moving)
                        {
                            Movement.Move(Npc, new Position(RespawnX + Randoms.random.Next(-RANDOM_MOVE_RANGE, RANDOM_MOVE_RANGE), RespawnY + Randoms.random.Next(-RANDOM_MOVE_RANGE, RANDOM_MOVE_RANGE)));
                        }
                        break;
                }

                lastMovement = DateTime.Now;
            }
        }

        private double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}
