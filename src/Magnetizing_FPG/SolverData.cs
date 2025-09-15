using Rhino.Geometry;
using System.Collections.Generic;

namespace Magnetizing_FPG
{
    /// <summary>
    /// Contains all input parameters needed by the magnetizing algorithm solver.
    /// This class encapsulates the data contracts for the core algorithm.
    /// </summary>
    public class SolverInputs
    {
        /// <summary>
        /// The house instance containing rooms, boundary, and adjacency information.
        /// </summary>
        public IHouseInstance HouseInstance { get; set; }

        /// <summary>
        /// Number of iterations to run the algorithm.
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Maximum distance between two connected rooms.
        /// </summary>
        public double MaxAdjDistance { get; set; }

        /// <summary>
        /// Size of each grid cell in meters.
        /// </summary>
        public double CellSize { get; set; }

        /// <summary>
        /// Boundary offset for the building boundary.
        /// </summary>
        public double BoundaryOffset { get; set; }

        /// <summary>
        /// Random seed for reproducible results. Use 0 for random seed each execution.
        /// </summary>
        public int RandomSeed { get; set; } = 0;

        // Corridor generation settings
        /// <summary>
        /// Whether to generate corridors on one side of rooms.
        /// </summary>
        public bool OneSideCorridorsChecked { get; set; }

        /// <summary>
        /// Whether to generate corridors on two sides of rooms.
        /// </summary>
        public bool TwoSidesCorridorsChecked { get; set; }

        /// <summary>
        /// Whether to generate corridors on all sides of rooms.
        /// </summary>
        public bool AllSidesCorridorsChecked { get; set; }

        /// <summary>
        /// Whether to treat corridors as additional spaces.
        /// </summary>
        public bool CorridorsAsAdditionalSpacesChecked { get; set; }

        // Post-processing settings
        /// <summary>
        /// Whether to remove dead ends from corridors.
        /// </summary>
        public bool RemoveDeadEnds { get; set; }

        /// <summary>
        /// Whether to remove all corridors.
        /// </summary>
        public bool RemoveAllCorridors { get; set; }
    }

    /// <summary>
    /// Contains all output results from the magnetizing algorithm solver.
    /// This class encapsulates the results produced by the core algorithm.
    /// </summary>
    public class SolverOutputs
    {
        /// <summary>
        /// List of room geometries as Breps.
        /// </summary>
        public List<Brep> RoomBreps { get; set; } = new List<Brep>();

        /// <summary>
        /// Corridor geometry as a single Brep.
        /// </summary>
        public Brep Corridors { get; set; }

        /// <summary>
        /// List of room names in the same order as RoomBreps.
        /// </summary>
        public List<string> RoomNames { get; set; } = new List<string>();

        /// <summary>
        /// Adjacency information as a formatted string.
        /// </summary>
        public string Adjacencies { get; set; } = "";

        /// <summary>
        /// Number of missing adjacencies for each room.
        /// </summary>
        public List<int> MissingAdjacences { get; set; } = new List<int>();

        /// <summary>
        /// The original boundary curve.
        /// </summary>
        public Curve Boundary { get; set; }

        /// <summary>
        /// The boundary curve with offset applied.
        /// </summary>
        public Curve BoundaryWithOffset { get; set; }

        /// <summary>
        /// Status message indicating algorithm progress or results.
        /// </summary>
        public string Message { get; set; } = "";
    }
}