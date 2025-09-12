using System;
using System.Collections.Generic;
using System.Linq;

namespace Magnetizing_FPG.Core.Domain
{
    /// <summary>
    /// Represents a 2D point.
    /// </summary>
    public struct Point2d : IEquatable<Point2d>
    {
        public static readonly Point2d Origin = new Point2d(0, 0);

        public double X { get; set; }
        public double Y { get; set; }

        public Point2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(Point2d other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Point2d Add(Vector2d vector)
        {
            return new Point2d(X + vector.X, Y + vector.Y);
        }

        public Vector2d Subtract(Point2d other)
        {
            return new Vector2d(X - other.X, Y - other.Y);
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2})";
        }

        public bool Equals(Point2d other)
        {
            return Math.Abs(X - other.X) < 1e-10 && Math.Abs(Y - other.Y) < 1e-10;
        }

        public override bool Equals(object obj)
        {
            return obj is Point2d other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(Point2d left, Point2d right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point2d left, Point2d right)
        {
            return !left.Equals(right);
        }

        public static Point2d operator +(Point2d point, Vector2d vector)
        {
            return point.Add(vector);
        }

        public static Vector2d operator -(Point2d left, Point2d right)
        {
            return left.Subtract(right);
        }
    }

    /// <summary>
    /// Represents a 2D vector.
    /// </summary>
    public struct Vector2d : IEquatable<Vector2d>
    {
        public static readonly Vector2d Zero = new Vector2d(0, 0);
        public static readonly Vector2d UnitX = new Vector2d(1, 0);
        public static readonly Vector2d UnitY = new Vector2d(0, 1);

        public double X { get; set; }
        public double Y { get; set; }

        public Vector2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Length => Math.Sqrt(X * X + Y * Y);

        public Vector2d Normalized
        {
            get
            {
                var length = Length;
                return length > 0 ? new Vector2d(X / length, Y / length) : Zero;
            }
        }

        public Vector2d Scale(double factor)
        {
            return new Vector2d(X * factor, Y * factor);
        }

        public double Dot(Vector2d other)
        {
            return X * other.X + Y * other.Y;
        }

        public override string ToString()
        {
            return $"[{X:F2}, {Y:F2}]";
        }

        public bool Equals(Vector2d other)
        {
            return Math.Abs(X - other.X) < 1e-10 && Math.Abs(Y - other.Y) < 1e-10;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector2d other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(Vector2d left, Vector2d right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2d left, Vector2d right)
        {
            return !left.Equals(right);
        }

        public static Vector2d operator +(Vector2d left, Vector2d right)
        {
            return new Vector2d(left.X + right.X, left.Y + right.Y);
        }

        public static Vector2d operator -(Vector2d left, Vector2d right)
        {
            return new Vector2d(left.X - right.X, left.Y - right.Y);
        }

        public static Vector2d operator *(Vector2d vector, double scalar)
        {
            return vector.Scale(scalar);
        }

        public static Vector2d operator *(double scalar, Vector2d vector)
        {
            return vector.Scale(scalar);
        }
    }

    /// <summary>
    /// Represents a 2D size (width and height).
    /// </summary>
    public struct Size2d : IEquatable<Size2d>
    {
        public static readonly Size2d Empty = new Size2d(0, 0);

        public double Width { get; set; }
        public double Height { get; set; }

        public Size2d(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Area => Width * Height;

        public double AspectRatio => Height > 0 ? Math.Max(Width / Height, Height / Width) : 0;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public override string ToString()
        {
            return $"{Width:F2}x{Height:F2}";
        }

        public bool Equals(Size2d other)
        {
            return Math.Abs(Width - other.Width) < 1e-10 && Math.Abs(Height - other.Height) < 1e-10;
        }

        public override bool Equals(object obj)
        {
            return obj is Size2d other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }

        public static bool operator ==(Size2d left, Size2d right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Size2d left, Size2d right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Represents a 2D axis-aligned rectangle.
    /// </summary>
    public struct Rectangle2d : IEquatable<Rectangle2d>
    {
        public static readonly Rectangle2d Empty = new Rectangle2d(0, 0, 0, 0);

        public double Left { get; set; }
        public double Bottom { get; set; }
        public double Right { get; set; }
        public double Top { get; set; }

        public Rectangle2d(double left, double bottom, double right, double top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public Rectangle2d(Point2d center, Size2d size)
        {
            var halfWidth = size.Width / 2;
            var halfHeight = size.Height / 2;
            Left = center.X - halfWidth;
            Right = center.X + halfWidth;
            Bottom = center.Y - halfHeight;
            Top = center.Y + halfHeight;
        }

        public double Width => Right - Left;
        public double Height => Top - Bottom;
        public double Area => Width * Height;
        public Point2d Center => new Point2d((Left + Right) / 2, (Bottom + Top) / 2);
        public Size2d Size => new Size2d(Width, Height);

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public bool Contains(Point2d point)
        {
            return point.X >= Left && point.X <= Right && point.Y >= Bottom && point.Y <= Top;
        }

        public bool IntersectsWith(Rectangle2d other)
        {
            return Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;
        }

        public static Rectangle2d Intersect(Rectangle2d a, Rectangle2d b)
        {
            var left = Math.Max(a.Left, b.Left);
            var right = Math.Min(a.Right, b.Right);
            var bottom = Math.Max(a.Bottom, b.Bottom);
            var top = Math.Min(a.Top, b.Top);

            if (left >= right || bottom >= top)
                return Empty;

            return new Rectangle2d(left, bottom, right, top);
        }

        public static Rectangle2d Union(Rectangle2d a, Rectangle2d b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;

            return new Rectangle2d(
                Math.Min(a.Left, b.Left),
                Math.Min(a.Bottom, b.Bottom),
                Math.Max(a.Right, b.Right),
                Math.Max(a.Top, b.Top));
        }

        public Rectangle2d Inflate(double amount)
        {
            return new Rectangle2d(Left - amount, Bottom - amount, Right + amount, Top + amount);
        }

        public override string ToString()
        {
            return $"[{Left:F2},{Bottom:F2} - {Right:F2},{Top:F2}]";
        }

        public bool Equals(Rectangle2d other)
        {
            return Math.Abs(Left - other.Left) < 1e-10 &&
                   Math.Abs(Bottom - other.Bottom) < 1e-10 &&
                   Math.Abs(Right - other.Right) < 1e-10 &&
                   Math.Abs(Top - other.Top) < 1e-10;
        }

        public override bool Equals(object obj)
        {
            return obj is Rectangle2d other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Bottom, Right, Top);
        }

        public static bool operator ==(Rectangle2d left, Rectangle2d right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Rectangle2d left, Rectangle2d right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Represents a 2D boundary defined by a series of connected points.
    /// </summary>
    public class Boundary
    {
        public List<Point2d> Points { get; set; } = new List<Point2d>();
        public bool IsClosed { get; set; } = true;
        public string Name { get; set; } = string.Empty;

        public Rectangle2d BoundingRectangle
        {
            get
            {
                if (!Points.Any()) return Rectangle2d.Empty;

                var minX = Points.Min(p => p.X);
                var maxX = Points.Max(p => p.X);
                var minY = Points.Min(p => p.Y);
                var maxY = Points.Max(p => p.Y);

                return new Rectangle2d(minX, minY, maxX, maxY);
            }
        }

        public double Area
        {
            get
            {
                if (Points.Count < 3) return 0;

                // Shoelace formula for polygon area
                double area = 0;
                for (int i = 0; i < Points.Count - 1; i++)
                {
                    area += Points[i].X * Points[i + 1].Y - Points[i + 1].X * Points[i].Y;
                }
                
                if (IsClosed && Points.Count > 2)
                {
                    area += Points[Points.Count - 1].X * Points[0].Y - Points[0].X * Points[Points.Count - 1].Y;
                }

                return Math.Abs(area) / 2.0;
            }
        }

        public double Perimeter
        {
            get
            {
                if (Points.Count < 2) return 0;

                double perimeter = 0;
                for (int i = 0; i < Points.Count - 1; i++)
                {
                    perimeter += Points[i].DistanceTo(Points[i + 1]);
                }

                if (IsClosed && Points.Count > 2)
                {
                    perimeter += Points[Points.Count - 1].DistanceTo(Points[0]);
                }

                return perimeter;
            }
        }

        public bool Contains(Point2d point)
        {
            if (Points.Count < 3) return false;

            // Ray casting algorithm
            int intersections = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                var p1 = Points[i];
                var p2 = Points[(i + 1) % Points.Count];

                if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    intersections++;
                }
            }

            return intersections % 2 == 1;
        }

        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (Points.Count < 3)
                result.AddError("Boundary must have at least 3 points");

            if (IsClosed && Points.Count > 0 && !Points[0].Equals(Points[Points.Count - 1]))
                result.AddWarning("Closed boundary should have first and last points equal");

            if (Area < 1.0)
                result.AddWarning("Boundary area is very small (less than 1 square meter)");

            // Check for self-intersection (simplified check)
            if (HasSelfIntersection())
                result.AddError("Boundary has self-intersections");

            return result;
        }

        private bool HasSelfIntersection()
        {
            // Simplified self-intersection check
            // TODO: Implement more robust line segment intersection algorithm
            return false;
        }

        public Boundary Clone()
        {
            return new Boundary
            {
                Points = new List<Point2d>(Points),
                IsClosed = IsClosed,
                Name = Name
            };
        }

        public override string ToString()
        {
            return $"Boundary '{Name}' ({Points.Count} points, {Area:F1}m²)";
        }
    }
}