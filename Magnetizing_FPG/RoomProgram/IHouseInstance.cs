using System.Collections.Generic;
using Rhino.Geometry;

namespace Magnetizing_FPG
{
    /// <summary>
    /// Defines the contract for a house instance, containing the overall building parameters and its constituent rooms.
    /// </summary>
    public interface IHouseInstance
    {
        /// <summary>
        /// The building boundary. 
        /// </summary>
        Curve boundary { get; }

        /// <summary>
        /// The entrance point. 
        /// </summary>
        Point3d startingPoint { get; }

        /// <summary>
        /// If the boundary should be rotated for optimal packing. 
        /// </summary>
        bool tryRotateBoundary { get; }

        /// <summary>
        /// The list of room instances within this house. 
        /// </summary>
        List<IRoomInstance> RoomInstances { get; }

        /// <summary>
        /// The adjacency data as strings (e.g. "1-2"). 
        /// </summary>
        List<string> adjStrList { get; }

        /// <summary>
        /// An array representation of the adjacency data. 
        /// </summary>
        int[,] adjArray { get; set; }
    }
}

