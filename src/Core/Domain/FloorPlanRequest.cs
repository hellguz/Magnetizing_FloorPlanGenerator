using System;
using System.Collections.Generic;
using System.Linq;

namespace Magnetizing_FPG.Core.Domain
{
    /// <summary>
    /// Represents a request to generate a floor plan with all necessary input data.
    /// </summary>
    public class FloorPlanRequest
    {
        /// <summary>
        /// The boundary within which to place rooms.
        /// </summary>
        public Boundary Boundary { get; set; } = new Boundary();

        /// <summary>
        /// The entrance point where the first room should be placed.
        /// </summary>
        public Point2d EntrancePoint { get; set; } = Point2d.Origin;

        /// <summary>
        /// List of rooms to be placed in the floor plan.
        /// </summary>
        public List<Room> Rooms { get; set; } = new List<Room>();

        /// <summary>
        /// Adjacency requirements between rooms.
        /// </summary>
        public List<AdjacencyConstraint> Adjacencies { get; set; } = new List<AdjacencyConstraint>();

        /// <summary>
        /// Configuration for the generation algorithm.
        /// </summary>
        public AlgorithmConfiguration Configuration { get; set; } = new AlgorithmConfiguration();

        /// <summary>
        /// Optional metadata about the request.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Request creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Unique identifier for the request.
        /// </summary>
        public Guid RequestId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets the total area of all requested rooms.
        /// </summary>
        public double TotalRoomArea => Rooms.Sum(r => r.Area);

        /// <summary>
        /// Gets the entrance room (if specified).
        /// </summary>
        public Room EntranceRoom => Rooms.FirstOrDefault(r => r.IsHall && r.PlacementPriority > 0);

        /// <summary>
        /// Validates the floor plan request.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            // Validate boundary
            var boundaryValidation = Boundary.Validate();
            result.Merge(boundaryValidation, "Boundary");

            // Check if entrance point is within boundary
            if (!Boundary.Contains(EntrancePoint))
                result.AddWarning("Entrance point is outside the boundary");

            // Validate rooms
            if (!Rooms.Any())
                result.AddError("At least one room must be specified");

            foreach (var room in Rooms)
            {
                var roomValidation = room.Validate();
                result.Merge(roomValidation, $"Room {room.Id}");
            }

            // Check for duplicate room IDs
            var duplicateIds = Rooms.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var id in duplicateIds)
            {
                result.AddError($"Duplicate room ID: {id}");
            }

            // Check total room area vs boundary area
            if (TotalRoomArea > Boundary.Area * 0.9) // Allow 10% for circulation
            {
                result.AddWarning($"Total room area ({TotalRoomArea:F1}m²) is close to boundary area ({Boundary.Area:F1}m²) - may be difficult to place all rooms");
            }

            if (TotalRoomArea > Boundary.Area)
            {
                result.AddError($"Total room area ({TotalRoomArea:F1}m²) exceeds boundary area ({Boundary.Area:F1}m²)");
            }

            // Validate adjacencies
            foreach (var adjacency in Adjacencies)
            {
                var adjValidation = adjacency.Validate(Rooms);
                result.Merge(adjValidation, $"Adjacency {adjacency.Room1Id}-{adjacency.Room2Id}");
            }

            // Validate configuration
            var configValidation = Configuration.Validate();
            result.Merge(configValidation, "Configuration");

            return result;
        }

        /// <summary>
        /// Creates a copy of the request.
        /// </summary>
        public FloorPlanRequest Clone()
        {
            return new FloorPlanRequest
            {
                Boundary = Boundary.Clone(),
                EntrancePoint = EntrancePoint,
                Rooms = Rooms.Select(r => r.Clone()).ToList(),
                Adjacencies = Adjacencies.Select(a => a.Clone()).ToList(),
                Configuration = Configuration.Clone(),
                Metadata = new Dictionary<string, object>(Metadata),
                CreatedAt = CreatedAt,
                RequestId = RequestId
            };
        }

        public override string ToString()
        {
            return $"FloorPlanRequest: {Rooms.Count} rooms, {Adjacencies.Count} adjacencies, {TotalRoomArea:F1}m² in {Boundary.Area:F1}m² boundary";
        }
    }

    /// <summary>
    /// Represents the result of a floor plan generation request.
    /// </summary>
    public class FloorPlanResult
    {
        /// <summary>
        /// The original request that generated this result.
        /// </summary>
        public FloorPlanRequest Request { get; set; }

        /// <summary>
        /// The generated floor plan (null if generation failed).
        /// </summary>
        public FloorPlan FloorPlan { get; set; }

        /// <summary>
        /// Indicates whether generation was successful.
        /// </summary>
        public bool IsSuccess => FloorPlan != null && !ValidationResult.Errors.Any();

        /// <summary>
        /// Validation results and any issues encountered.
        /// </summary>
        public ValidationResult ValidationResult { get; set; } = new ValidationResult();

        /// <summary>
        /// Detailed generation log and debugging information.
        /// </summary>
        public List<string> GenerationLog { get; set; } = new List<string>();

        /// <summary>
        /// Generation completion timestamp.
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total generation time.
        /// </summary>
        public TimeSpan GenerationTime => CompletedAt - Request.CreatedAt;

        /// <summary>
        /// Additional result metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static FloorPlanResult Success(FloorPlanRequest request, FloorPlan floorPlan)
        {
            return new FloorPlanResult
            {
                Request = request,
                FloorPlan = floorPlan,
                ValidationResult = ValidationResult.Success()
            };
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static FloorPlanResult Failure(FloorPlanRequest request, ValidationResult validationResult, Exception exception = null)
        {
            var result = new FloorPlanResult
            {
                Request = request,
                FloorPlan = null,
                ValidationResult = validationResult
            };

            if (exception != null)
            {
                result.GenerationLog.Add($"Exception: {exception.Message}");
                if (exception.StackTrace != null)
                {
                    result.GenerationLog.Add($"Stack trace: {exception.StackTrace}");
                }
            }

            return result;
        }

        public override string ToString()
        {
            var status = IsSuccess ? "Success" : "Failed";
            var time = GenerationTime.TotalMilliseconds;
            return $"FloorPlanResult: {status} in {time:F0}ms";
        }
    }

    /// <summary>
    /// Represents an adjacency constraint between two rooms.
    /// </summary>
    public class AdjacencyConstraint
    {
        /// <summary>
        /// ID of the first room.
        /// </summary>
        public int Room1Id { get; set; }

        /// <summary>
        /// ID of the second room.
        /// </summary>
        public int Room2Id { get; set; }

        /// <summary>
        /// Type of adjacency requirement.
        /// </summary>
        public AdjacencyType Type { get; set; } = AdjacencyType.Required;

        /// <summary>
        /// Maximum acceptable distance between rooms.
        /// </summary>
        public double MaxDistance { get; set; } = 2.0;

        /// <summary>
        /// Priority of this constraint (higher = more important).
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Additional constraint properties.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Validates the adjacency constraint.
        /// </summary>
        public ValidationResult Validate(List<Room> rooms)
        {
            var result = new ValidationResult();

            if (Room1Id == Room2Id)
                result.AddError("Room cannot be adjacent to itself");

            if (MaxDistance <= 0)
                result.AddError("Maximum distance must be greater than zero");

            var room1 = rooms.FirstOrDefault(r => r.Id == Room1Id);
            var room2 = rooms.FirstOrDefault(r => r.Id == Room2Id);

            if (room1 == null)
                result.AddError($"Room {Room1Id} not found");

            if (room2 == null)
                result.AddError($"Room {Room2Id} not found");

            return result;
        }

        /// <summary>
        /// Creates a copy of the constraint.
        /// </summary>
        public AdjacencyConstraint Clone()
        {
            return new AdjacencyConstraint
            {
                Room1Id = Room1Id,
                Room2Id = Room2Id,
                Type = Type,
                MaxDistance = MaxDistance,
                Priority = Priority,
                Properties = new Dictionary<string, object>(Properties)
            };
        }

        public override string ToString()
        {
            return $"{Room1Id}-{Room2Id} ({Type}, max {MaxDistance:F1}m)";
        }

        public override bool Equals(object obj)
        {
            return obj is AdjacencyConstraint other && 
                   ((Room1Id == other.Room1Id && Room2Id == other.Room2Id) ||
                    (Room1Id == other.Room2Id && Room2Id == other.Room1Id));
        }

        public override int GetHashCode()
        {
            // Ensure symmetric hash code for undirected adjacency
            var id1 = Math.Min(Room1Id, Room2Id);
            var id2 = Math.Max(Room1Id, Room2Id);
            return HashCode.Combine(id1, id2);
        }
    }

    /// <summary>
    /// Types of adjacency requirements.
    /// </summary>
    public enum AdjacencyType
    {
        /// <summary>
        /// Rooms must be adjacent (hard constraint).
        /// </summary>
        Required,

        /// <summary>
        /// Rooms should be adjacent if possible (soft constraint).
        /// </summary>
        Preferred,

        /// <summary>
        /// Rooms should not be adjacent.
        /// </summary>
        Avoid
    }
}