using System;
using System.Collections.Generic;

namespace Magnetizing_FPG.Core.Domain
{
    /// <summary>
    /// Represents a room in a floor plan with its properties and spatial requirements.
    /// This is a pure domain model without any external dependencies.
    /// </summary>
    public class Room
    {
        /// <summary>
        /// Unique identifier for the room within a floor plan.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Human-readable name or description of the room.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Required area of the room in square meters.
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// Whether this room functions as a circulation space (hall/corridor).
        /// </summary>
        public bool IsHall { get; set; }

        /// <summary>
        /// Center position of the room in 2D space.
        /// </summary>
        public Point2d Position { get; set; } = Point2d.Origin;

        /// <summary>
        /// Dimensions of the room (width and height).
        /// </summary>
        public Size2d Dimensions { get; set; } = Size2d.Empty;

        /// <summary>
        /// List of room IDs that should be adjacent to this room.
        /// </summary>
        public List<int> AdjacentRoomIds { get; set; } = new List<int>();

        /// <summary>
        /// Indicates whether this room has unmet adjacency requirements.
        /// </summary>
        public bool HasMissingAdjacencies { get; set; }

        /// <summary>
        /// Minimum acceptable width for the room in meters.
        /// </summary>
        public double MinWidth { get; set; } = 2.0;

        /// <summary>
        /// Maximum acceptable aspect ratio (width/height or height/width, whichever is larger).
        /// </summary>
        public double MaxAspectRatio { get; set; } = 2.0;

        /// <summary>
        /// Priority for placement (higher values placed first).
        /// </summary>
        public int PlacementPriority { get; set; }

        /// <summary>
        /// Custom properties for extensibility.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the calculated height based on area and width.
        /// </summary>
        public double Height => Dimensions.Width > 0 ? Area / Dimensions.Width : 0;

        /// <summary>
        /// Gets the current aspect ratio of the room.
        /// </summary>
        public double AspectRatio
        {
            get
            {
                if (Dimensions.Height <= 0 || Dimensions.Width <= 0) return 0;
                return Math.Max(Dimensions.Width / Dimensions.Height, Dimensions.Height / Dimensions.Width);
            }
        }

        /// <summary>
        /// Gets the bounding rectangle of the room.
        /// </summary>
        public Rectangle2d BoundingRectangle
        {
            get
            {
                var halfWidth = Dimensions.Width / 2;
                var halfHeight = Dimensions.Height / 2;
                return new Rectangle2d(
                    Position.X - halfWidth,
                    Position.Y - halfHeight,
                    Position.X + halfWidth,
                    Position.Y + halfHeight);
            }
        }

        /// <summary>
        /// Validates that the room meets its constraints.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (Area <= 0)
                result.AddError("Room area must be greater than zero");

            if (string.IsNullOrWhiteSpace(Name))
                result.AddWarning("Room name should not be empty");

            if (Dimensions.Width > 0 && Dimensions.Width < MinWidth)
                result.AddError($"Room width ({Dimensions.Width:F2}m) is less than minimum ({MinWidth:F2}m)");

            if (AspectRatio > MaxAspectRatio)
                result.AddWarning($"Room aspect ratio ({AspectRatio:F2}) exceeds maximum ({MaxAspectRatio:F2})");

            return result;
        }

        /// <summary>
        /// Creates a copy of the room.
        /// </summary>
        public Room Clone()
        {
            return new Room
            {
                Id = Id,
                Name = Name,
                Area = Area,
                IsHall = IsHall,
                Position = Position,
                Dimensions = Dimensions,
                AdjacentRoomIds = new List<int>(AdjacentRoomIds),
                HasMissingAdjacencies = HasMissingAdjacencies,
                MinWidth = MinWidth,
                MaxAspectRatio = MaxAspectRatio,
                PlacementPriority = PlacementPriority,
                Properties = new Dictionary<string, object>(Properties)
            };
        }

        /// <summary>
        /// Checks if this room overlaps with another room.
        /// </summary>
        public bool OverlapsWith(Room other)
        {
            if (other == null) return false;
            return BoundingRectangle.IntersectsWith(other.BoundingRectangle);
        }

        /// <summary>
        /// Calculates the distance between this room and another room (center to center).
        /// </summary>
        public double DistanceTo(Room other)
        {
            if (other == null) return double.MaxValue;
            return Position.DistanceTo(other.Position);
        }

        /// <summary>
        /// Calculates the minimum distance between the edges of this room and another room.
        /// </summary>
        public double EdgeDistanceTo(Room other)
        {
            if (other == null) return double.MaxValue;
            
            var thisRect = BoundingRectangle;
            var otherRect = other.BoundingRectangle;
            
            // If rooms overlap, distance is negative (penetration depth)
            if (thisRect.IntersectsWith(otherRect))
            {
                var intersection = Rectangle2d.Intersect(thisRect, otherRect);
                return -Math.Min(intersection.Width, intersection.Height);
            }
            
            // Calculate minimum distance between non-overlapping rectangles
            var dx = Math.Max(0, Math.Max(thisRect.Left - otherRect.Right, otherRect.Left - thisRect.Right));
            var dy = Math.Max(0, Math.Max(thisRect.Bottom - otherRect.Top, otherRect.Bottom - thisRect.Top));
            
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString()
        {
            return $"Room {Id}: {Name} ({Area:F1}m²)";
        }

        public override bool Equals(object obj)
        {
            return obj is Room other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}