using System.Collections.Generic;

namespace Magnetizing_FPG
{
    /// <summary>
    /// Defines the contract for a single room, including its properties and relationships.
    /// </summary>
    public interface IRoomInstance
    {
        /// <summary>
        /// A unique room identifier. 
        /// </summary>
        int RoomId { get; set; } 

        /// <summary>
        /// The room area in square meters (m²). 
        /// </summary>
        double RoomArea { get; set; } 

        /// <summary>
        /// The room name. 
        /// </summary>
        string RoomName { get; set; } 

        /// <summary>
        /// Whether the room is a hall or circulation space. 
        /// </summary>
        bool isHall { get; set; } 

        /// <summary>
        /// A list of rooms adjacent to this room. 
        /// </summary>
        List<IRoomInstance> AdjacentRoomsList { get; }

        /// <summary>
        /// Indicates if the room has any missing adjacencies in the final layout. 
        /// </summary>
        bool hasMissingAdj { get; set; }
    }
}

