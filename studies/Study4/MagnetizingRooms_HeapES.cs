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
    public class MagnetizingRooms_HeapES : GH_Component
    {

        Random random = new Random();

        List<RoomCells> bestRoomCellsList = new List<RoomCells>(); // The one that actually means something. It stores room's dimensions for all rooms of bestGrid
        List<RoomCells> roomCellsList = new List<RoomCells>();
        List<GridSolution> gridSolutionsHeap;

        // That's needed for AppendAdditionalMenuItems functions.
        bool oneSideCorridorsChecked = false;
        bool twoSidesCorridorsChecked = true;
        bool allSidesCorridorsChecked = false;

        bool corridorsAsAdditionalSpacesChecked = true;

        bool shouldOnlyRecomputeDeadEnds = false;

        bool removeDeadEndsChecked = true;
        bool removeAllCorridorsChecked = false;

        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public MagnetizingRooms_HeapES()
          : base("MagnetizingRooms_HeapES", "MagnetR_HES",
              "MagnetizingRooms_HeapES",
              "FloorPlanGen", "Study_4")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // pManager.AddCurveParameter("Boundary", "Boundary", "Boundary", GH_ParamAccess.item);
            // pManager.AddPointParameter("Starting Points", "Starting Points", "Starting Points", GH_ParamAccess.list);
            // pManager.AddGenericParameter("RoomList", "RoomList", "RoomList", GH_ParamAccess.list);
            pManager.AddGenericParameter("House Instance", "HI", "House Instance", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iterations", "I", "Iteratins (n*n*n)", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("MaxAdjDistance", "MAD", "MaxAdjDistance", GH_ParamAccess.item, 2);
            // pManager.AddTextParameter("Adjacencies", "Adjacencies", "Adjacencies as list of string \"1 - 3, 2 - 4,..\"", GH_ParamAccess.list, "0 - 0");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Corridors", "C", "Corridors as Breps", GH_ParamAccess.item);
            pManager.AddBrepParameter("Room Breps", "Rs", "Rooms as Breps list", GH_ParamAccess.list);
            pManager.AddTextParameter("Room Names", "Ns", "Room Names", GH_ParamAccess.list);
            //pManager.AddSurfaceParameter("GridCells", "GC", "Grid cells as surfaces", GH_ParamAccess.list);
            //pManager.AddIntegerParameter("xGridDim", "G_X", "xGridDim", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Boundary", "B", "Boundary", GH_ParamAccess.item);
            pManager.AddTextParameter("Adjacencies", "A", "Adjacencies as list of string \"1 - 3, 2 - 4,..\"", GH_ParamAccess.item);
            pManager.AddIntegerParameter("MissingAdjacences", "!A", "Missing Adjacences for every room of the list", GH_ParamAccess.list);
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
            List<int> gridOutput = new List<int>();
            List<string> adjStrList = new List<string>();
            double maxAdjDistance = 0;
            int entranceIndexInRoomAreas = -1;
            HouseInstance houseInstance = new HouseInstance();

            GH_ObjectWrapper houseInstanceWrapper = new GH_ObjectWrapper();
            DA.GetData("House Instance", ref houseInstanceWrapper);
            houseInstance = houseInstanceWrapper.Value as HouseInstance;

            List<RoomInstance> roomList = houseInstance.RoomInstances;
            Curve boundaryCrv = houseInstance.boundary;
            int[,] adjArray = houseInstance.adjArray;
            adjStrList = houseInstance.adjStrList;
            List<Point3d> startingPoints = new List<Point3d>();
            startingPoints.Add(houseInstance.startingPoint);

            for (int i = 0; i < roomList.Count; i++)
                if (RoomInstance.entranceIds.Contains(roomList[i].RoomId))
                {
                    entranceIndexInRoomAreas = i + 1;
                    break;
                }

            int x = 0;
            int y = 0;

            DA.GetData("Iterations", ref iterations);
            DA.GetData("MaxAdjDistance", ref maxAdjDistance);

            // Let's deal with setting boundary curve. The curve is rotated so it fits best into rectangle. 
            // Then all cells are generated as surfaces and rotated back again
            // Moreover, we should rotate the starting point also

            double boundaryCurveRotationRad = 0;
            double minBoundaryArea = double.MaxValue;
            if (houseInstance.tryRotateBoundary)

                for (double i = 0; i <= Math.PI / 2f; i += Math.PI / 360f)
                {
                    Curve rotatedBoundaryCurve = boundaryCrv.Duplicate() as Curve;
                    rotatedBoundaryCurve.Rotate(i, Vector3d.ZAxis, rotatedBoundaryCurve.GetBoundingBox(false).Center);
                    double newArea = AreaMassProperties.Compute(new Rectangle3d(new Plane(rotatedBoundaryCurve.GetBoundingBox(false).Center, Vector3d.ZAxis)
                        , rotatedBoundaryCurve.GetBoundingBox(false).Diagonal.X
                        , rotatedBoundaryCurve.GetBoundingBox(false).Diagonal.Y).ToNurbsCurve()).Area;

                    if (newArea < minBoundaryArea)
                    {
                        minBoundaryArea = newArea;
                        boundaryCurveRotationRad = i;
                    }
                }
            Point3d rotationCenter = boundaryCrv.GetBoundingBox(false).Center;
            Point3d tempP = startingPoints[0];
            tempP.Transform(Transform.Rotation(boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter));
            startingPoints[0] = tempP;
            boundaryCrv.Rotate(boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter);


            x = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.X);
            y = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.Y);

            // So the cells are rotated back again, so we can use them

            Point3d originPoint = boundaryCrv.GetBoundingBox(false).Corner(true, true, true);
            Vector3d diagonal = boundaryCrv.GetBoundingBox(false).Diagonal;
            Surface[] gridSurfaceArray = new Surface[x * y];
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    gridSurfaceArray[j + y * i] = new PlaneSurface(new Plane(
                        Point3d.Origin, Vector3d.ZAxis)
                        , new Interval(originPoint.X + (i) * diagonal.X / x, originPoint.X + (i + 1) * diagonal.X / x)
                        , new Interval(originPoint.Y + (j) * diagonal.Y / y, originPoint.Y + (j + 1) * diagonal.Y / y));


            if (houseInstance.tryRotateBoundary)
                for (int i = 0; i < x; i++)
                    for (int j = 0; j < y; j++)
                        gridSurfaceArray[i + x * j].Rotate(-boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter);

            int[,] grid = new int[x, y];
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    if (boundaryCrv.Contains(new Point3d(boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X + i + 0.5f
                    , boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y + j + 0.5f, 0)) == PointContainment.Inside)
                        grid[i, j] = 0;
                    else
                        grid[i, j] = 200;



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


            // Actual program start

            int[,] bestGrid = grid.Clone() as int[,];
            int[,] startGrid = grid.Clone() as int[,];

            List<int> roomOrder = new List<int>();
            bool placedEntranceRoom;

            if (shouldOnlyRecomputeDeadEnds && gridSolutionsHeap != null && gridSolutionsHeap.Count > 0)
            {
                shouldOnlyRecomputeDeadEnds = false;
                iterations = 0;
            }
            else
                gridSolutionsHeap = new List<GridSolution>();

            for (int i = 0; i < iterations; i++)
            {
                for (int l = 0; l < 4; l++)
                {
                    for (int h = 0; h < 1; h++)
                    {
                        if (gridSolutionsHeap.Count > l && i != 0 && i % 3 == 0)
                        {
                            placedEntranceRoom = true;

                            for (int q = 0; q < gridSolutionsHeap[l].grid.GetLength(0); q++)
                                for (int w = 0; w < gridSolutionsHeap[l].grid.GetLength(1); w++)
                                    grid[q, w] = gridSolutionsHeap[l].grid[q, w];

                            roomOrder.Clear();
                            for (int q = 0; q < gridSolutionsHeap[l].roomOrder.Count; q++)
                                roomOrder.Add(gridSolutionsHeap[l].roomOrder[q]);

                            roomCellsList.Clear();
                            for (int q = 0; q < gridSolutionsHeap[l].roomCellsList.Count; q++)
                                roomCellsList.Add(new RoomCells(gridSolutionsHeap[l].roomCellsList[q]));

                            // grid = gridSolutionsHeap[l].grid.Clone() as int[,];
                            // roomOrder = gridSolutionsHeap[l].roomOrder.ConvertAll(p => Convert.ToInt32(p));
                            //roomCellsList = gridSolutionsHeap[l].roomCellsList.ConvertAll(p => new RoomCells(p));

                            // Let's remove last 1-5 rooms from the solution, so we can try to develope it more
                            int roomRemovalCount = 1 + random.Next(5);
                            for (int j = 0; j < roomRemovalCount; j++)
                                if (roomOrder.Count > 1)
                                {
                                    RemoveRoomFromGrid(ref grid, roomCellsList[roomOrder[roomOrder.Count - 1]]);
                                    roomOrder.RemoveAt(roomOrder.Count - 1);
                                }
                        }
                        else
                        {
                            grid = startGrid.Clone() as int[,];
                            roomOrder = new List<int>();
                            placedEntranceRoom = false;

                            roomCellsList.Clear();
                            foreach (RoomInstance room in roomList)
                                roomCellsList.Add(new RoomCells());
                        }

                        // Let's try to place each room in case it is not placed yet
                        for (int j = 0; j < roomList.Count; j++)
                        {
                            if (j == 1 && grid[(int)Math.Floor(startingPoints[0].X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X),
                                    (int)Math.Floor(startingPoints[0].Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y)] == -1 && roomOrder.Count == 1)
                            {
                                grid[(int)Math.Floor(startingPoints[0].X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X),
                                    (int)Math.Floor(startingPoints[0].Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y)] = 0;
                            }
                            List<IntPair> roomOrderList = new List<IntPair>();
                            //roomOrderList.Add(new IntPair(-1, -1));

                            for (int w = 1; w <= roomList.Count; w++)
                                if (!GridContains(grid, w))
                                    roomOrderList.Add(new IntPair(w, 0));
                                else
                                    roomOrderList.Add(new IntPair(w, -1));

                            for (int q = 0; q < adjArray.GetLength(0); q++)
                            {
                                if (GridContains(grid, roomOrderList[adjArray[q, 1] - 1].roomNumber) && !GridContains(grid, roomOrderList[adjArray[q, 0] - 1].roomNumber))
                                    roomOrderList[adjArray[q, 0] - 1] = new IntPair(roomOrderList[adjArray[q, 0] - 1].roomNumber
                                        , roomOrderList[adjArray[q, 0] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);

                                if (GridContains(grid, roomOrderList[adjArray[q, 0] - 1].roomNumber) && !GridContains(grid, roomOrderList[adjArray[q, 1] - 1].roomNumber))
                                    roomOrderList[adjArray[q, 1] - 1] = new IntPair(roomOrderList[adjArray[q, 1] - 1].roomNumber
                                        , roomOrderList[adjArray[q, 1] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);
                            }
                            roomOrderList = roomOrderList.OrderBy(key => -key.AdjNum).ToList();

                            int roomNum;

                            if (RoomInstance.entranceIds.Count > 0 && entranceIndexInRoomAreas >= 0 && placedEntranceRoom == false)
                            {
                                roomNum = entranceIndexInRoomAreas;
                                placedEntranceRoom = true;
                            }
                            // If at least one unplaced room is adjacent to at least one placed room, then place it
                            else if (roomOrderList[0].AdjNum > 0)
                                roomNum = roomOrderList[0].roomNumber;
                            // If no, then place the most adjacent room overal
                            else
                            {
                                roomOrderList = new List<IntPair>();
                                // roomOrderList.Add(new IntPair(-1, -1));

                                for (int w = 1; w <= roomList.Count; w++)
                                    if (!GridContains(grid, w))
                                        roomOrderList.Add(new IntPair(w, 0));
                                    else
                                        roomOrderList.Add(new IntPair(w, -1));

                                for (int q = 0; q < adjArray.GetLength(0); q++)
                                {
                                    if (!GridContains(grid, roomOrderList[adjArray[q, 0] - 1].roomNumber))
                                        roomOrderList[adjArray[q, 0] - 1] = new IntPair(roomOrderList[adjArray[q, 0] - 1].roomNumber
                                            , roomOrderList[adjArray[q, 0] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);

                                    if (!GridContains(grid, roomOrderList[adjArray[q, 1] - 1].roomNumber))
                                        roomOrderList[adjArray[q, 1] - 1] = new IntPair(roomOrderList[adjArray[q, 1] - 1].roomNumber
                                            , roomOrderList[adjArray[q, 1] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);

                                }
                                roomOrderList = roomOrderList.OrderBy(key => -key.AdjNum).ToList();
                                roomNum = roomOrderList[0].roomNumber;
                            }


                            if (!GridContains(grid, roomNum))
                            {
                                if (TryPlaceNewRoomToTheGrid(ref grid, (int)roomList[roomNum - 1].RoomArea, roomNum, adjArray, maxAdjDistance, roomList[roomNum - 1].isHall))
                                    roomOrder.Add(roomNum - 1);
                                else
                                    break;
                            }
                        }

                        // Add this new solution to solution heap and remove the ancestor. That's needed for diversity of variants
                        if (gridSolutionsHeap.Count > l && i % 3 == 0)
                        {
                            if (roomOrder.Count > gridSolutionsHeap[l].roomOrder.Count)
                            {
                                gridSolutionsHeap.Add(new GridSolution(grid.Clone() as int[,], roomCellsList.ConvertAll(roomCells => new RoomCells(roomCells)), roomOrder));
                                gridSolutionsHeap.RemoveAt(l);
                            }
                        }
                        else
                            gridSolutionsHeap.Add(new GridSolution(grid.Clone() as int[,], roomCellsList.ConvertAll(roomCells => new RoomCells(roomCells)), roomOrder));

                    }
                }
                gridSolutionsHeap = gridSolutionsHeap.OrderBy(solution => -solution.roomOrder.Count).ToList();
                if (gridSolutionsHeap.Count > 8)
                    gridSolutionsHeap.RemoveRange(8, Math.Max(0, gridSolutionsHeap.Count - 8));
            }

            gridSolutionsHeap = gridSolutionsHeap.OrderBy(solution => -solution.roomOrder.Count).ToList() as List<GridSolution>;

           // AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, gridSolutionsHeap[0].roomOrder.Count.ToString());
           // AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, gridSolutionsHeap[gridSolutionsHeap.Count - 1].roomOrder.Count.ToString());


            bestGrid = gridSolutionsHeap[0].grid.Clone() as int[,];

            if (removeDeadEndsChecked)
            {
                RemoveDeadEnds(ref bestGrid, gridSolutionsHeap[0].roomCellsList);
                //RemoveDeadEnds(ref bestGrid, gridSolutionsHeap[0].roomCellsList);
            }


                if (removeAllCorridorsChecked)
                RemoveAllCorridors(ref bestGrid, gridSolutionsHeap[0].roomCellsList);


            List<int> placedRoomsNums = new List<int>();
            // Remove all '200' cells, they stand for outside boundary of the building
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                {
                    if (bestGrid[i, j] != 200)
                        gridOutput.Add(bestGrid[i, j]);
                    else
                        gridOutput.Add(0);

                    if (!placedRoomsNums.Contains(bestGrid[i, j]) && bestGrid[i, j] != 0 && bestGrid[i, j] != -1 && bestGrid[i, j] != 200)
                        placedRoomsNums.Add(bestGrid[i, j]);
                }
            placedRoomsNums.Sort();

            // Indicate all RoomInstances that are not placed in the grid on the graph in grasshopper window
            List<int> missingRoomAdj = MissingRoomAdjacences(bestGrid, adjArray);
            for (int i = 0; i < roomList.Count; i++)
                if (!placedRoomsNums.Contains(Convert.ToInt32(i + 1)))
                {
                    if (roomList[i].hasMissingAdj != true)
                        roomList[i].hasMissingAdj = true;
                }
                else
                {
                    if (roomList[i].hasMissingAdj != false)
                        roomList[i].hasMissingAdj = false;
                }

            // missingRoomAdj is not the list that we're looking for. It considers wrong list of rooms (all of them, instead of only placed ones)
            // So we have to fix it a bit
            List<int> missingRoomAdjSortedList = new List<int>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
                missingRoomAdjSortedList.Add(missingRoomAdj[placedRoomsNums[i] - 1]);

            List<string> roomNames = new List<string>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
            {
                if (!roomList[placedRoomsNums[i] - 1].isHall)
                    roomNames.Add(roomList[placedRoomsNums[i] - 1].RoomName);
                else
                    roomNames.Add("&&HALL&&"+ roomList[placedRoomsNums[i] - 1].RoomName);
            }



            // At the end let's convert all needed rooms to halls   
            for (int i = 0; i < bestGrid.GetLength(0); i++)
                for (int j = 0; j < bestGrid.GetLength(1); j++)
                    if (bestGrid[i, j] > 0 && bestGrid[i, j] <= roomList.Count)
                        if (roomList[bestGrid[i, j] - 1].isHall)
                            bestGrid[i, j] = -1;


            List<Brep> roomBrepsList = new List<Brep>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
            {
                List<Brep> cellsCollection = new List<Brep>();
                for (int q = 0; q < gridOutput.Count; q++)
                    if (gridOutput[q] == placedRoomsNums[i])
                        cellsCollection.Add(gridSurfaceArray[q].ToBrep());

                if (Brep.JoinBreps(cellsCollection, 0.01f) != null)
                    roomBrepsList.Add(Brep.JoinBreps(cellsCollection, 0.01f)[0]);

            }

            Brep corridorsBrep = new Brep();
            for (int i = 0; i < placedRoomsNums.Count; i++)
            {
                List<Brep> cellsCollection = new List<Brep>();
                for (int q = 0; q < gridOutput.Count; q++)
                    if (gridOutput[q] == -1)
                        cellsCollection.Add(gridSurfaceArray[q].ToBrep());

                if (Brep.JoinBreps(cellsCollection, 0.01f) != null)
                    corridorsBrep = Brep.JoinBreps(cellsCollection, 0.01f)[0];

            }

            string adjacenciesOutputString = "";
            for (int i = 0; i < adjArray.GetLength(0); i++)
                if (placedRoomsNums.Contains(adjArray[i, 0]) && placedRoomsNums.Contains(adjArray[i, 1]))
                    adjacenciesOutputString += placedRoomsNums.IndexOf(adjArray[i, 0]) + "-" + placedRoomsNums.IndexOf(adjArray[i, 1]) + "\n";

            DA.SetDataList("Room Breps", roomBrepsList);
            DA.SetData("Corridors", corridorsBrep);
            // DA.SetDataList("GridCells", gridSurfaceArray);
            // DA.SetData("xGridDim", x);
            DA.SetDataList("MissingAdjacences", missingRoomAdjSortedList);
            // DA.SetData("Boundary", boundaryCrv);
            DA.SetData("Adjacencies", adjacenciesOutputString);
            DA.SetDataList("Room Names", roomNames);

            this.Message = gridSolutionsHeap[0].roomOrder.Count + " of " + roomList.Count + " placed";
        }

        private void RemoveDeadEnds(ref int[,] grid, List<RoomCells> roomCellsList)
        {
            List<int[]> toRemove = new List<int[]>();
            List<int> newValues = new List<int>();

            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (grid[i, j] == -1)
                    {
                        List<int[]> neighbours = new List<int[]>();
                        int indexInRoomCellsList = -1;

                        for (int q = 0; q < roomCellsList.Count; q++)
                            if (i >= roomCellsList[q].x && i < roomCellsList[q].x + roomCellsList[q].w
                                && j >= roomCellsList[q].y && j < roomCellsList[q].y + roomCellsList[q].h)
                                indexInRoomCellsList = q;

                        if (j >= 0 && j < grid.GetLength(1))
                        {
                            if (i > 0)
                                if (grid[i - 1, j] == -1)
                                    neighbours.Add(new int[] { i - 1, j });
                            if (i < grid.GetLength(0) - 1)
                                if (grid[i + 1, j] == -1)
                                    neighbours.Add(new int[] { i + 1, j });
                        }
                        if (i >= 0 && i < grid.GetLength(0))
                        {
                            if (j > 0)
                                if (grid[i, j - 1] == -1)
                                    neighbours.Add(new int[] { i, j - 1 });
                            if (j < grid.GetLength(1) - 1)
                                if (grid[i, j + 1] == -1)
                                    neighbours.Add(new int[] { i, j + 1 });
                        }

                        if (neighbours.Count == 1)
                        {
                            //toRemove.Add(new int[] { i, j });
                            int iT = i;
                            int jT = j;

                            int iDelta = neighbours[0][0] - i;
                            int jDelta = neighbours[0][1] - j;


                            if (indexInRoomCellsList >= 0)
                                while (jT >= 0 && jT < grid.GetLength(1) && iT >= 0 && iT < grid.GetLength(0)
                                    && grid[iT, jT] == -1 && neighbours.Count <= 2
                                    && iT >= roomCellsList[indexInRoomCellsList].x && iT < roomCellsList[indexInRoomCellsList].x + roomCellsList[indexInRoomCellsList].w
                                && jT >= roomCellsList[indexInRoomCellsList].y && jT < roomCellsList[indexInRoomCellsList].y + roomCellsList[indexInRoomCellsList].h)
                                {
                                    toRemove.Add(new int[] { iT, jT });
                                    newValues.Add(indexInRoomCellsList + 1);
                                    iT += iDelta;
                                    jT += jDelta;

                                    neighbours = new List<int[]>();

                                    if (jT >= 0 && jT < grid.GetLength(1))
                                    {
                                        if (iT > 0)
                                            if (grid[iT - 1, jT] == -1)
                                                neighbours.Add(new int[] { iT - 1, jT });
                                        if (iT < grid.GetLength(0) - 1)
                                            if (grid[iT + 1, jT] == -1)
                                                neighbours.Add(new int[] { iT + 1, jT });
                                    }
                                    if (iT >= 0 && iT < grid.GetLength(0))
                                    {
                                        if (jT > 0)
                                            if (grid[iT, jT - 1] == -1)
                                                neighbours.Add(new int[] { iT, jT - 1 });
                                        if (jT < grid.GetLength(1) - 1)
                                            if (grid[iT, jT + 1] == -1)
                                                neighbours.Add(new int[] { iT, jT + 1 });
                                    }
                                }
                        }
                    }
                }

            for (int i = 0; i < toRemove.Count; i++)
                grid[toRemove[i][0], toRemove[i][1]] = newValues[i];
        }

        private void RemoveAllCorridors(ref int[,] grid, List<RoomCells> roomCellsList)
        {

            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (grid[i, j] == -1)
                    {

                        int indexInRoomCellsList = -1;

                        for (int q = 0; q < roomCellsList.Count; q++)
                            if (i >= roomCellsList[q].x && i < roomCellsList[q].x + roomCellsList[q].w
                                && j >= roomCellsList[q].y && j < roomCellsList[q].y + roomCellsList[q].h)
                                indexInRoomCellsList = q;

                        if (indexInRoomCellsList > -1)
                            grid[i, j] = indexInRoomCellsList + 1;
                    }
                }
        }

        private class GridSolution
        {
            public int[,] grid;
            public List<RoomCells> roomCellsList = new List<RoomCells>();
            public List<int> roomOrder = new List<int>();

            public GridSolution(int[,] Grid, List<RoomCells> RoomCellsList, List<int> RoomOrder)
            {
                grid = Grid;
                roomCellsList = RoomCellsList.ConvertAll(roomCells => new RoomCells(roomCells));
                roomOrder = RoomOrder.ConvertAll(i => i);
            }
        }

        public List<int> MissingRoomAdjacences(int[,] grid, int[,] adjArray)
        {
            List<int> missingAdj = new List<int>();// (adjArray.GetLength(0));
            int maxRoomNum = 0;
            for (int i = 0; i < adjArray.GetLength(0); i++)
                maxRoomNum = Math.Max(Math.Max(maxRoomNum, adjArray[i, 0]), adjArray[i, 1]);

            for (int i = 0; i < maxRoomNum; i++)
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

        public bool TryPlaceNewRoomToTheGrid(ref int[,] grid, int area, int roomNum, int[,] adjArray, double maxAdjDistance, bool isHall = false)
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

            // Let's try to define proportions for the room considering its area and something else I don't know what
            /*  List<int> divisors = new List<int>();
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
              yDim = area / xDim;
              int temp = xDim;
              if (random.Next(2) == 0)
              {
                  xDim = yDim;
                  yDim = temp;
              } */

            // New variant:
            double ratio = 1 + random.NextDouble() * 1f;
            double xDim_d = Math.Sqrt(area / ratio);
            double yDim_d = ratio * Math.Sqrt(area / ratio);

            xDim = (int)Math.Round(xDim_d);
            yDim = (int)Math.Round(yDim_d);

            if (xDim == 0)
                xDim++;
            if (yDim == 0)
                yDim++;

            if (random.Next(2) == 0)
            {
                int temp = xDim;
                xDim = yDim;
                yDim = temp;
            }


            // Choose the corridor generation mode according to MenuItemDropDown selection
            if (oneSideCorridorsChecked && !isHall)
            {

                if (random.Next(2) == 0)
                {
                    if (!corridorsAsAdditionalSpacesChecked)
                        xDim--;

                    if (xDim == 0)
                        xDim++;
                    if (yDim == 0)
                        yDim++;

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
                    if (!corridorsAsAdditionalSpacesChecked)
                        yDim--;

                    if (xDim == 0)
                        xDim++;
                    if (yDim == 0)
                        yDim++;

                    room = new int[xDim, yDim + 1];
                    for (int i = 0; i < xDim; i++)
                        for (int j = 0; j < yDim + 1; j++)
                            if (j == 0)
                                room[i, j] = -1;
                            else
                                room[i, j] = roomNum;
                }

            }
            else if (twoSidesCorridorsChecked && !isHall)
            {
                if (!corridorsAsAdditionalSpacesChecked)
                {
                    xDim--;
                    yDim--;
                }

                if (xDim == 0)
                    xDim++;
                if (yDim == 0)
                    yDim++;


                room = new int[xDim + 1, yDim + 1];
                for (int i = 0; i < xDim + 1; i++)
                    for (int j = 0; j < yDim + 1; j++)
                        if (i == 0 || j == 0)
                            room[i, j] = -1;
                        else
                            room[i, j] = roomNum;
            }
            else if (allSidesCorridorsChecked || isHall)
            {
                // xDim--;
                //yDim--;

                if (!corridorsAsAdditionalSpacesChecked)
                {
                    xDim -= 2;
                    yDim -= 2;
                }

                if (xDim <= 0)
                    xDim = 1;
                if (yDim <= 0)
                    yDim = 1;


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
                    x -= w-1;
                if (placementSolutions[0].roomPosition == RoomPosition.BottomLeft || placementSolutions[0].roomPosition == RoomPosition.BottomRight)
                    y -= h-1;

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
                return Properties.Resources.MagnetizingRoomsIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{78fe6801-611b-453f-946a-2fda9514a3eb}"); }
        }


        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "One-side corridors", Menu_OneSideCorClick, true, oneSideCorridorsChecked);
            Menu_AppendItem(menu, "Two-sides corridors", Menu_TwoSidesCorClick, true, twoSidesCorridorsChecked);
            Menu_AppendItem(menu, "All-sides corridors", Menu_AllSidesCorClick, true, allSidesCorridorsChecked);

            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Remove Dead Ends", Menu_RemoveDeadEndsClick, true, removeDeadEndsChecked);
            Menu_AppendItem(menu, "Remove All Corridors", Menu_RemoveAllCorridorsClick, true, removeAllCorridorsChecked);

            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Corridors as additional spaces", Menu_CorridorsAsAdditionalSpacesChecked, true, corridorsAsAdditionalSpacesChecked);

            base.AppendAdditionalComponentMenuItems(menu);
        }

        public void Menu_CorridorsAsAdditionalSpacesChecked(object sender, EventArgs e)
        {
            corridorsAsAdditionalSpacesChecked = !corridorsAsAdditionalSpacesChecked;
            this.ExpireSolution(true);
        }

        public void Menu_RemoveDeadEndsClick(object sender, EventArgs e)
        {
            removeDeadEndsChecked = !removeDeadEndsChecked;

            if (removeDeadEndsChecked)
                removeAllCorridorsChecked = false;

            shouldOnlyRecomputeDeadEnds = true;
            this.ExpireSolution(true);
        }

        public void Menu_RemoveAllCorridorsClick(object sender, EventArgs e)
        {
            removeAllCorridorsChecked = !removeAllCorridorsChecked;
            if (removeAllCorridorsChecked)
                removeDeadEndsChecked = false;

            shouldOnlyRecomputeDeadEnds = true;
            this.ExpireSolution(true);
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