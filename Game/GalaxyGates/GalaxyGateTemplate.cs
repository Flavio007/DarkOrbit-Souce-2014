using Ow.Game.Movements;
using System.Collections.Generic;

namespace Ow.Game.GalaxyGates
{
    class GalaxyGateWaveTemplate
    {
        public int Id { get; set; }
        public int NpcId { get; set; }
        public int NpcCount { get; set; }
        public int Multiplier { get; set; }
        public int KeyNpc { get; set; }
        public int MinionsId { get; set; }
        public int MinionsCount { get; set; }
        public int MinionsMultiplier { get; set; }
    }

    class GalaxyGateTemplate
    {
        public int Id { get; set; }
        public string CodeName { get; set; }
        public string Name { get; set; }
        public int EntryMapId { get; set; }
        public int VisualMapId { get; set; }
        public Position EntryPortalPosition { get; set; }
        public int EntryMapIdMmo { get; set; }
        public int EntryMapIdEic { get; set; }
        public int EntryMapIdVru { get; set; }
        public Position EntryPortalPositionMmo { get; set; }
        public Position EntryPortalPositionEic { get; set; }
        public Position EntryPortalPositionVru { get; set; }
        public int EntryPortalGraphicId { get; set; }
        public int BaseMapId { get; set; }
        public Position BasePosition { get; set; }
        public int WavePortalGraphicId { get; set; }
        public int ExitPortalGraphicId { get; set; }
        public Position GateCenterPosition { get; set; }
        public Position GateCenterPositionMmo { get; set; }
        public Position GateCenterPositionEic { get; set; }
        public Position GateCenterPositionVru { get; set; }
        public string NpcSuffix { get; set; }
        public int MaxLives { get; set; }
        public List<GalaxyGateWaveTemplate> Waves { get; set; }

        public GalaxyGateTemplate()
        {
            CodeName = "";
            Name = "";
            NpcSuffix = "";
            EntryPortalPosition = new Position(0, 0);
            EntryPortalPositionMmo = new Position(0, 0);
            EntryPortalPositionEic = new Position(0, 0);
            EntryPortalPositionVru = new Position(0, 0);
            BasePosition = new Position(0, 0);
            GateCenterPosition = new Position(11100, 6500);
            GateCenterPositionMmo = new Position(11100, 6500);
            GateCenterPositionEic = new Position(11100, 6500);
            GateCenterPositionVru = new Position(11100, 6500);
            MaxLives = 5;
            Waves = new List<GalaxyGateWaveTemplate>();
        }
    }
}
