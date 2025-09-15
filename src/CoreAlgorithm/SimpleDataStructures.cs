using System;
using System.Collections.Generic;

namespace Magnetizing_FPG.CoreAlgorithm
{
    /// <summary>
    /// Simplified 3D point structure to replace Rhino.Geometry.Point3d
    /// </summary>
    public struct SimplePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public SimplePoint(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public SimplePoint(double x, double y) : this(x, y, 0) { }

        public static SimplePoint Origin => new SimplePoint(0, 0, 0);

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2}, {Z:F2})";
        }
    }

    /// <summary>
    /// Simplified vector structure to replace Rhino.Geometry.Vector3d
    /// </summary>
    public struct SimpleVector
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public SimpleVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public SimpleVector(double x, double y) : this(x, y, 0) { }

        public static SimpleVector ZAxis => new SimpleVector(0, 0, 1);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public static SimpleVector operator +(SimpleVector a, SimpleVector b)
        {
            return new SimpleVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static SimpleVector operator -(SimpleVector a, SimpleVector b)
        {
            return new SimpleVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static SimpleVector operator *(SimpleVector v, double scalar)
        {
            return new SimpleVector(v.X * scalar, v.Y * scalar, v.Z * scalar);
        }

        public static SimpleVector operator /(SimpleVector v, double scalar)
        {
            return new SimpleVector(v.X / scalar, v.Y / scalar, v.Z / scalar);
        }
    }

    /// <summary>
    /// Simplified rectangle structure to replace Rhino.Geometry.Rectangle3d and Curve
    /// </summary>
    public struct SimpleRectangle
    {
        public SimplePoint Center { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public SimpleRectangle(SimplePoint center, double width, double height)
        {
            Center = center;
            Width = width;
            Height = height;
        }

        public SimpleRectangle(double x, double y, double width, double height)
        {
            Center = new SimplePoint(x + width / 2, y + height / 2, 0);
            Width = width;
            Height = height;
        }

        public double Area => Width * Height;

        public SimpleBoundingBox GetBoundingBox()
        {
            return new SimpleBoundingBox(
                Center.X - Width / 2, Center.Y - Height / 2, Center.Z,
                Center.X + Width / 2, Center.Y + Height / 2, Center.Z
            );
        }

        public bool Contains(SimplePoint point)
        {
            var bounds = GetBoundingBox();
            return point.X >= bounds.MinX && point.X <= bounds.MaxX &&
                   point.Y >= bounds.MinY && point.Y <= bounds.MaxY;
        }

        public List<SimplePoint> GetCorners()
        {
            double halfWidth = Width / 2;
            double halfHeight = Height / 2;

            return new List<SimplePoint>
            {
                new SimplePoint(Center.X - halfWidth, Center.Y - halfHeight, Center.Z), // Bottom-left
                new SimplePoint(Center.X + halfWidth, Center.Y - halfHeight, Center.Z), // Bottom-right
                new SimplePoint(Center.X + halfWidth, Center.Y + halfHeight, Center.Z), // Top-right
                new SimplePoint(Center.X - halfWidth, Center.Y + halfHeight, Center.Z)  // Top-left
            };
        }
    }

    /// <summary>
    /// Simplified bounding box structure
    /// </summary>
    public struct SimpleBoundingBox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }

        public SimpleBoundingBox(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        public SimplePoint Center => new SimplePoint((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);
        public SimpleVector Diagonal => new SimpleVector(MaxX - MinX, MaxY - MinY, MaxZ - MinZ);
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public double Depth => MaxZ - MinZ;

        public SimplePoint Corner(bool minX, bool minY, bool minZ)
        {
            return new SimplePoint(
                minX ? MinX : MaxX,
                minY ? MinY : MaxY,
                minZ ? MinZ : MaxZ
            );
        }
    }

    /// <summary>
    /// Simplified curve/boundary structure
    /// </summary>
    public class SimpleBoundary
    {
        public List<SimplePoint> Points { get; set; }

        public SimpleBoundary()
        {
            Points = new List<SimplePoint>();
        }

        public SimpleBoundary(List<SimplePoint> points)
        {
            Points = points ?? new List<SimplePoint>();
        }

        public static SimpleBoundary CreateRectangle(double x, double y, double width, double height)
        {
            return new SimpleBoundary(new List<SimplePoint>
            {
                new SimplePoint(x, y, 0),
                new SimplePoint(x + width, y, 0),
                new SimplePoint(x + width, y + height, 0),
                new SimplePoint(x, y + height, 0),
                new SimplePoint(x, y, 0) // Close the rectangle
            });
        }

        public SimpleBoundingBox GetBoundingBox()
        {
            if (Points.Count == 0)
                return new SimpleBoundingBox(0, 0, 0, 0, 0, 0);

            double minX = Points[0].X, minY = Points[0].Y, minZ = Points[0].Z;
            double maxX = Points[0].X, maxY = Points[0].Y, maxZ = Points[0].Z;

            foreach (var point in Points)
            {
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
                if (point.Z < minZ) minZ = point.Z;
                if (point.Z > maxZ) maxZ = point.Z;
            }

            return new SimpleBoundingBox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        public bool Contains(SimplePoint point)
        {
            // Simple point-in-polygon test using ray casting algorithm
            // This is a simplified version - for complex shapes you might need more sophisticated algorithms

            var bounds = GetBoundingBox();
            if (point.X < bounds.MinX || point.X > bounds.MaxX ||
                point.Y < bounds.MinY || point.Y > bounds.MaxY)
                return false;

            // Ray casting algorithm
            int intersections = 0;
            for (int i = 0; i < Points.Count - 1; i++)
            {
                SimplePoint p1 = Points[i];
                SimplePoint p2 = Points[i + 1];

                if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    intersections++;
                }
            }

            return (intersections % 2) == 1;
        }

        public double GetArea()
        {
            // Calculate area using shoelace formula
            if (Points.Count < 3) return 0;

            double area = 0;
            int n = Points.Count - 1; // Exclude the closing point if it's the same as the first

            if (Points[0].X == Points[Points.Count - 1].X &&
                Points[0].Y == Points[Points.Count - 1].Y)
            {
                n = Points.Count - 1;
            }
            else
            {
                n = Points.Count;
            }

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += Points[i].X * Points[j].Y;
                area -= Points[j].X * Points[i].Y;
            }

            return Math.Abs(area) / 2.0;
        }
    }

    /// <summary>
    /// Simplified room instance for core algorithm
    /// </summary>
    public class SimpleRoom
    {
        public string RoomName { get; set; } = "";
        public double RoomArea { get; set; }
        public bool IsHall { get; set; } = false;
        public bool IsEntrance { get; set; } = false;
        public int RoomId { get; set; }

        public SimpleRoom() { }

        public SimpleRoom(string name, double area, bool isHall = false, bool isEntrance = false)
        {
            RoomName = name;
            RoomArea = area;
            IsHall = isHall;
            IsEntrance = isEntrance;
        }
    }

    /// <summary>
    /// Simplified house instance for core algorithm
    /// </summary>
    public class SimpleHouse
    {
        public string HouseName { get; set; } = "";
        public string FloorName { get; set; } = "";
        public SimpleBoundary Boundary { get; set; }
        public SimplePoint StartingPoint { get; set; }
        public bool TryRotateBoundary { get; set; } = false;
        public List<SimpleRoom> Rooms { get; set; }
        public List<string> AdjacencyStrings { get; set; }
        public int[,] AdjacencyArray { get; set; }

        public SimpleHouse()
        {
            Rooms = new List<SimpleRoom>();
            AdjacencyStrings = new List<string>();
            Boundary = new SimpleBoundary();
            StartingPoint = SimplePoint.Origin;
        }

        public void GenerateAdjacencyArray()
        {
            if (AdjacencyStrings.Count == 0) return;

            AdjacencyArray = new int[AdjacencyStrings.Count, 2];
            for (int i = 0; i < AdjacencyStrings.Count; i++)
            {
                var parts = AdjacencyStrings[i].Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0].Trim(), out int room1) &&
                        int.TryParse(parts[1].Trim(), out int room2))
                    {
                        AdjacencyArray[i, 0] = room1;
                        AdjacencyArray[i, 1] = room2;
                    }
                }
            }
        }
    }
}