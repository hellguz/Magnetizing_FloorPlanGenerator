using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Magnetizing_FPG.Core
{
    /// <summary>
    /// Holds the final results of the floor plan generation process.
    /// </summary>
    public class FloorPlanResult
    {
        public List<Brep> RoomBreps { get; set; }
        public Brep CorridorsBrep { get; set; }
        public List<string> RoomNames { get; set; }
        public string AdjacenciesOutputString { get; set; }
        public List<int> MissingRoomAdjSortedList { get; set; }
        public int PlacedRoomsCount { get; set; }
        public int TotalRoomsCount { get; set; }

        public FloorPlanResult()
        {
            RoomBreps = new List<Brep>();
            RoomNames = new List<string>();
            MissingRoomAdjSortedList = new List<int>();
        }
    }
}

