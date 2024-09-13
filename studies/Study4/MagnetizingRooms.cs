using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Magnetizing_FPG
{
    public class MagnetizingRooms : GH_Component
    {

        Random random = new Random();

        List<RoomCells> bestRoomCellsList = new List<RoomCells>(); // The one that actually means something. It stores room's dimensions for all rooms of bestGrid
        List<RoomCells> roomCellsList = new List<RoomCells>();

        // That's needed for AppendAdditionalMenuItems functions.
        bool oneSideCorridorsChecked = true;
        bool twoSidesCorridorsChecked = false;
        bool allSidesCorridorsChecked = false;

        ToolStripMenuItem DropDown_OneSideCor;
        ToolStripMenuItem DropDown_TwoSidesCor;
        ToolStripMenuItem DropDown_AllSidesCor;

        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public MagnetizingRooms()
          : base("MagnetizingRooms", "MagnetizingRooms",
              "MagnetizingRooms",
              "FloorPlanGen", "Study_4")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "Boundary", "Boundary", GH_ParamAccess.item);
            pManager.AddPointParameter("Starting Points", "Starting Points", "Starting Points", GH_ParamAccess.list);
            pManager.AddTextParameter("Room Areas", "Areas", "Areas", GH_ParamAccess.item, "20, 50, 40, 30, 40");
            //pManager.AddGenericParameter("RoomList", "RoomList", "RoomList", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Iterations", "Iterations", "Iteratins (n*n*n)", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("MaxAdjDistance", "MaxAdjDistance", "MaxAdjDistance", GH_ParamAccess.item, 3);
            pManager.AddTextParameter("Adjacencies", "Adjacencies", "Adjacencies as list of string \"1 - 3, 2 - 4,..\"", GH_ParamAccess.list, "0 - 0");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Grid", "Grid", "Grid", GH_ParamAccess.list);
            pManager.AddIntegerParameter("xGridDim", "xGridDim", "xGridDim", GH_ParamAccess.item);
            pManager.AddIntegerParameter("RoomOrder", "RoomOrder", "RoomOrder", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> rooms = new List<Curve>();
            List<int> dims = new List<int>();
            int iterations = 0;
            List<int> roomAreas = new List<int>();
            string roomsInput = "";
            List<int> gridOutput = new List<int>();
            List<string> adjStrList = new List<string>();
            double maxAdjDistance = 0;
            int entranceIndexInRoomAreas = -1;



            DA.GetData("Room Areas", ref roomsInput);

            List<GH_ObjectWrapper> roomListWrappers = new List<GH_ObjectWrapper>();
            List<RoomInstance> roomList = new List<RoomInstance>();

           /* DA.GetDataList("RoomList", roomListWrappers);
            foreach (GH_ObjectWrapper wrapper in roomListWrappers)
            {
                roomList.Add(wrapper.Value as RoomInstance);
                if (RoomInstance.entranceIds.Contains((wrapper.Value as RoomInstance).RoomId))
                    entranceIndexInRoomAreas = roomList.Count;
            }*/

            

              for (int i = 0; i < roomsInput.Split(',').Length; i++)
              {
                  roomAreas.Add(Int16.Parse(roomsInput.Split(',')[i]));
                roomList.Add(new RoomInstance() { RoomArea = Int16.Parse(roomsInput.Split(',')[i]) });
                  if (roomAreas[roomAreas.Count - 1] < 0)
                  {
                      roomAreas[roomAreas.Count - 1] *= -1;
                      entranceIndexInRoomAreas = i + 1;
                  }
              }


            int x = 0;
            int y = 0;

            DA.GetData("Iterations", ref iterations);
            DA.GetData("MaxAdjDistance", ref maxAdjDistance);
            DA.GetDataList("Adjacencies", adjStrList);

            // Let's deal with getting boundary curve
            Curve boundaryCrv = new PolylineCurve();
            DA.GetData("Boundary", ref boundaryCrv);
            x = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.X);
            y = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.Y);

            int[,] grid = new int[x, y];
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    if (boundaryCrv.Contains(new Point3d(boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X + i + 0.5f
                    , boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y + j + 0.5f, 0)) == PointContainment.Inside)
                        grid[i, j] = 0;
                    else
                        grid[i, j] = 200;

            List<Point3d> startingPoints = new List<Point3d>();
            DA.GetDataList("Starting Points", startingPoints);


            if (startingPoints == null)
                grid[x / 2, y / 2] = -1;
            else
                foreach (Point3d point in startingPoints)
                {
                    if (boundaryCrv.Contains(point) == PointContainment.Inside)
                    {
                        int xIndex = (int)Math.Floor(point.X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X);
                        int yIndex = (int)Math.Floor(point.Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y);
                        grid[xIndex, yIndex] = -1;
                    }
                }



            int[,] adjArray = new int[adjStrList.Count, 2];

            for (int i = 0; i < adjStrList.Count; i++)
            {
                adjArray[i, 0] = Int32.Parse((adjStrList[i].Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[0]));
                adjArray[i, 1] = Int32.Parse((adjStrList[i].Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[1]));

            }


            // Actual program start


            int[,] bestGrid = grid.Clone() as int[,];
            int[,] startGrid = grid.Clone() as int[,];




            for (int i = 0; i < iterations; i++)
            {
                grid = startGrid.Clone() as int[,];

                roomCellsList.Clear();
                foreach (RoomInstance room in roomList)
                    roomCellsList.Add(new RoomCells());

                bool placedEntranceRoom = false;

                //  grid[1, 1] = -1;

                for (int j = 0; j < roomList.Count; j++)
                {
                    List<IntPair> roomOrderList = new List<IntPair>();

                    roomOrderList.Add(new IntPair(-1, -1));

                    for (int w = 1; w <= roomList.Count; w++)
                        if (!GridContains(grid, w))
                            roomOrderList.Add(new IntPair(w, 0));
                        else
                            roomOrderList.Add(new IntPair(w, -1));

                    for (int q = 0; q < adjArray.GetLength(0); q++)
                    {
                        if (GridContains(grid, roomOrderList[adjArray[q, 1]].roomNumber) && !GridContains(grid, roomOrderList[adjArray[q, 0]].roomNumber))
                            roomOrderList[adjArray[q, 0]] = new IntPair(roomOrderList[adjArray[q, 0]].roomNumber, roomOrderList[adjArray[q, 0]].AdjNum + 1 + random.NextDouble() * 0.05f);

                        if (GridContains(grid, roomOrderList[adjArray[q, 0]].roomNumber) && !GridContains(grid, roomOrderList[adjArray[q, 1]].roomNumber))
                            roomOrderList[adjArray[q, 1]] = new IntPair(roomOrderList[adjArray[q, 1]].roomNumber, roomOrderList[adjArray[q, 1]].AdjNum + 1 + random.NextDouble() * 0.05f);

                    }
                    roomOrderList = roomOrderList.OrderBy(key => -key.AdjNum).ToList();

                    int roomNum;

                    if (RoomInstance.entranceIds.Count > 0 && entranceIndexInRoomAreas >= 0 && placedEntranceRoom == false)
                    {
                        roomNum = entranceIndexInRoomAreas;
                        placedEntranceRoom = true;
                    }
                    else if (roomOrderList[0].AdjNum > 0) // If at least one unplaced room is adjacent to at least one placed room, then place it
                        roomNum = roomOrderList[0].roomNumber;
                    else // If no, then place the most adjacent room overal
                    {
                        roomOrderList = new List<IntPair>();

                        roomOrderList.Add(new IntPair(-1, -1));

                        for (int w = 1; w <= roomList.Count; w++)
                            if (!GridContains(grid, w))
                                roomOrderList.Add(new IntPair(w, 0));
                            else
                                roomOrderList.Add(new IntPair(w, -1));

                        for (int q = 0; q < adjArray.GetLength(0); q++)
                        {
                            if (!GridContains(grid, roomOrderList[adjArray[q, 0]].roomNumber))
                                roomOrderList[adjArray[q, 0]] = new IntPair(roomOrderList[adjArray[q, 0]].roomNumber, roomOrderList[adjArray[q, 0]].AdjNum + 1 + random.NextDouble() * 0.05f);

                            if (!GridContains(grid, roomOrderList[adjArray[q, 1]].roomNumber))
                                roomOrderList[adjArray[q, 1]] = new IntPair(roomOrderList[adjArray[q, 1]].roomNumber, roomOrderList[adjArray[q, 1]].AdjNum + 1 + random.NextDouble() * 0.05f);

                        }
                        roomOrderList = roomOrderList.OrderBy(key => -key.AdjNum).ToList();
                        roomNum = roomOrderList[0].roomNumber;
                    }


                    //if (!TryPlaceNewRoom(roomAreas[roomNum - 1], roomNum, ref grid, adjArray, maxAdjDistance))
                    if (!PlaceNewRoom_TEST(ref grid, roomList[roomNum - 1].RoomArea, roomNum, adjArray, maxAdjDistance, roomList[roomNum - 1].isHall))
                    { break; }

                }


                /*  // Let's find rooms on the grid that have missing connections. Let's do it for every iteration. (May take a lot of operational time)
                  List<int> missingAdj = MissingRoomAdjacences(grid, adjArray);
                  for (int k = 0; k < missingAdj.Count; k++)
                  {
                      missingAdj = MissingRoomAdjacences(grid, adjArray);

                      if (missingAdj[k] > 0 && GridContains(grid, k + 1))
                      {
                          RemoveRoomFromGrid(ref grid, roomCellsList[k]);
                          if (PlaceNewRoom_TEST(ref grid, roomList[k].RoomArea, k + 1, adjArray, maxAdjDistance, roomList[k].isHall))
                              AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Placed ID_" + (k + 1) + ": " + missingAdj[k]);
                      }
                  }
                  */



                if (GridAIsBetterThanB(grid, bestGrid))
                {
                    bestGrid = grid.Clone() as int[,];
                    bestRoomCellsList = roomCellsList.ConvertAll(roomCells => new RoomCells(roomCells));
                }
            }

            // At the end let's convert all needed rooms to halls
            for (int i = 0; i < bestGrid.GetLength(0); i++)
                for (int j = 0; j < bestGrid.GetLength(1); j++)
                    if (bestGrid[i, j] > 0 && bestGrid[i, j] <= roomList.Count)
                        if (roomList[bestGrid[i, j] - 1].isHall)
                            bestGrid[i, j] = -1;

            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                {
                    if (bestGrid[i, j] != 200)
                        gridOutput.Add(bestGrid[i, j]);
                    else
                        gridOutput.Add(0);
                }

            DA.SetDataList(0, gridOutput);
            DA.SetData(1, x);

        }

        public List<int> MissingRoomAdjacences(int[,] grid, int[,] adjArray)
        {
            List<int> missingAdj = new List<int>();// (adjArray.GetLength(0));
            for (int i = 0; i < adjArray.GetLength(0); i++)
                missingAdj.Add(0);

            for (int l = 0; l < adjArray.GetLength(0); l++)
            {
                bool exists = false;
                for (int i = 0; i < grid.GetLength(0); i++)
                    for (int j = 0; j < grid.GetLength(1); j++)
                        if (grid[i, j] == adjArray[l, 0])
                            exists = true;
                if (!exists)
                    missingAdj[adjArray[l, 1] - 1]++;


                exists = false;
                for (int i = 0; i < grid.GetLength(0); i++)
                    for (int j = 0; j < grid.GetLength(1); j++)
                        if (grid[i, j] == adjArray[l, 1])
                            exists = true;
                if (!exists)
                    missingAdj[adjArray[l, 0] - 1]++;
            }

            return missingAdj;
        }

        public bool PlaceNewRoom_TEST(ref int[,] grid, double area, int roomNum, int[,] adjArray, double maxAdjDistance, bool isHall = false)
        {
            int[,] availableCellsGrid = new int[grid.GetLength(0), grid.GetLength(1)];  //= grid;
            int[,] room = new int[50, 50];

            int xDim;
            int yDim;

            List<int> adjacentRooms = new List<int>();

            for (int i = 0; i < adjArray.GetLength(0); i++)
                if (adjArray[i, 0] == roomNum && GridContains(grid, adjArray[i, 1]))
                    adjacentRooms.Add(adjArray[i, 1]);
                else if (adjArray[i, 1] == roomNum && GridContains(grid, adjArray[i, 0]))
                    adjacentRooms.Add(adjArray[i, 0]);


            //  for (int i = 0; i < adjArray.GetLength(0); i++)
            //      Rhino.RhinoApp.WriteLine(adjArray[i,0].ToString() + " + "+ adjArray[i, 1].ToString());

            List<int> divisors = new List<int>();
            for (int i = 2; i <= Math.Sqrt(area); i++)
                if (area % i == 0)
                    divisors.Add(i);

            area--;
            do
            {
                divisors.Clear();
                area++;
                for (int i = 1; i <= Math.Sqrt(area); i++)
                    if (area % i == 0)
                        divisors.Add(i);

            } while (area / (double)divisors[divisors.Count - 1] / (double)divisors[divisors.Count - 1] > 1.5f);

            xDim = divisors[divisors.Count - 1];
            yDim = (int)(area / xDim);

            int temp = xDim;
            if (random.Next(2) == 0)
            {
                xDim = yDim;
                yDim = temp;
            }


            // Choose the corridor generation mode according to MenuItemDropDown selection
            if (oneSideCorridorsChecked)
            {
                if (random.Next(2) == 0)
                {
                    room = new int[xDim + 1, yDim];
                    for (int i = 0; i < xDim + 1; i++)
                        for (int j = 0; j < yDim; j++)
                            if (i == 0)
                                room[i, j] = -1;
                            else
                                room[i, j] = roomNum;
                }
                else
                {
                    room = new int[xDim, yDim + 1];
                    for (int i = 0; i < xDim; i++)
                        for (int j = 0; j < yDim + 1; j++)
                            if (j == 0)
                                room[i, j] = -1;
                            else
                                room[i, j] = roomNum;
                }

            }
            else if (twoSidesCorridorsChecked)
            {
                room = new int[xDim + 1, yDim + 1];
                for (int i = 0; i < xDim + 1; i++)
                    for (int j = 0; j < yDim + 1; j++)
                        if (i == 0 || j == 0)
                            room[i, j] = -1;
                        else
                            room[i, j] = roomNum;
            }
            else if (allSidesCorridorsChecked)
            {
                xDim--;
                yDim--;

                room = new int[xDim + 2, yDim + 2];
                for (int i = 0; i <= xDim + 1; i++)
                    for (int j = 0; j <= yDim + 1; j++)
                        if (i == 0 || j == 0 || i == xDim + 1 || j == yDim + 1)
                            room[i, j] = -1;
                        else
                            room[i, j] = roomNum;
            }

            // Start filling availableCellsGrid: 0 = not available, 1 = available
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    availableCellsGrid[i, j] = 0;

                    if (grid[i, j] == 0)
                        for (int l = -1; l <= 1; l++)
                            for (int k = -1; k <= 1; k++)
                                if ((l == 0 || k == 0) && l != k)
                                    if (i + l >= 0 && i + l < grid.GetLength(0) && j + k >= 0 && j + k < grid.GetLength(1))
                                        if (grid[i + l, j + k] == -1)
                                        {
                                            if (CellsAreNearerThan(i, j, adjacentRooms, grid, maxAdjDistance))
                                            {
                                                availableCellsGrid[i, j] = 1;
                                                // AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "avalCell! " + roomNum + ": " + i + "_" + j);
                                            }
                                        }
                }


            List<RoomPlacementSolution> placementSolutions = new List<RoomPlacementSolution>();



            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {

                    if (availableCellsGrid[i, j] == 1)
                    {
                        if (RoomIsPlaceable_TEST(grid, room, i, j, RoomPosition.BottomLeft))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.BottomLeft, room
                                , GetRoomScore(grid, room, i, j, RoomPosition.BottomLeft));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                        if (RoomIsPlaceable_TEST(grid, room, i, j, RoomPosition.BottomRight))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.BottomRight, room
                                , GetRoomScore(grid, room, i, j, RoomPosition.BottomRight));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                        if (RoomIsPlaceable_TEST(grid, room, i, j, RoomPosition.TopLeft))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.TopLeft, room
                                , GetRoomScore(grid, room, i, j, RoomPosition.TopLeft));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                        if (RoomIsPlaceable_TEST(grid, room, i, j, RoomPosition.TopRight))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.TopRight, room
                                , GetRoomScore(grid, room, i, j, RoomPosition.TopRight));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                    }
                }


            //placementSolutions = placementSolutions.OrderBy(a => new Random(Guid.NewGuid().GetHashCode()).Next(2) == 0).ToList();
            placementSolutions = placementSolutions.OrderBy(t => -t.score).ToList();

            if (placementSolutions.Count > 0)
            {
                int x = placementSolutions[0].x;
                int y = placementSolutions[0].y;
                int w = placementSolutions[0].room.GetLength(0);
                int h = placementSolutions[0].room.GetLength(1);

                if (placementSolutions[0].roomPosition == RoomPosition.BottomLeft || placementSolutions[0].roomPosition == RoomPosition.TopLeft)
                    x -= w - 1;
                if (placementSolutions[0].roomPosition == RoomPosition.BottomLeft || placementSolutions[0].roomPosition == RoomPosition.BottomRight)
                    y -= h - 1;

                roomCellsList[roomNum - 1] = new RoomCells(x, y, w, h);


                PlaceRoomSolution(placementSolutions[0], placementSolutions[0].room, ref grid, isHall);
                return true;
            }
            else
                return false;
        }

        private double DistanceToRoomNumber_TEST(int[,] grid, int x, int y, int targetNum, double maxDistance = 20)
        {

            bool[,] availabilityGrid = new bool[grid.GetLength(0), grid.GetLength(1)];
            double[,] distanceGrid = new double[grid.GetLength(0), grid.GetLength(1)];

            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (grid[i, j] == -1)
                        availabilityGrid[i, j] = true;
                    else
                        availabilityGrid[i, j] = false;

                    distanceGrid[i, j] = -1;
                }

            distanceGrid[x, y] = 0;

            SetCellDistancesAround(x, y, ref distanceGrid, ref availabilityGrid, maxDistance);

            double minDist = 5000;

            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    for (int l = -1; l <= 1; l++)
                        for (int k = -1; k <= 1; k++)
                            if ((l == 0 || k == 0) && l != k)
                                if (i + l >= 0 && i + l < grid.GetLength(0) && j + k >= 0 && j + k < grid.GetLength(1))

                                    if (grid[i + l, j + k] == targetNum && distanceGrid[i, j] != -1)
                                        minDist = Math.Min(minDist, distanceGrid[i, j]);
                }

            return minDist;
        }

        private class RoomCells
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
        // Now add method for saving RoomCells to a list and then implement removing rooms in case of them having missing adj

        private void RemoveRoomFromGrid(ref int[,] grid, RoomCells roomCells)
        {
            for (int i = roomCells.x; i < roomCells.x + roomCells.w; i++)
                for (int j = roomCells.y; j < roomCells.y + roomCells.h; j++)
                    grid[i, j] = 0;
        }

        private void PlaceRoomSolution(RoomPlacementSolution solution, int[,] room, ref int[,] grid, bool isHall = false)
        {
            switch (solution.roomPosition)
            {
                case RoomPosition.TopRight:
                    if (solution.x + room.GetLength(0) < grid.GetLength(0) && solution.y + room.GetLength(1) < grid.GetLength(1))
                    {
                        for (int i = solution.x; i < solution.x + room.GetLength(0); i++)
                            for (int j = solution.y; j < solution.y + room.GetLength(1); j++)
                                grid[i, j] = room[i - solution.x, j - solution.y];
                    }
                    break;

                case RoomPosition.BottomRight:
                    if (solution.x + room.GetLength(0) < grid.GetLength(0) && solution.y - room.GetLength(1) >= 0)
                    {
                        for (int i = solution.x; i < solution.x + room.GetLength(0); i++)
                            for (int j = solution.y; j > solution.y - room.GetLength(1); j--)
                                grid[i, j] = room[i - solution.x, -(j - solution.y)];
                    }
                    break;

                case RoomPosition.BottomLeft:
                    if (solution.x - room.GetLength(0) >= 0 && solution.y - room.GetLength(1) >= 0)
                    {
                        for (int i = solution.x; i > solution.x - room.GetLength(0); i--)
                            for (int j = solution.y; j > solution.y - room.GetLength(1); j--)
                                grid[i, j] = room[-(i - solution.x), -(j - solution.y)];
                    }
                    break;

                case RoomPosition.TopLeft:
                    if (solution.x - room.GetLength(0) >= 0 && solution.y + room.GetLength(1) < grid.GetLength(1))
                    {
                        for (int i = solution.x; i > solution.x - room.GetLength(0); i--)
                            for (int j = solution.y; j < solution.y + room.GetLength(1); j++)
                                grid[i, j] = room[-(i - solution.x), j - solution.y];
                    }
                    break;
            }
        }

        private int GetRoomScore(int[,] grid, int[,] room, int x, int y, RoomPosition roomPosition)
        {
            int score = 0;

            List<int> cellsToCheck = new List<int>();



            if (roomPosition == RoomPosition.BottomLeft)
            {
                for (int i = 0; i < grid.GetLength(0); i++)
                {
                    if (x - i >= 0 && y + 1 < grid.GetLength(1))
                        cellsToCheck.Add(grid[x - i, y + 1]);

                    if (x - i >= 0 && y - room.GetLength(1) - 1 >= 0)
                        cellsToCheck.Add(grid[x - i, y - room.GetLength(1) - 1]);
                }

                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (x + 1 < grid.GetLength(0) && y - j >= 0)
                        cellsToCheck.Add(grid[x + 1, y - j]);

                    if (x - room.GetLength(0) - 1 >= 0 && y - j >= 0)
                        cellsToCheck.Add(grid[x - room.GetLength(0) - 1, y - j]);
                }
            }

            if (roomPosition == RoomPosition.BottomRight)
            {
                for (int i = 0; i < grid.GetLength(0); i++)
                {
                    if (x + i < grid.GetLength(0) && y + 1 < grid.GetLength(1))
                        cellsToCheck.Add(grid[x + i, y + 1]);

                    if (x + i < grid.GetLength(0) && y - room.GetLength(1) - 1 >= 0)
                        cellsToCheck.Add(grid[x + i, y - room.GetLength(1) - 1]);
                }

                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (x - 1 >= 0 && y - j >= 0)
                        cellsToCheck.Add(grid[x - 1, y - j]);

                    if (x + room.GetLength(0) + 1 < grid.GetLength(0) && y - j > 0)
                        cellsToCheck.Add(grid[x + room.GetLength(0) + 1, y - j]);
                }
            }

            if (roomPosition == RoomPosition.TopLeft)
            {
                for (int i = 0; i < grid.GetLength(0); i++)
                {
                    if (x - i >= 0 && y - 1 >= 0)
                        cellsToCheck.Add(grid[x - i, y - 1]);

                    if (x - i >= 0 && y + room.GetLength(1) + 1 < grid.GetLength(1))
                        cellsToCheck.Add(grid[x - i, y + room.GetLength(1) + 1]);
                }

                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (x + 1 < grid.GetLength(0) && y + j < grid.GetLength(1))
                        cellsToCheck.Add(grid[x + 1, y + j]);

                    if (x - room.GetLength(0) - 1 >= 0 && y + j < grid.GetLength(1))
                        cellsToCheck.Add(grid[x - room.GetLength(0) - 1, y + j]);
                }
            }

            if (roomPosition == RoomPosition.TopRight)
            {
                for (int i = 0; i < grid.GetLength(0); i++)
                {
                    if (x + i < grid.GetLength(0) && y - 1 >= 0)
                        cellsToCheck.Add(grid[x + i, y - 1]);

                    if (x + i < grid.GetLength(0) && y + room.GetLength(1) + 1 < grid.GetLength(1))
                        cellsToCheck.Add(grid[x + i, y + room.GetLength(1) + 1]);
                }

                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (x - 1 >= 0 && y + j < grid.GetLength(1))
                        cellsToCheck.Add(grid[x - 1, y + j]);

                    if (x + room.GetLength(0) + 1 < grid.GetLength(0) && y + j < grid.GetLength(1))
                        cellsToCheck.Add(grid[x + room.GetLength(0) + 1, y + j]);
                }
            }

            foreach (int a in cellsToCheck)
                if (a != 0 && a != 200)
                    score++;

            return score;
        }

        private class RoomPlacementSolution
        {
            public RoomPlacementSolution(int roomX, int roomY, RoomPosition position, int[,] mRoom, int mScore)
            {
                x = roomX;
                y = roomY;
                roomPosition = position;
                this.score = mScore;
                this.room = mRoom.Clone() as int[,];
            }

            public RoomPlacementSolution(RoomPlacementSolution a)
            {
                x = a.x;
                y = a.y;
                roomPosition = a.roomPosition;
                this.score = a.score;
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

            public RoomPosition roomPosition;
            public int score = 0;
            public int x;
            public int y;
            public int[,] room;
        }

        private enum RoomPosition { TopRight, BottomRight, BottomLeft, TopLeft, Undefined }


        private bool RoomIsPlaceable_TEST(int[,] grid, int[,] room, int x, int y, RoomPosition roomPosition)
        {
            switch (roomPosition)
            {
                case RoomPosition.TopRight:
                    if (x + room.GetLength(0) < grid.GetLength(0) && y + room.GetLength(1) < grid.GetLength(1))
                    {
                        for (int i = x; i < x + room.GetLength(0); i++)
                            for (int j = y; j < y + room.GetLength(1); j++)
                                if (grid[i, j] != 0)
                                    return false;
                        return true;
                    }
                    break;

                case RoomPosition.BottomRight:
                    if (x + room.GetLength(0) < grid.GetLength(0) && y - room.GetLength(1) >= 0)
                    {
                        for (int i = x; i < x + room.GetLength(0); i++)
                            for (int j = y; j > y - room.GetLength(1); j--)
                                if (grid[i, j] != 0)
                                    return false;
                        return true;
                    }
                    break;

                case RoomPosition.BottomLeft:
                    if (x - room.GetLength(0) >= 0 && y - room.GetLength(1) >= 0)
                    {
                        for (int i = x; i > x - room.GetLength(0); i--)
                            for (int j = y; j > y - room.GetLength(1); j--)
                                if (grid[i, j] != 0)
                                    return false;
                        return true;
                    }
                    break;

                case RoomPosition.TopLeft:
                    if (x - room.GetLength(0) >= 0 && y + room.GetLength(1) < grid.GetLength(1))
                    {
                        for (int i = x; i > x - room.GetLength(0); i--)
                            for (int j = y; j < y + room.GetLength(1); j++)
                                if (grid[i, j] != 0)
                                    return false;
                        return true;
                    }
                    break;

            }
            return false;
        }



        public struct IntPair
        {
            public IntPair(int a1, int b1)
            {
                roomNumber = a1;
                AdjNum = b1;
            }

            public IntPair(int a1, double b1)
            {
                roomNumber = a1;
                AdjNum = b1;
            }
            public int roomNumber;
            public double AdjNum;
        }

        public bool CellsAreNearerThan(int x, int y, List<int> targetCellsList, int[,] grid, double maxDistance = 2)
        {
            foreach (int targetCell in targetCellsList)
            {
                if (!CellIsNearerThan(x, y, targetCell, grid, maxDistance))
                    return false;
            }
            return true;
        }



        public bool CellIsNearerThan(int x, int y, int targetCellNumber, int[,] grid, double maxDistance = 2)
        {
            bool[,] availabilityGrid = new bool[grid.GetLength(0), grid.GetLength(1)];
            double[,] distanceGrid = new double[grid.GetLength(0), grid.GetLength(1)];

            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (grid[i, j] == -1)
                        availabilityGrid[i, j] = true;
                    else
                        availabilityGrid[i, j] = false;
                    distanceGrid[i, j] = -1;
                }

            distanceGrid[x, y] = 0;
            availabilityGrid[x, y] = true;

            SetCellDistancesAround(x, y, ref distanceGrid, ref availabilityGrid, maxDistance);


            int minX = -1;
            int minY = -1;
            double minTargetDist = -1;

            for (int i = 0; i < availabilityGrid.GetLength(0); i++)
                for (int j = 0; j < availabilityGrid.GetLength(1); j++)
                {
                    if (grid[i, j] == targetCellNumber)
                        if ((distanceGrid[i, j] < minTargetDist || minTargetDist == -1) && distanceGrid[i, j] > 0)
                        {
                            minTargetDist = distanceGrid[i, j];
                            minX = i;
                            minY = j;
                        }
                }

            if (minTargetDist != -1 && minTargetDist <= maxDistance)
                return true;
            else
                return false;
        }

        public void SetCellDistancesAround(int x, int y, ref double[,] distanceGrid, ref bool[,] availabilityGrid, double maxDist)
        {
            List<List<int>> recursionList = new List<List<int>>();


            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    if (x + i >= 0 && x + i < distanceGrid.GetLength(0) &&
                        y + j >= 0 && y + j < distanceGrid.GetLength(1))
                        if (!(i == 0 && j == 0))
                        {
                            double addValue;
                            if (i * j != 0) //if the cell is corner cell
                                addValue = 1.414f; //sqrt(2)
                            else
                                addValue = 1;

                            if (availabilityGrid[x, y])
                                if (distanceGrid[x + i, y + j] > distanceGrid[x, y] + addValue || distanceGrid[x + i, y + j] == -1)
                                {
                                    distanceGrid[x + i, y + j] = distanceGrid[x, y] + addValue;
                                    if (distanceGrid[x + i, y + j] <= maxDist)
                                        recursionList.Add(new List<int>() { x + i, y + j });
                                }
                        }
                }
            // doesnt really affect performance
            foreach (List<int> item in recursionList)
                SetCellDistancesAround(item[0], item[1], ref distanceGrid, ref availabilityGrid, maxDist);

            return;
        }

        bool GridAIsBetterThanB(int[,] A, int[,] B)
        {
            int aCount = 0;
            int bCount = 0;

            List<int> aRooms = new List<int>();
            List<int> bRooms = new List<int>();

            for (int i = 0; i < A.GetLength(0); i++)
                for (int j = 0; j < A.GetLength(1); j++)
                {
                    if (A[i, j] == 0)
                        aCount++;
                    if (B[i, j] == 0)
                        bCount++;

                    if (aRooms.FindIndex(a => a == A[i, j]) == -1)
                        aRooms.Add(A[i, j]);

                    if (bRooms.FindIndex(b => b == B[i, j]) == -1)
                        bRooms.Add(B[i, j]);

                }

            // return aRooms.Count > bRooms.Count;
            return aCount < bCount;
        }


        public bool GridContains(int[,] grid, int val)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    if (grid[i, j] == val)
                        return true;
            return false;
        }

        /*
        bool TryPlaceNewRoom(int area, int num, ref int[,] grid, int[,] adjArray, double maxAdjDistance = 3f)
        {
            int xDim;
            int yDim;

            List<int> adjacentRooms = new List<int>();

            for (int i = 0; i < adjArray.GetLength(0); i++)
                if (adjArray[i, 0] == num && GridContains(grid, adjArray[i, 1]))
                    adjacentRooms.Add(adjArray[i, 1]);
                else if (adjArray[i, 1] == num && GridContains(grid, adjArray[i, 0]))
                    adjacentRooms.Add(adjArray[i, 0]);


            //  for (int i = 0; i < adjArray.GetLength(0); i++)
            //      Rhino.RhinoApp.WriteLine(adjArray[i,0].ToString() + " + "+ adjArray[i, 1].ToString());

            List<int> divisors = new List<int>();
            for (int i = 2; i <= area; i++)
                if (area % i == 0)
                    divisors.Add(i);

            while (divisors.Count < 4)
            {
                divisors.Clear();
                area++;
                for (int i = 2; i <= area; i++)
                    if (area % i == 0)
                        divisors.Add(i);

            }

            xDim = divisors[divisors.Count / 2 - random.Next(2)];
            yDim = area / xDim;


            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1) - 1; j++)
                {
                    if (grid[i, j] == -1)
                        if (grid[i, j + 1] == 0 && EnoughSpaceOnThe(xDim, yDim, num, i, j + 1, ref grid, true)
                            && CellsAreNearerThan(i, j, adjacentRooms, grid, maxAdjDistance))
                        {
                            EnoughSpaceOnThe(xDim, yDim, num, i, j + 1, ref grid, false);
                            i = grid.GetLength(0);
                            j = grid.GetLength(1);
                            return true;
                        }
                        else if (grid[i, j - 1] == 0 && EnoughSpaceOnThe(xDim, yDim, num, i, j - 1, ref grid, true)
                            && CellsAreNearerThan(i, j, adjacentRooms, grid, maxAdjDistance))
                        {
                            EnoughSpaceOnThe(xDim, yDim, num, i, j - 1, ref grid, false);
                            i = grid.GetLength(0);
                            j = grid.GetLength(1);
                            return true;
                        }

                        else if (grid[i + 1, j] == 0 && EnoughSpaceOnThe(xDim, yDim, num, i + 1, j, ref grid, true)
                            && CellsAreNearerThan(i, j, adjacentRooms, grid, maxAdjDistance))
                        {
                            EnoughSpaceOnThe(xDim, yDim, num, i + 1, j, ref grid, false);
                            i = grid.GetLength(0);
                            j = grid.GetLength(1);
                            return true;
                        }
                        else if (grid[i - 1, j] == 0 && EnoughSpaceOnThe(xDim, yDim, num, i - 1, j, ref grid, true)
                            && CellsAreNearerThan(i, j, adjacentRooms, grid, maxAdjDistance))
                        {
                            EnoughSpaceOnThe(xDim, yDim, num, i - 1, j, ref grid, false);
                            i = grid.GetLength(0);
                            j = grid.GetLength(1);
                            return true;
                        }
                }

            return false;
        }
        */

        private bool EnoughSpaceOnThe(int xDim, int yDim, int roomNum, int gridX, int gridY, ref int[,] grid, bool preserveGrid = false)
        {

            // roomNum++; // Yes, coz rooms should start from 1, not from 0 // No, coz this solution sucks

            int xM = 1;
            int yM = 1;


            for (int it = 0; it < 4; it++)
            {

                switch (it)
                {
                    default: break;

                    case (0):
                        xM = 1;
                        yM = 1;
                        break;

                    case (1):
                        xM = -1;
                        yM = 1;
                        break;

                    case (2):
                        xM = 1;
                        yM = -1;
                        break;
                    case (3):
                        xM = -1;
                        yM = -1;
                        break;
                }

                if (gridX + xDim * xM >= grid.GetLength(0) || gridX + xDim * xM < 0 ||
                    gridY + yDim * yM >= grid.GetLength(1) || gridY + yDim * yM < 0)
                {
                    break;
                }

                bool xDimIsLessThanYDim = false;
                bool solutionFound = false;

                if (xDim > yDim)
                {
                    xDim++;
                    xDimIsLessThanYDim = true;
                }
                else
                {
                    xDimIsLessThanYDim = false;
                    yDim++;
                }

                // if one of the cells is not free -> terminate
                solutionFound = true;
                for (int i = 0; i < xDim; i++)
                    for (int j = 0; j < yDim; j++)
                    {
                        if (grid[gridX + i * xM, gridY + j * yM] != 0)
                        {
                            // break
                            solutionFound = false;
                            i = xDim;
                            j = yDim;
                        }
                    }

                if (solutionFound)
                {
                    if (!preserveGrid)
                        for (int i = 0; i < xDim; i++)
                            for (int j = 0; j < yDim; j++)
                            {
                                if ((xDimIsLessThanYDim && i == 0) || (!xDimIsLessThanYDim && j == 0))
                                    grid[gridX + i * xM, gridY + j * yM] = -1;
                                else
                                    grid[gridX + i * xM, gridY + j * yM] = roomNum;
                            }

                    return true;
                }

                if (xDimIsLessThanYDim)
                {
                    xDim--;
                    yDim++;
                    xDimIsLessThanYDim = false;
                }
                else
                {
                    xDimIsLessThanYDim = true;
                    yDim--;
                    xDim++;
                }

                solutionFound = true;
                for (int i = 0; i < xDim; i++)
                    for (int j = 0; j < yDim; j++)
                    {
                        if (grid[gridX + i * xM, gridY + j * yM] != 0)
                        {
                            // break
                            solutionFound = false;
                            i = xDim;
                            j = yDim;
                        }
                    }


                if (solutionFound)
                {
                    if (!preserveGrid)
                        for (int i = 0; i < xDim; i++)
                            for (int j = 0; j < yDim; j++)
                            {
                                if ((xDimIsLessThanYDim && i == 0) || (!xDimIsLessThanYDim && j == 0))
                                    grid[gridX + i * xM, gridY + j * yM] = -1;
                                else
                                    grid[gridX + i * xM, gridY + j * yM] = roomNum;
                            }

                    return true;
                }
            }
            return false;
        }

        private void Shuffle(ref List<int> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                int value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }




        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{78fe6801-611b-453f-946a-2fda953393eb}"); }
        }


        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            DropDown_OneSideCor = Menu_AppendItem(menu, "One-side corridors", Menu_OneSideCorClick, true, oneSideCorridorsChecked);
            DropDown_TwoSidesCor = Menu_AppendItem(menu, "Two-sides corridors", Menu_TwoSidesCorClick, true, twoSidesCorridorsChecked);
            DropDown_AllSidesCor = Menu_AppendItem(menu, "All-sides corridors", Menu_AllSidesCorClick, true, allSidesCorridorsChecked);

            base.AppendAdditionalComponentMenuItems(menu);
        }

        public void Menu_OneSideCorClick(object sender, EventArgs e)
        {
            if (!oneSideCorridorsChecked)
            {
                oneSideCorridorsChecked = !oneSideCorridorsChecked;

                twoSidesCorridorsChecked = !oneSideCorridorsChecked;
                allSidesCorridorsChecked = !oneSideCorridorsChecked;

                ExpireSolution(true);
            }
        }

        public void Menu_TwoSidesCorClick(object sender, EventArgs e)
        {
            if (!twoSidesCorridorsChecked)
            {
                twoSidesCorridorsChecked = !twoSidesCorridorsChecked;

                oneSideCorridorsChecked = !twoSidesCorridorsChecked;
                allSidesCorridorsChecked = !twoSidesCorridorsChecked;

                ExpireSolution(true);
            }
        }

        public void Menu_AllSidesCorClick(object sender, EventArgs e)
        {
            if (!allSidesCorridorsChecked)
            {
                allSidesCorridorsChecked = !allSidesCorridorsChecked;

                oneSideCorridorsChecked = !allSidesCorridorsChecked;
                twoSidesCorridorsChecked = !allSidesCorridorsChecked;

                ExpireSolution(true);
            }
        }
    }
}