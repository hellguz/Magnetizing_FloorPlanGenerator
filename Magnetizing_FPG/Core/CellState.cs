using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Magnetizing_FPG.Core
{
    /// <summary>
    /// Defines the state of a cell in the floor plan grid.
    /// </summary>
    public enum CellState
    {
        Empty,
        Corridor,
        OutOfBounds
        // Room states are handled by positive integers representing the room ID.
    }

    /// <summary>
    /// Defines the four possible corner-aligned positions for placing a room.
    /// </summary>
    public enum RoomPosition { TopRight, BottomRight, BottomLeft, TopLeft, Undefined }
}

