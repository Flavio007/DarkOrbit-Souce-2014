using Ow.Game;
using Ow.Game.Objects;
using System.Collections.Generic;

namespace Ow.Game.GalaxyGates
{
    class GalaxyGateInstance
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public int OwnerFactionId { get; set; }
        public int TemplateId { get; set; }
        public int MapId { get; set; }
        public Spacemap Spacemap { get; set; }
        public GalaxyGateTemplate Template { get; set; }
        public int CurrentWave { get; set; }
        public int LivesLeft { get; set; }
        public bool Completed { get; set; }
        public bool Failed { get; set; }
        public HashSet<string> DestroyedNpcSlots { get; set; }
        public Dictionary<int, string> RuntimeNpcToSlot { get; set; }
        public List<Portal> TemporaryPortals { get; set; }
        public Portal EntryPortal { get; set; }
        public bool WaveSpawnInProgress { get; set; }
        public bool PendingPersist { get; set; }

        public GalaxyGateInstance()
        {
            DestroyedNpcSlots = new HashSet<string>();
            RuntimeNpcToSlot = new Dictionary<int, string>();
            TemporaryPortals = new List<Portal>();
            CurrentWave = 1;
        }
    }
}
