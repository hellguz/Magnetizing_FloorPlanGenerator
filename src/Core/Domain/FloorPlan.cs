using System;
using System.Collections.Generic;
using System.Linq;

namespace Magnetizing_FPG.Core.Domain
{
    /// <summary>
    /// Represents a complete floor plan with rooms, corridors, and metadata.
    /// </summary>
    public class FloorPlan
    {
        /// <summary>
        /// The boundary of the building.
        /// </summary>
        public Boundary Boundary { get; set; } = new Boundary();

        /// <summary>
        /// All rooms in the floor plan.
        /// </summary>
        public List<Room> Rooms { get; set; } = new List<Room>();

        /// <summary>
        /// Generated corridors connecting rooms.
        /// </summary>
        public List<Corridor> Corridors { get; set; } = new List<Corridor>();

        /// <summary>
        /// The entrance point of the building.
        /// </summary>
        public Point2d EntrancePoint { get; set; } = Point2d.Origin;

        /// <summary>
        /// Grid cell size used for generation.
        /// </summary>
        public double CellSize { get; set; } = 1.0;

        /// <summary>
        /// Performance and quality metrics.
        /// </summary>
        public FloorPlanMetrics Metrics { get; set; } = new FloorPlanMetrics();

        /// <summary>
        /// Generation timestamp.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Algorithm configuration used for generation.
        /// </summary>
        public Dictionary<string, object> GenerationParameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets all adjacency relationships in the floor plan.
        /// </summary>
        public List<AdjacencyRelationship> Adjacencies
        {
            get
            {
                var adjacencies = new List<AdjacencyRelationship>();
                
                foreach (var room in Rooms)
                {
                    foreach (var adjacentId in room.AdjacentRoomIds)
                    {
                        var adjacentRoom = Rooms.FirstOrDefault(r => r.Id == adjacentId);
                        if (adjacentRoom != null && room.Id < adjacentId) // Avoid duplicates
                        {
                            adjacencies.Add(new AdjacencyRelationship
                            {
                                Room1 = room,
                                Room2 = adjacentRoom,
                                IsSatisfied = CalculateAdjacencySatisfaction(room, adjacentRoom),
                                Distance = room.EdgeDistanceTo(adjacentRoom)
                            });
                        }
                    }
                }

                return adjacencies;
            }
        }

        /// <summary>
        /// Gets the total area of all rooms.
        /// </summary>
        public double TotalRoomArea => Rooms.Sum(r => r.Area);

        /// <summary>
        /// Gets the total area of all corridors.
        /// </summary>
        public double TotalCorridorArea => Corridors.Sum(c => c.Area);

        /// <summary>
        /// Gets the total utilized area (rooms + corridors).
        /// </summary>
        public double TotalUtilizedArea => TotalRoomArea + TotalCorridorArea;

        /// <summary>
        /// Gets the utilization efficiency (utilized area / boundary area).
        /// </summary>
        public double UtilizationEfficiency => Boundary.Area > 0 ? TotalUtilizedArea / Boundary.Area : 0;

        /// <summary>
        /// Validates the floor plan for correctness and quality.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            // Validate boundary
            var boundaryValidation = Boundary.Validate();
            result.Merge(boundaryValidation);

            // Validate rooms
            foreach (var room in Rooms)
            {
                var roomValidation = room.Validate();
                result.Merge(roomValidation, $"Room {room.Id}");

                // Check if room is within boundary
                if (!IsRoomWithinBoundary(room))
                    result.AddWarning($"Room {room.Id} extends outside the boundary");
            }

            // Check for room overlaps
            var overlaps = FindRoomOverlaps();
            foreach (var overlap in overlaps)
            {
                result.AddError($"Rooms {overlap.Room1.Id} and {overlap.Room2.Id} overlap");
            }

            // Validate adjacencies
            var unsatisfiedAdjacencies = Adjacencies.Where(a => !a.IsSatisfied).ToList();
            foreach (var adj in unsatisfiedAdjacencies)
            {
                result.AddWarning($"Adjacency between rooms {adj.Room1.Id} and {adj.Room2.Id} is not satisfied (distance: {adj.Distance:F2}m)");
            }

            // Check utilization efficiency
            if (UtilizationEfficiency < 0.6)
                result.AddWarning($"Low space utilization efficiency: {UtilizationEfficiency:P1}");
            else if (UtilizationEfficiency > 0.95)
                result.AddWarning($"Very high space utilization: {UtilizationEfficiency:P1} - may be overcrowded");

            return result;
        }

        /// <summary>
        /// Finds all room overlaps in the floor plan.
        /// </summary>
        public List<RoomOverlap> FindRoomOverlaps()
        {
            var overlaps = new List<RoomOverlap>();

            for (int i = 0; i < Rooms.Count; i++)
            {
                for (int j = i + 1; j < Rooms.Count; j++)
                {
                    var room1 = Rooms[i];
                    var room2 = Rooms[j];

                    if (room1.OverlapsWith(room2))
                    {
                        var intersection = Rectangle2d.Intersect(room1.BoundingRectangle, room2.BoundingRectangle);
                        overlaps.Add(new RoomOverlap
                        {
                            Room1 = room1,
                            Room2 = room2,
                            OverlapArea = intersection.Area
                        });
                    }
                }
            }

            return overlaps;
        }

        /// <summary>
        /// Checks if a room is completely within the boundary.
        /// </summary>
        private bool IsRoomWithinBoundary(Room room)
        {
            var rect = room.BoundingRectangle;
            var corners = new[]
            {
                new Point2d(rect.Left, rect.Bottom),
                new Point2d(rect.Right, rect.Bottom),
                new Point2d(rect.Right, rect.Top),
                new Point2d(rect.Left, rect.Top)
            };

            return corners.All(corner => Boundary.Contains(corner));
        }

        /// <summary>
        /// Calculates whether an adjacency relationship is satisfied.
        /// </summary>
        private bool CalculateAdjacencySatisfaction(Room room1, Room room2, double maxDistance = 2.0)
        {
            return room1.EdgeDistanceTo(room2) <= maxDistance;
        }

        /// <summary>
        /// Creates a copy of the floor plan.
        /// </summary>
        public FloorPlan Clone()
        {
            return new FloorPlan
            {
                Boundary = Boundary.Clone(),
                Rooms = Rooms.Select(r => r.Clone()).ToList(),
                Corridors = Corridors.Select(c => c.Clone()).ToList(),
                EntrancePoint = EntrancePoint,
                CellSize = CellSize,
                Metrics = Metrics.Clone(),
                GeneratedAt = GeneratedAt,
                GenerationParameters = new Dictionary<string, object>(GenerationParameters)
            };
        }

        public override string ToString()
        {
            return $"FloorPlan: {Rooms.Count} rooms, {Corridors.Count} corridors, {UtilizationEfficiency:P1} efficiency";
        }
    }

    /// <summary>
    /// Represents a corridor connecting rooms.
    /// </summary>
    public class Corridor
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Point2d> Path { get; set; } = new List<Point2d>();
        public double Width { get; set; } = 1.2;
        public List<int> ConnectedRoomIds { get; set; } = new List<int>();

        public double Length
        {
            get
            {
                if (Path.Count < 2) return 0;
                double length = 0;
                for (int i = 0; i < Path.Count - 1; i++)
                {
                    length += Path[i].DistanceTo(Path[i + 1]);
                }
                return length;
            }
        }

        public double Area => Length * Width;

        public Corridor Clone()
        {
            return new Corridor
            {
                Id = Id,
                Name = Name,
                Path = new List<Point2d>(Path),
                Width = Width,
                ConnectedRoomIds = new List<int>(ConnectedRoomIds)
            };
        }

        public override string ToString()
        {
            return $"Corridor {Id}: {Length:F1}m × {Width:F1}m ({Area:F1}m²)";
        }
    }

    /// <summary>
    /// Represents an adjacency relationship between two rooms.
    /// </summary>
    public class AdjacencyRelationship
    {
        public Room Room1 { get; set; }
        public Room Room2 { get; set; }
        public bool IsSatisfied { get; set; }
        public double Distance { get; set; }
        public string Type { get; set; } = "required";

        public override string ToString()
        {
            var status = IsSatisfied ? "✓" : "✗";
            return $"{status} {Room1.Id}-{Room2.Id} ({Distance:F2}m)";
        }
    }

    /// <summary>
    /// Represents an overlap between two rooms.
    /// </summary>
    public class RoomOverlap
    {
        public Room Room1 { get; set; }
        public Room Room2 { get; set; }
        public double OverlapArea { get; set; }

        public override string ToString()
        {
            return $"Overlap: Rooms {Room1.Id}-{Room2.Id} ({OverlapArea:F2}m²)";
        }
    }

    /// <summary>
    /// Performance and quality metrics for a floor plan.
    /// </summary>
    public class FloorPlanMetrics
    {
        public TimeSpan GenerationTime { get; set; }
        public long MemoryUsed { get; set; }
        public int IterationsPerformed { get; set; }
        public int RoomsPlaced { get; set; }
        public int AdjacenciesSatisfied { get; set; }
        public int TotalAdjacencies { get; set; }
        public double AdjacencySatisfactionRate => TotalAdjacencies > 0 ? (double)AdjacenciesSatisfied / TotalAdjacencies : 0;

        // Quality metrics
        public double SpaceEfficiency { get; set; }
        public double CirculationRatio { get; set; }
        public double AverageRoomAspectRatio { get; set; }
        public int DeadEndCorridors { get; set; }

        public FloorPlanMetrics Clone()
        {
            return new FloorPlanMetrics
            {
                GenerationTime = GenerationTime,
                MemoryUsed = MemoryUsed,
                IterationsPerformed = IterationsPerformed,
                RoomsPlaced = RoomsPlaced,
                AdjacenciesSatisfied = AdjacenciesSatisfied,
                TotalAdjacencies = TotalAdjacencies,
                SpaceEfficiency = SpaceEfficiency,
                CirculationRatio = CirculationRatio,
                AverageRoomAspectRatio = AverageRoomAspectRatio,
                DeadEndCorridors = DeadEndCorridors
            };
        }

        public override string ToString()
        {
            return $"Metrics: {GenerationTime.TotalMilliseconds:F0}ms, {AdjacencySatisfactionRate:P1} adjacencies, {SpaceEfficiency:P1} efficiency";
        }
    }
}