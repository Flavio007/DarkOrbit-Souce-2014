using Ow.Game.Movements;
using Ow.Net.netty.commands;
using System.Collections.Generic;

namespace Ow.Game.Objects
{
    class POI
    {
        public string Id { get; }
        public short TypeId { get; set; }
        public short DesignId { get; set; }
        public short ShapeId { get; set; }
        public POITypes Type
        {
            get { return (POITypes)TypeId; }
            set { TypeId = (short)value; }
        }
        public POIDesigns Design
        {
            get { return (POIDesigns)DesignId; }
            set { DesignId = (short)value; }
        }
        public POIShapes Shape
        {
            get { return (POIShapes)ShapeId; }
            set { ShapeId = (short)value; }
        }
        public List<Position> ShapeCords { get; set; }
        public bool Inverted { get; set; }
        public string TypeSpecification { get; set; }
        public bool Active { get; set; }

        public POI(string id, POITypes type, POIDesigns design, POIShapes shape, List<Position> shapeCords, bool active = true, bool inverted = false, string poiTypeSpecification = "")
            : this(id, (short)type, (short)design, (short)shape, shapeCords, active, inverted, poiTypeSpecification)
        {
        }

        public POI(string id, short typeId, short designId, short shapeId, List<Position> shapeCords, bool active = true, bool inverted = false, string poiTypeSpecification = "")
        {
            Id = id;
            TypeId = typeId;
            DesignId = designId;
            ShapeId = shapeId;
            ShapeCords = shapeCords;
            Inverted = inverted;
            TypeSpecification = poiTypeSpecification;
            Active = active;
        }

        public List<int> ShapeCordsToInts()
        {
            List<int> cords = new List<int>();
            foreach (var cord in ShapeCords)
            {
                cords.Add(cord.X);
                cords.Add(cord.Y);
            }
            return cords;
        }

        public byte[] GetPOICreateCommand()
        {
            return MapAddPOICommand.write(Id, new POITypeModule(TypeId), TypeSpecification, new POIDesignModule(DesignId), ShapeId, ShapeCordsToInts(), Inverted, Active);
        }
    }
}
