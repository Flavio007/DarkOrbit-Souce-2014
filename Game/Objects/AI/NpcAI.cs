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
        private const int DefaultDecisionIntervalMinMs = 850;
        private const int DefaultDecisionIntervalMaxMs = 1350;
        private const int ChaseDecisionIntervalMinMs = 350;
        private const int ChaseDecisionIntervalMaxMs = 550;

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
        private bool carrierReturning;
        private bool carrierEngaged;
        private DateTime carrierIdleUntil;
        private DateTime carrierNextWander;
        private double carrierOffsetX, carrierOffsetY;
        private bool carrierOffsetReady;

        public void TickAI()
        {
            var now = DateTime.Now;
            if (Npc.Destroyed || !Npc.Spacemap.ContainsCharacter(Npc)) return;
            if (TickCarrierEscort(now)) return;
            if ((Npc.Attacking && Npc.Selected == null) || (Npc.Selected != null && (!(Npc.Selected is Player selectedPlayer) || !CanChasePlayer(selectedPlayer, HasUnlimitedChaseRange()))))
                Npc.Deselection();
            var ignoreDistanceForNpc = HasUnlimitedChaseRange();
            if (nextDecisionTime == DateTime.MinValue)
            {
                decisionIntervalMs = GetNextDecisionIntervalMs();
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

            if (TryFleeToCorner())
            {
                lastMovement = now;
                decisionIntervalMs = GetNextDecisionIntervalMs();
                nextDecisionTime = now.AddMilliseconds(decisionIntervalMs);
                return;
            }

            if (lastMovement.AddMilliseconds(GetMovementDecisionCooldownMs()) < now)
            {
                switch (AIOption)
                {
                    case NpcAIOption.SEARCH_FOR_ENEMIES:
                        AcquireTarget(ignoreDistanceForNpc, true);

                        if (!Npc.Moving && Npc.Selected == null)
                        {
                            int nextPosX = Randoms.random.Next(Npc.Spacemap.Id == 29 ? 40000 : 20000);
                            int nextPosY = Randoms.random.Next(Npc.Spacemap.Id == 29 ? 25600 : 12800);

                            Movement.Move(Npc, new Position(nextPosX, nextPosY));
                        }
                        break;
                    case NpcAIOption.FLY_TO_ENEMY:
                        if (Npc.Selected != null && Npc.Selected is Player && CanChasePlayer(Npc.Selected as Player, ignoreDistanceForNpc))
                        {
                            var player = Npc.Selected as Player;

                            TryMoveToEnemy(player);
                            AIOption = IsWithinAttackRange(player) ? NpcAIOption.WAIT_PLAYER_MOVE : NpcAIOption.FLY_TO_ENEMY;
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
                        if (Npc.Selected != null && Npc.Selected is Player && CanChasePlayer(Npc.Selected as Player, ignoreDistanceForNpc))
                        {
                            var player = Npc.Selected as Player;

                            if (!IsWithinAttackRange(player) || ShouldRefreshChaseDestination(player))
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
                        AcquireTarget(ignoreDistanceForNpc, false);
                        if (!Npc.Moving)
                        {
                            Movement.Move(Npc, new Position(RespawnX + Randoms.random.Next(-SWARM_RANGE, SWARM_RANGE), RespawnY + Randoms.random.Next(-SWARM_RANGE, SWARM_RANGE)));
                        }
                        break;
                    case NpcAIOption.ESCORT:
                        AcquireTarget(ignoreDistanceForNpc, false);
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
                decisionIntervalMs = GetNextDecisionIntervalMs();
                nextDecisionTime = now.AddMilliseconds(decisionIntervalMs);
            }
        }

        private bool TickCarrierEscort(DateTime now)
        {
            var mother = Npc.CarrierMother;
            if (mother == null) return false;
            if (mother.Destroyed || mother.Spacemap != Npc.Spacemap) { Npc.Deselection(); return true; }
            var settings = CarrierSettings.Current;
            var distance = Npc.Position.DistanceTo(mother.Position);
            var recent = Npc.LastCombatTime > Npc.CarrierOrderTime ? Npc.LastCombatTime : Npc.CarrierOrderTime;
            if (Npc.lastAttackTime > recent) recent = Npc.lastAttackTime;
            if (!carrierReturning && Npc.Attacking && Npc.Selected is Player player && CanChasePlayer(player, true) &&
                recent.AddMilliseconds(settings.MinionReturnMs) > now && distance < settings.MinionLeashRadius)
            {
                carrierEngaged = true;
                return false;
            }
            if (carrierEngaged) {
                carrierIdleUntil = recent.AddMilliseconds(settings.MinionReturnMs); carrierEngaged = false;
                Npc.Deselection();
            }
            if (!carrierReturning && carrierIdleUntil > now && distance < settings.MinionLeashRadius && !Npc.Attacking)
                return true;
            if (Npc.Attacking) carrierReturning = true;
            if (distance <= settings.MinionOrbitRadius) carrierReturning = false;
            Npc.Deselection();
            AIOption = NpcAIOption.MINION;
            // Only join fights close to the carrier, rather than chase a remote player forever.
            if (!carrierReturning && distance < settings.MinionOrbitRadius * 2 && mother.Attacking && mother.SelectedCharacter is Player target &&
                CanChasePlayer(target, true) && target.Position.DistanceTo(mother.Position) < settings.MinionLeashRadius * .6)
            {
                Npc.Selected = target; Npc.Attacking = true; Npc.CarrierOrderTime = now;
                AIOption = NpcAIOption.FLY_TO_ENEMY;
                return false;
            }
            if (!carrierOffsetReady || (!Npc.Moving && now >= carrierNextWander))
            {
                // Entity IDs are sequential: ID % 360 put an entire wave in the
                // same sector. Golden-angle sectors distribute even small waves.
                var angle = Npc.CarrierEscortSlot * 2.399963229728653 + (Randoms.random.NextDouble() - .5) * .6;
                var radius = settings.MinionOrbitRadius * (.7 + Randoms.random.NextDouble() * .3);
                carrierOffsetX = Math.Cos(angle) * radius;
                carrierOffsetY = Math.Sin(angle) * radius;
                carrierOffsetReady = true;
                carrierNextWander = now.AddMilliseconds(Randoms.random.Next(1200, 3000));
            }
            var destination = new Position(mother.Position.X + (int)carrierOffsetX, mother.Position.Y + (int)carrierOffsetY);
            if (lastMovement.AddMilliseconds(400) <= now &&
                (Npc.Destination == null || Npc.Destination.DistanceTo(destination) > 70 ||
                 (!Npc.Moving && Npc.Position.DistanceTo(destination) > 40)))
            {
                Movement.Move(Npc, destination);
                lastMovement = now;
            }
            return true;
        }

        private int GetNextDecisionIntervalMs()
        {
            return IsActivelyChasingTarget()
                ? Randoms.random.Next(ChaseDecisionIntervalMinMs, ChaseDecisionIntervalMaxMs)
                : Randoms.random.Next(DefaultDecisionIntervalMinMs, DefaultDecisionIntervalMaxMs);
        }

        private int GetMovementDecisionCooldownMs()
        {
            return IsActivelyChasingTarget() ? ChaseDecisionIntervalMinMs : 1000;
        }

        private bool IsActivelyChasingTarget()
        {
            return Npc != null
                && Npc.Selected is Player
                && (AIOption == NpcAIOption.FLY_TO_ENEMY || AIOption == NpcAIOption.WAIT_PLAYER_MOVE);
        }

        private double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private bool TryFleeToCorner()
        {
            if (Npc.CarrierMother != null || Npc.Carrier != null) return false;
            if (Npc.Spacemap == null || (!IsGalaxyGateNpc() &&
                (Npc.Ship.Id == 80 || Npc.Ship.Id == 480 || Npc.Ship.Id == 880 ||
                 AIOption == NpcAIOption.MINION || AIOption == NpcAIOption.ESCORT)))
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

        private bool HasUnlimitedChaseRange()
        {
            return IsGalaxyGateNpc() || (Npc != null && Npc.UseMapWideChaseRange && Npc.AllMapRange && Npc.Spacemap != null && Npc.Spacemap.Options.RangeDisabled);
        }

        public void Reset(Position position)
        {
            AIOption = NpcAIOption.SEARCH_FOR_ENEMIES;
            RespawnX = position.X;
            RespawnY = position.Y;
            lastMovement = DateTime.MinValue;
            nextDecisionTime = DateTime.MinValue;
        }

        public bool RetargetForDrawFire(Player player)
        {
            if (Npc.Destroyed || !Npc.Spacemap.ContainsCharacter(Npc) || !CanChasePlayer(player, HasUnlimitedChaseRange()))
                return false;

            // Deselection clears Attacking and the chase state. Taunt changes the
            // target directly and keeps special minion/escort/mothership roles.
            Npc.Selected = player;
            Npc.Attacking = true;
            if (AIOption == NpcAIOption.SEARCH_FOR_ENEMIES || AIOption == NpcAIOption.FLY_TO_ENEMY || AIOption == NpcAIOption.WAIT_PLAYER_MOVE)
                AIOption = NpcAIOption.FLY_TO_ENEMY;
            lastMovement = DateTime.MinValue;
            nextDecisionTime = DateTime.Now;
            return true;
        }

        private void AcquireTarget(bool ignoreDistance, bool chase)
        {
            if (Npc.Selected is Player current && CanChasePlayer(current, ignoreDistance))
            {
                if (chase && Npc.Attacking) AIOption = NpcAIOption.FLY_TO_ENEMY;
                return;
            }
            if (!Npc.Aggressive) return;
            var candidate = Npc.InRangeCharacters.Values.OfType<Player>()
                .Where(player => CanChasePlayer(player, ignoreDistance) && !player.Storage.IsInSafeZone
                    && (!chase || ignoreDistance || Npc.Position.DistanceTo(player.Position) < Npc.AgroRange))
                .OrderBy(player => Npc.Position.DistanceTo(player.Position)).FirstOrDefault();
            if (candidate == null) return;
            Npc.Selected = candidate;
            Npc.Attacking = true;
            if (chase) AIOption = NpcAIOption.FLY_TO_ENEMY;
        }

        private bool CanChasePlayer(Player player, bool ignoreDistanceForNpc)
        {
            return player != null && !player.Destroyed && Npc.Spacemap.ContainsCharacter(player)
                && player.UnderEmp <= DateTime.Now
                && (!player.Storage.IsInSafeZone || player.Selected == Npc)
                && !player.Storage.IsInDemilitarizedZone
                && !player.Invisible
                && (ignoreDistanceForNpc || Npc.Position.DistanceTo(player.Position) < Npc.RenderRange);
        }

        private bool IsWithinAttackRange(Player player)
        {
            return player != null && Npc != null && Npc.Position.DistanceTo(player.Position) <= NPC_ATTACK_RANGE;
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
            if (player == null || Npc == null || Npc.Position.DistanceTo(player.Position) <= NPC_ATTACK_RANGE - 25)
                return;

            if (!ShouldRefreshChaseDestination(player))
                return;

            Movement.Move(Npc, GetChasePosition(player));
        }

        private Position GetChasePosition(Player player)
        {
            if (player == null)
                return Npc.Position;

            var angleDegrees = ((Npc.Id * 37) + (player.Id * 17)) % 360;
            if (angleDegrees < 0)
                angleDegrees += 360;

            var angleRadians = DegreeToRadian(angleDegrees);

            return new Position(
                player.Position.X + (int)Math.Round(Math.Cos(angleRadians) * ALIEN_DISTANCE_TO_USER),
                player.Position.Y + (int)Math.Round(Math.Sin(angleRadians) * ALIEN_DISTANCE_TO_USER));
        }
    }
}

