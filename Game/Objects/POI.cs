using Ow.Game.Movements;
using Ow.Net.netty.commands;
using System.Collections.Generic;
using System;

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
        public List<int> ShapeCoordinates { get; set; }
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
            ShapeCoordinates = null;
            Inverted = inverted;
            TypeSpecification = poiTypeSpecification;
            Active = active;
        }

        public POI(string id, POITypes type, POIDesigns design, POIShapes shape, List<int> shapeCoordinates, bool active = true, bool inverted = false, string poiTypeSpecification = "")
        {
            Id = id;
            TypeId = (short)type;
            DesignId = (short)design;
            ShapeId = (short)shape;
            ShapeCords = new List<Position>();
            ShapeCoordinates = shapeCoordinates ?? new List<int>();
            for (var i = 0; i + 1 < ShapeCoordinates.Count; i += 2)
                ShapeCords.Add(new Position(ShapeCoordinates[i], ShapeCoordinates[i + 1]));
            if (shape == POIShapes.CIRCLE && ShapeCoordinates.Count == 3)
                ShapeCords.Add(new Position(ShapeCoordinates[2], ShapeCoordinates[2]));
            Inverted = inverted;
            TypeSpecification = poiTypeSpecification;
            Active = active;
        }

        public List<int> ShapeCordsToInts()
        {
            if (ShapeCoordinates != null)
                return new List<int>(ShapeCoordinates);

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

        public bool Contains(double x, double y)
        {
            var p = ShapeCordsToInts();
            if (p.Count < 3) return false;
            bool inside = false;
            if (Shape == POIShapes.CIRCLE)
            {
                double rx = p[2], ry = p.Count == 4 ? p[3] : rx;
                if (rx <= 0 || ry <= 0) return false;
                double dx = (x - p[0]) / rx, dy = (y - p[1]) / ry;
                inside = dx * dx + dy * dy <= 1;
            }
            else if (Shape == POIShapes.RECTANGLE && p.Count == 4)
                inside = x >= Math.Min(p[0], p[2]) && x <= Math.Max(p[0], p[2]) && y >= Math.Min(p[1], p[3]) && y <= Math.Max(p[1], p[3]);
            else
                for (int i = 0, j = p.Count / 2 - 1; i < p.Count / 2; j = i++)
                {
                    double ax = p[2*i], ay = p[2*i+1], bx = p[2*j], by = p[2*j+1];
                    if ((ay > y) != (by > y) && x < (bx-ax)*(y-ay)/(by-ay)+ax) inside = !inside;
                }
            return Inverted ? !inside : inside;
        }

        public bool Blocks(int faction)
        {
            if (!Active || Shape != POIShapes.RECTANGLE) return false;
            if (Type == POITypes.NO_ACCESS) return true;
            if (Type != POITypes.FACTION_NO_ACCESS) return false;
            return TypeSpecification == faction.ToString() || TypeSpecification == (faction == 1 ? "MMO" : faction == 2 ? "EIC" : faction == 3 ? "VRU" : "NONE");
        }

        // Split the complete segment at every boundary: endpoint-only checks allow crossing walls.
        public Position SlideDestination(Position start, Position end)
        {
            double limit=MovementLimit(start,end);
            if(limit>=1) return end;
            var p=ShapeCordsToInts();
            int left=p[0],right=p[0],top=p[1],bottom=p[1];
            for(int i=2;i+1<p.Count;i+=2){left=Math.Min(left,p[i]);right=Math.Max(right,p[i]);top=Math.Min(top,p[i+1]);bottom=Math.Max(bottom,p[i+1]);}
            double x=start.X+((double)end.X-start.X)*limit,y=start.Y+((double)end.Y-start.Y)*limit;
            double vertical=Math.Min(Math.Abs(x-left),Math.Abs(x-right)),horizontal=Math.Min(Math.Abs(y-top),Math.Abs(y-bottom));
            var slide=vertical<horizontal?new Position((int)Math.Round(x),end.Y):new Position(end.X,(int)Math.Round(y));
            limit=MovementLimit(start,slide);
            return new Position((int)Math.Round(start.X+((double)slide.X-start.X)*limit),(int)Math.Round(start.Y+((double)slide.Y-start.Y)*limit));
        }

        public double MovementLimit(Position start, Position end)
        {
            var p = ShapeCordsToInts();
            double dx = (double)end.X-start.X, dy = (double)end.Y-start.Y;
            var cuts = new List<double> { 0, 1 };
            if (Shape == POIShapes.CIRCLE && p.Count >= 3)
            {
                double rx=p[2], ry=p.Count == 4 ? p[3] : rx;
                if (rx <= 0 || ry <= 0) return 1;
                double x=(start.X-p[0])/rx, y=(start.Y-p[1])/ry, vx=dx/rx, vy=dy/ry;
                double a=vx*vx+vy*vy, b=2*(x*vx+y*vy), c=x*x+y*y-1, disc=b*b-4*a*c;
                if (a > 0 && disc >= 0) { cuts.Add((-b-Math.Sqrt(disc))/(2*a)); cuts.Add((-b+Math.Sqrt(disc))/(2*a)); }
            }
            else if (p.Count >= 4)
            {
                if (Shape == POIShapes.RECTANGLE && p.Count == 4)
                    p = new List<int> {p[0],p[1],p[2],p[1],p[2],p[3],p[0],p[3]};
                for (int i=0,j=p.Count/2-1;i<p.Count/2;j=i++)
                {
                    double ax=p[2*j], ay=p[2*j+1], ex=(double)p[2*i]-ax, ey=(double)p[2*i+1]-ay;
                    double det=dx*ey-dy*ex;
                    if (Math.Abs(det)<1e-9) continue;
                    double t=((ax-start.X)*ey-(ay-start.Y)*ex)/det;
                    double u=((ax-start.X)*dy-(ay-start.Y)*dx)/det;
                    if(u>=0 && u<=1) cuts.Add(t);
                }
            }
            cuts.RemoveAll(t => t < 0 || t > 1); cuts.Sort();
            bool escaped = !Contains(start.X,start.Y);
            for(int i=1;i<cuts.Count;i++)
            {
                double mid=(cuts[i-1]+cuts[i])/2;
                bool blocked=Contains(start.X+dx*mid,start.Y+dy*mid);
                if (!blocked) escaped=true;
                else if (escaped) return Math.Max(0,cuts[i-1]-2/Math.Max(1,Math.Sqrt(dx*dx+dy*dy)));
            }
            return 1;
        }
    }
}
