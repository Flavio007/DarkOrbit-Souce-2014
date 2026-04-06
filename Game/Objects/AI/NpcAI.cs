using Ow.Game.Movements;
using Ow.Managers;
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
        private static int NPC_ATTACK_RANGE = 450;
        private static int CHASE_REPATH_TOLERANCE = 125;
        private static int RANDOM_MOVE_RANGE = 250;
        public int RespawnX = 0;
        public int RespawnY = 0;
        public InstanceNpc VIP { set; get; }
        private static int MINION_RANGE = 200;
        private static int SWARM_RANGE = 700;

        public NpcAI(Npc npc) { Npc = npc; }

        public DateTime lastMovement = new DateTime();
        private DateTime nextDecisionTime = DateTime.MinValue;
        private int decisionIntervalMs = 0;

        public void TickAI()
        {
            var now = DateTime.Now;
            var ignoreDistanceForGalaxyGate = IsGalaxyGateNpc();
            if (nextDecisionTime == DateTime.MinValue)
            {
                decisionIntervalMs = Randoms.random.Next(850, 1350);
                nextDecisionTime = now.AddMilliseconds(Randoms.random.Next(0, decisionIntervalMs));
            }

            if (now < nextDecisionTime)
                return;

            if (Npc.Ship.Id == 80 || Npc.Ship.Id == 480 || Npc.Ship.Id == 880)
                AIOption = NpcAIOption.MOTHERSHIP_RANDOM;
            if (Npc.Ship.Id == 81 || Npc.Ship.Id == 481 || Npc.Ship.Id == 881)
                AIOption = NpcAIOption.MINION;
            if (Npc.Selected is Player user)
                if (user.UnderEmp > now)
                {
                    Npc.Selected = null;
                    Npc.Attacking = false;
                }

            if (TryFleeToGalaxyGateCorner())
            {
                lastMovement = now;
                decisionIntervalMs = Randoms.random.Next(850, 1350);
                nextDecisionTime = now.AddMilliseconds(decisionIntervalMs);
                return;
            }

            if (lastMovement.AddSeconds(1) < now)
            {
                switch (AIOption)
                {
                    case NpcAIOption.SEARCH_FOR_ENEMIES:
                        foreach (var players in Npc.InRangeCharacters.Values)
                        {
                            if (players is Player)
                            {
                                var player = players as Player;

                                if ((player.Storage.IsInSafeZone && player.Selected != Npc) || player.Storage.IsInDemilitarizedZone || player.Invisible || (!ignoreDistanceForGalaxyGate && Npc.Position.DistanceTo(player.Position) > Npc.RenderRange))
                                {
                                    Npc.Attacking = false;
                                    Npc.Selected = null;
                                    if (Npc.Ship.Id != 80 && Npc.Ship.Id != 481 && Npc.Ship.Id != 881)
                                        AIOption = NpcAIOption.SEARCH_FOR_ENEMIES;
                                }
                                else if (ignoreDistanceForGalaxyGate || Npc.Position.DistanceTo(player.Position) < Npc.AgroRange)
                                {
                                    if (Npc.Aggressive && !player.Storage.IsInSafeZone)
                                        Npc.Attacking = true;

                                    Npc.Selected = player;
                                    if (Npc.Ship.Id != 80 && Npc.Ship.Id != 481 && Npc.Ship.Id != 881)
                                        AIOption = NpcAIOption.FLY_TO_ENEMY;
                                    break;
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
                        if (Npc.Selected != null && Npc.Selected is Player && CanChasePlayer(Npc.Selected as Player, ignoreDistanceForGalaxyGate))
                        {
                            var player = Npc.Selected as Player;

                            TryMoveToEnemy(player);
                            AIOption = Npc.InRange(player, NPC_ATTACK_RANGE) ? NpcAIOption.WAIT_PLAYER_MOVE : NpcAIOption.FLY_TO_ENEMY;
                            if (player.Storage.IsInSafeZone && player.Selected != Npc)
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
                        if (Npc.Selected != null && Npc.Selected is Player && CanChasePlayer(Npc.Selected as Player, ignoreDistanceForGalaxyGate))
                        {
                            var player = Npc.Selected as Player;

                            if (!Npc.InRange(player, NPC_ATTACK_RANGE) || ShouldRefreshChaseDestination(player))
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

                                if (player.Storage.IsInDemilitarizedZone || player.Invisible || (!ignoreDistanceForGalaxyGate && Npc.Position.DistanceTo(player.Position) > Npc.RenderRange))
                                {
                                    Npc.Attacking = false;
                                    Npc.Selected = null;
                                }
                                else
                                {
                                    Npc.Selected = player;
                                    if (Npc.Aggressive)
                                        Npc.Attacking = true;
                                    break;
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

                                if (player.Storage.IsInDemilitarizedZone || player.Invisible || (!ignoreDistanceForGalaxyGate && Npc.Position.DistanceTo(player.Position) > Npc.RenderRange))
                                {
                                    Npc.Attacking = false;
                                    Npc.Selected = null;
                                }
                                else
                                {
                                    Npc.Selected = player;
                                    if (Npc.Aggressive)
                                        Npc.Attacking = true;
                                    break;
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

                lastMovement = now;
                decisionIntervalMs = Randoms.random.Next(850, 1350);
                nextDecisionTime = now.AddMilliseconds(decisionIntervalMs);
            }
        }

        private double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private bool TryFleeToGalaxyGateCorner()
        {
            if (!IsGalaxyGateNpc() || Npc.Spacemap == null)
                return false;

            if (Npc.MaxHitPoints <= 0)
                return false;

            var hpRatio = (double)Npc.CurrentHitPoints / Npc.MaxHitPoints;
            if (hpRatio > 0.20)
                return false;

            var upperLeft = new Position(0, 0);
            var lowerRight = new Position(Npc.Spacemap.Limits[1].X, Npc.Spacemap.Limits[1].Y);
            var distanceToUpperLeft = Npc.Position.DistanceTo(upperLeft);
            var distanceToLowerRight = Npc.Position.DistanceTo(lowerRight);
            var fleeTarget = distanceToUpperLeft <= distanceToLowerRight ? upperLeft : lowerRight;

            AIOption = NpcAIOption.SEARCH_FOR_ENEMIES;

            if (!Npc.Moving || Npc.Destination == null || Npc.Destination.DistanceTo(fleeTarget) > 250)
                Movement.Move(Npc, fleeTarget);

            return true;
        }

        private bool IsGalaxyGateNpc()
        {
            return Npc?.Spacemap != null && EventManager.GalaxyGate != null && EventManager.GalaxyGate.IsGalaxyGateMap(Npc.Spacemap.Id);
        }

        private bool CanChasePlayer(Player player, bool ignoreDistanceForGalaxyGate)
        {
            return player != null
                && !player.Storage.IsInDemilitarizedZone
                && !player.Invisible
                && (ignoreDistanceForGalaxyGate || Npc.Position.DistanceTo(player.Position) < Npc.RenderRange);
        }

        private bool ShouldRefreshChaseDestination(Player player)
        {
            var desiredPosition = GetChasePosition(player);

            return !Npc.Moving
                || Npc.Destination == null
                || Npc.Destination.DistanceTo(desiredPosition) > CHASE_REPATH_TOLERANCE;
        }

        private void TryMoveToEnemy(Player player)
        {
            if (player == null || Npc.InRange(player, NPC_ATTACK_RANGE - 25))
                return;

            if (!ShouldRefreshChaseDestination(player))
                return;

            Movement.Move(Npc, GetChasePosition(player));
        }

        private Position GetChasePosition(Player player)
        {
            if (player == null)
                return Npc.Position;

            var distanceToPlayer = Npc.Position.DistanceTo(player.Position);
            if (distanceToPlayer <= 0)
                return new Position(player.Position.X + ALIEN_DISTANCE_TO_USER, player.Position.Y);

            var directionX = Npc.Position.X - player.Position.X;
            var directionY = Npc.Position.Y - player.Position.Y;
            var scale = ALIEN_DISTANCE_TO_USER / distanceToPlayer;

            return new Position(
                player.Position.X + (int)Math.Round(directionX * scale),
                player.Position.Y + (int)Math.Round(directionY * scale));
        }
    }
}

