using System;
using System.Collections.Generic;

namespace Magnetizing_FPG.Core
{
    /// <summary>
    /// Contains the dimensions and the origin of a room on a grid.
    /// </summary>
    public class RoomCells
    {
        public int x;
        public int y;
        public int w;
        public int h;

        public RoomCells() { }

        public RoomCells(int X, int Y, int W, int H)
        {
            x = X;
            y = Y;
            w = W;
            h = H;
        }

        public RoomCells(RoomCells roomCells)
        {
            x = roomCells.x;
            y = roomCells.y;
            w = roomCells.w;
            h = roomCells.h;
        }
    }

    /// <summary>
    /// Contains a complete solution that was once computed.
    /// It is used to store solutions generated during previous iterations.
    /// </summary>
    public class GridSolution
    {
        public int[,] grid;
        public List<RoomCells> roomCellsList = new List<RoomCells>();
        public List<int> placedRoomsOrderedList = new List<int>();

        public GridSolution(int[,] Grid, List<RoomCells> RoomCellsList, List<int> RoomOrder)
        {
            grid = Grid;
            roomCellsList = RoomCellsList.ConvertAll(roomCells => new RoomCells(roomCells));
            placedRoomsOrderedList = RoomOrder.ConvertAll(i => i);
        }
    }

    /// <summary>
    /// This one is simple, it contains the room placement solution as well as
    /// rating of the room.
    /// </summary>
    public class RoomPlacementSolution
    {
        public RoomPosition roomPosition;
        public int rating = 0;
        public int x;
        public int y;
        public int[,] room;

        public RoomPlacementSolution(int roomX, int roomY, RoomPosition position, int[,] mRoom, int mScore)
        {
            x = roomX;
            y = roomY;
            roomPosition = position;
            this.rating = mScore;
            this.room = mRoom.Clone() as int[,];
        }

        public RoomPlacementSolution(RoomPlacementSolution a)
        {
            x = a.x;
            y = a.y;
            roomPosition = a.roomPosition;
            this.rating = a.rating;
            this.room = a.room.Clone() as int[,];
        }

        public int GetRoomNum()
        {
            int max = -1;
            for (int i = 0; i < room.GetLength(0); i++)
                for (int j = 0; j < room.GetLength(1); j++)
                    max = Math.Max(max, room[i, j]);
            return max;
        }
    }

    /// <summary>
    /// IntPair generally is used for saving {(room number in the initialRoomsList) + 1, room priority}
    /// </summary>
    public class IntPair // CHANGED from struct to class to fix CS1612
    {
        public int roomNumber;
        public double AdjNum;

        public IntPair(int a1, double b1)
        {
            roomNumber = a1;
            AdjNum = b1;
        }
    }
}