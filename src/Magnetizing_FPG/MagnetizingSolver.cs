using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace Magnetizing_FPG
{
    /// <summary>
    /// Core magnetizing floor plan generation algorithm solver.
    /// This class contains the pure algorithm logic without any Grasshopper dependencies.
    /// </summary>
    public class MagnetizingSolver
    {
        // Algorithm state variables - moved from MagnetizingRooms_ES
        private Random random;
        private List<RoomCells> roomCellsList = new List<RoomCells>();
        private List<GridSolution> gridSolutionsCollection;

        // Algorithm constants
        private const double MaxRatio = 1.9f;

        /// <summary>
        /// Main algorithm execution method.
        /// Takes solver inputs and returns solver outputs.
        /// </summary>
        /// <param name="inputs">All input parameters for the algorithm</param>
        /// <returns>All output results from the algorithm</returns>
        public SolverOutputs Execute(SolverInputs inputs)
        {
            var outputs = new SolverOutputs();

            // Initialize random generator with seed
            if (inputs.RandomSeed == 0)
            {
                // Use time-based seed for random behavior
                random = new Random();
            }
            else
            {
                // Use provided seed for deterministic behavior
                random = new Random(inputs.RandomSeed);
            }

            // Extract inputs for easier access
            IHouseInstance houseInstance = inputs.HouseInstance;
            int iterations = inputs.Iterations;
            double maxAdjDistance = inputs.MaxAdjDistance;
            double oneCellSize = inputs.CellSize;
            double boundaryOffset = inputs.BoundaryOffset;

            // Get room and adjacency data from house instance
            List<IRoomInstance> initialRoomsList = houseInstance.RoomInstances;
            Curve boundaryCrv = houseInstance.boundary;
            int[,] adjArray = houseInstance.adjArray;
            List<string> adjStrList = houseInstance.adjStrList;

            // Set boundary outputs
            outputs.Boundary = boundaryCrv;
            boundaryCrv = boundaryCrv.Offset(Plane.WorldXY, boundaryOffset, 0.001f, CurveOffsetCornerStyle.Sharp)[0];
            outputs.BoundaryWithOffset = boundaryCrv;

            // Scale boundary and starting point for grid cell size
            boundaryCrv.Scale(1 / oneCellSize);
            Point3d startingPoint = houseInstance.startingPoint;
            startingPoint.Transform(Transform.Scale(new Point3d(0, 0, 0), 1 / oneCellSize));

            // Setup starting points
            List<Point3d> startingPoints = new List<Point3d>();
            startingPoints.Add(startingPoint);

            // Find entrance room index
            int entranceIndexInRoomAreas = -1;
            for (int i = 0; i < initialRoomsList.Count; i++)
                if (RoomInstance.entranceIds.Contains(initialRoomsList[i].RoomId))
                {
                    entranceIndexInRoomAreas = i + 1;
                    break;
                }

            // Handle boundary rotation if enabled
            double boundaryCurveRotationRad = 0;
            double minBoundaryArea = double.MaxValue;
            if (houseInstance.tryRotateBoundary)
            {
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
            }

            Point3d rotationCenter = boundaryCrv.GetBoundingBox(false).Center;
            Point3d tempP = startingPoints[0];
            tempP.Transform(Transform.Rotation(boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter));
            startingPoints[0] = tempP;
            boundaryCrv.Rotate(boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter);

            // Calculate grid dimensions
            int x = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.X);
            int y = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.Y);

            Point3d originPoint = boundaryCrv.GetBoundingBox(false).Corner(true, true, true);
            Vector3d diagonal = boundaryCrv.GetBoundingBox(false).Diagonal;

            // Generate grid surface array
            Surface[] gridSurfaceArray = new Surface[x * y];
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    gridSurfaceArray[j + y * i] = new PlaneSurface(new Plane(
                        Point3d.Origin, Vector3d.ZAxis)
                        , new Interval(originPoint.X + (i) * diagonal.X / x, originPoint.X + (i + 1) * diagonal.X / x)
                        , new Interval(originPoint.Y + (j) * diagonal.Y / y, originPoint.Y + (j + 1) * diagonal.Y / y));

            // Rotate surfaces back if boundary was rotated
            if (houseInstance.tryRotateBoundary)
                for (int i = 0; i < x; i++)
                    for (int j = 0; j < y; j++)
                        gridSurfaceArray[i + x * j].Rotate(-boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter);

            // Scale surfaces back to original size
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    gridSurfaceArray[i + x * j].Scale(oneCellSize);

            // Initialize working grid
            int[,] workingGrid = new int[x, y];
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    if (boundaryCrv.Contains(new Point3d(boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X + i + 0.5f
                    , boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y + j + 0.5f, 0)) == PointContainment.Inside)
                        workingGrid[i, j] = 0;
                    else
                        workingGrid[i, j] = 9999;

            // Set starting points in grid
            if (startingPoints == null)
                workingGrid[x / 2, y / 2] = -1;
            else
                foreach (Point3d point in startingPoints)
                {
                    if (boundaryCrv.Contains(point) == PointContainment.Inside)
                    {
                        int xIndex = (int)Math.Floor(point.X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X);
                        int yIndex = (int)Math.Floor(point.Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y);
                        workingGrid[xIndex, yIndex] = -1;
                    }
                }

            // Save initial working grid state
            int[,] initialWorkingGrid = workingGrid.Clone() as int[,];

            // Initialize algorithm variables
            List<int> placedRoomsOrderedList = new List<int>();
            bool placedEntranceRoom;

            // Handle recomputation vs new solutions
            bool shouldOnlyRecomputeDeadEnds = false; // This was a component state, now defaulting to false
            if (shouldOnlyRecomputeDeadEnds && gridSolutionsCollection != null && gridSolutionsCollection.Count > 0)
            {
                shouldOnlyRecomputeDeadEnds = false;
                iterations = 0;
            }
            else
                gridSolutionsCollection = new List<GridSolution>();

            // Main algorithm loop
            for (int currentIteration = 0; currentIteration < iterations; currentIteration++)
            {
                int gridSolutionCapacity = 5;
                for (int gridSolutionCurrentIndex = 0; gridSolutionCurrentIndex < gridSolutionCapacity; gridSolutionCurrentIndex++)
                {
                    int newSolutionsFrequency = 3;

                    // Handle solution modification vs new generation
                    if (gridSolutionsCollection.Count > gridSolutionCurrentIndex && currentIteration != 0 && currentIteration % newSolutionsFrequency == 0)
                    {
                        // Restore from previous solution and modify
                        placedEntranceRoom = true;

                        for (int q = 0; q < gridSolutionsCollection[gridSolutionCurrentIndex].grid.GetLength(0); q++)
                            for (int w = 0; w < gridSolutionsCollection[gridSolutionCurrentIndex].grid.GetLength(1); w++)
                                workingGrid[q, w] = gridSolutionsCollection[gridSolutionCurrentIndex].grid[q, w];

                        placedRoomsOrderedList.Clear();
                        for (int q = 0; q < gridSolutionsCollection[gridSolutionCurrentIndex].placedRoomsOrderedList.Count; q++)
                            placedRoomsOrderedList.Add(gridSolutionsCollection[gridSolutionCurrentIndex].placedRoomsOrderedList[q]);

                        roomCellsList.Clear();
                        for (int q = 0; q < gridSolutionsCollection[gridSolutionCurrentIndex].roomCellsList.Count; q++)
                            roomCellsList.Add(new RoomCells(gridSolutionsCollection[gridSolutionCurrentIndex].roomCellsList[q]));

                        // Remove last placed rooms to try different placements
                        int roomRemovalCount = 1 + random.Next(5);
                        for (int j = 0; j < roomRemovalCount; j++)
                            if (placedRoomsOrderedList.Count > 1)
                            {
                                RemoveRoomFromGrid(ref workingGrid, roomCellsList[placedRoomsOrderedList[placedRoomsOrderedList.Count - 1]]);
                                placedRoomsOrderedList.RemoveAt(placedRoomsOrderedList.Count - 1);
                            }
                    }
                    else
                    {
                        // Generate new solution
                        workingGrid = initialWorkingGrid.Clone() as int[,];
                        placedRoomsOrderedList = new List<int>();
                        placedEntranceRoom = false;

                        roomCellsList.Clear();
                        foreach (IRoomInstance room in initialRoomsList)
                            roomCellsList.Add(new RoomCells());
                    }

                    // Try to place each room
                    for (int j = 0; j < initialRoomsList.Count; j++)
                    {
                        // Remove starting point after first room is placed
                        if (j == 1 && workingGrid[(int)Math.Floor(startingPoints[0].X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X),
                                (int)Math.Floor(startingPoints[0].Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y)] == -1 && placedRoomsOrderedList.Count == 1)
                        {
                            workingGrid[(int)Math.Floor(startingPoints[0].X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X),
                                (int)Math.Floor(startingPoints[0].Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y)] = 0;
                        }

                        // Calculate room priorities
                        List<IntPair> roomsOrderList = new List<IntPair>();

                        for (int w = 1; w <= initialRoomsList.Count; w++)
                            if (!GridContains(workingGrid, w))
                                roomsOrderList.Add(new IntPair(w, 0));
                            else
                                roomsOrderList.Add(new IntPair(w, -1));

                        // Fill priorities based on adjacencies
                        for (int q = 0; q < adjArray.GetLength(0); q++)
                        {
                            if (GridContains(workingGrid, roomsOrderList[adjArray[q, 1] - 1].roomNumber) && !GridContains(workingGrid, roomsOrderList[adjArray[q, 0] - 1].roomNumber))
                                roomsOrderList[adjArray[q, 0] - 1] = new IntPair(roomsOrderList[adjArray[q, 0] - 1].roomNumber
                                    , roomsOrderList[adjArray[q, 0] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);

                            if (GridContains(workingGrid, roomsOrderList[adjArray[q, 0] - 1].roomNumber) && !GridContains(workingGrid, roomsOrderList[adjArray[q, 1] - 1].roomNumber))
                                roomsOrderList[adjArray[q, 1] - 1] = new IntPair(roomsOrderList[adjArray[q, 1] - 1].roomNumber
                                    , roomsOrderList[adjArray[q, 1] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);
                        }
                        roomsOrderList = roomsOrderList.OrderBy(key => -key.AdjNum).ToList();

                        // Determine which room to place next
                        int roomToBePlacedNum;

                        // Place entrance room first if not placed yet
                        if (RoomInstance.entranceIds.Count > 0 && entranceIndexInRoomAreas >= 0 && placedEntranceRoom == false)
                        {
                            roomToBePlacedNum = entranceIndexInRoomAreas;
                            placedEntranceRoom = true;
                        }
                        // Place room with highest adjacency priority
                        else if (roomsOrderList[0].AdjNum > 0)
                            roomToBePlacedNum = roomsOrderList[0].roomNumber;
                        // If no adjacent rooms, place most connected room overall
                        else
                        {
                            roomsOrderList = new List<IntPair>();

                            for (int w = 1; w <= initialRoomsList.Count; w++)
                                if (!GridContains(workingGrid, w))
                                    roomsOrderList.Add(new IntPair(w, 0));
                                else
                                    roomsOrderList.Add(new IntPair(w, -1));

                            for (int q = 0; q < adjArray.GetLength(0); q++)
                            {
                                if (!GridContains(workingGrid, roomsOrderList[adjArray[q, 0] - 1].roomNumber))
                                    roomsOrderList[adjArray[q, 0] - 1] = new IntPair(roomsOrderList[adjArray[q, 0] - 1].roomNumber
                                        , roomsOrderList[adjArray[q, 0] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);

                                if (!GridContains(workingGrid, roomsOrderList[adjArray[q, 1] - 1].roomNumber))
                                    roomsOrderList[adjArray[q, 1] - 1] = new IntPair(roomsOrderList[adjArray[q, 1] - 1].roomNumber
                                        , roomsOrderList[adjArray[q, 1] - 1].AdjNum + 1 + random.NextDouble() * 0.1f);
                            }
                            roomsOrderList = roomsOrderList.OrderBy(key => -key.AdjNum).ToList();
                            roomToBePlacedNum = roomsOrderList[0].roomNumber;
                        }

                        // Try to place the selected room
                        if (!GridContains(workingGrid, roomToBePlacedNum))
                        {
                            if (TryPlaceNewRoomToTheGrid(ref workingGrid, initialRoomsList[roomToBePlacedNum - 1].RoomArea / oneCellSize / oneCellSize
                                , roomToBePlacedNum, adjArray, maxAdjDistance, initialRoomsList[roomToBePlacedNum - 1].isHall, inputs))
                                placedRoomsOrderedList.Add(roomToBePlacedNum - 1);
                            else
                                break;
                        }
                    }

                    // Handle solution storage and replacement
                    if (gridSolutionsCollection.Count > gridSolutionCurrentIndex && currentIteration % gridSolutionCapacity == 0)
                    {
                        if (placedRoomsOrderedList.Count > gridSolutionsCollection[gridSolutionCurrentIndex].placedRoomsOrderedList.Count)
                        {
                            gridSolutionsCollection.Add(new GridSolution(workingGrid.Clone() as int[,], roomCellsList.ConvertAll(roomCells => new RoomCells(roomCells)), placedRoomsOrderedList));
                            gridSolutionsCollection.RemoveAt(gridSolutionCurrentIndex);
                        }
                    }
                    else
                        gridSolutionsCollection.Add(new GridSolution(workingGrid.Clone() as int[,], roomCellsList.ConvertAll(roomCells => new RoomCells(roomCells)), placedRoomsOrderedList));
                }

                // Limit solution collection size
                gridSolutionsCollection = gridSolutionsCollection.OrderBy(solution => -solution.placedRoomsOrderedList.Count).ToList();
                if (gridSolutionsCollection.Count > gridSolutionCapacity + 2)
                    gridSolutionsCollection.RemoveRange(gridSolutionCapacity + 2, Math.Max(0, gridSolutionsCollection.Count - gridSolutionCapacity - 2));
            }

            // Sort solutions by number of placed rooms
            gridSolutionsCollection = gridSolutionsCollection.OrderBy(solution => -solution.placedRoomsOrderedList.Count).ToList() as List<GridSolution>;

            // Get best solution
            int[,] bestGrid = gridSolutionsCollection[0].grid.Clone() as int[,];

            // Apply post-processing based on settings
            if (inputs.RemoveDeadEnds)
                RemoveDeadEnds(ref bestGrid, gridSolutionsCollection[0].roomCellsList);

            if (inputs.RemoveAllCorridors)
                RemoveAllCorridors(ref bestGrid, gridSolutionsCollection[0].roomCellsList);

            // Convert grid to linear array for processing
            List<int> bestGridLinear = new List<int>();
            HashSet<int> placedRoomsNums = new HashSet<int>();

            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                {
                    if (bestGrid[i, j] != 9999)
                        bestGridLinear.Add(bestGrid[i, j]);
                    else
                        bestGridLinear.Add(0);

                    if (!placedRoomsNums.Contains(bestGrid[i, j]) && bestGrid[i, j] != 0 && bestGrid[i, j] != -1 && bestGrid[i, j] != 9999)
                        placedRoomsNums.Add(bestGrid[i, j]);
                }

            // Calculate missing adjacencies and update room states
            List<int> missingRoomAdj = MissingRoomAdjacences(bestGrid, adjArray);
            for (int i = 0; i < initialRoomsList.Count; i++)
                if (!placedRoomsNums.Contains(Convert.ToInt32(i + 1)))
                {
                    if (initialRoomsList[i].hasMissingAdj != true)
                        initialRoomsList[i].hasMissingAdj = true;
                }
                else
                {
                    if (initialRoomsList[i].hasMissingAdj != false)
                        initialRoomsList[i].hasMissingAdj = false;
                }

            List<int> placedRoomsNumsList = placedRoomsNums.ToList();

            // Prepare missing adjacencies for placed rooms only
            List<int> missingRoomAdjSortedList = new List<int>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
                missingRoomAdjSortedList.Add(missingRoomAdj[placedRoomsNumsList[i] - 1]);

            // Generate room names
            List<string> roomNames = new List<string>();
            for (int i = 0; i < placedRoomsNumsList.Count; i++)
            {
                if (!initialRoomsList[placedRoomsNumsList[i] - 1].isHall)
                    roomNames.Add(initialRoomsList[placedRoomsNumsList[i] - 1].RoomName);
                else
                    roomNames.Add("&&HALL&&" + initialRoomsList[placedRoomsNumsList[i] - 1].RoomName);
            }

            // Convert cells to room Breps
            List<Brep> roomBrepsList = new List<Brep>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
            {
                List<Brep> cellsCollection = new List<Brep>();
                for (int q = 0; q < bestGridLinear.Count; q++)
                    if (bestGridLinear[q] == placedRoomsNumsList[i])
                        cellsCollection.Add(gridSurfaceArray[q].ToBrep());

                if (Brep.JoinBreps(cellsCollection, 0.01f) != null)
                    roomBrepsList.Add(Brep.JoinBreps(cellsCollection, 0.01f)[0]);
            }

            // Convert cells to corridor Brep
            Brep corridorsBrep = new Brep();
            for (int i = 0; i < placedRoomsNums.Count; i++)
            {
                List<Brep> cellsCollection = new List<Brep>();
                for (int q = 0; q < bestGridLinear.Count; q++)
                    if (bestGridLinear[q] == -1)
                        cellsCollection.Add(gridSurfaceArray[q].ToBrep());

                if (Brep.JoinBreps(cellsCollection, 0.01f) != null)
                    corridorsBrep = Brep.JoinBreps(cellsCollection, 0.01f)[0];
            }

            // Generate adjacency output string
            string adjacenciesOutputString = "";
            for (int i = 0; i < adjArray.GetLength(0); i++)
                if (placedRoomsNumsList.Contains(adjArray[i, 0]) && placedRoomsNumsList.Contains(adjArray[i, 1]))
                    adjacenciesOutputString += placedRoomsNumsList.IndexOf(adjArray[i, 0]) + "-" + placedRoomsNumsList.IndexOf(adjArray[i, 1]) + "\n";

            // Set outputs
            outputs.RoomBreps = roomBrepsList;
            outputs.Corridors = corridorsBrep;
            outputs.RoomNames = roomNames;
            outputs.Adjacencies = adjacenciesOutputString;
            outputs.MissingAdjacences = missingRoomAdjSortedList;
            outputs.Message = gridSolutionsCollection[0].placedRoomsOrderedList.Count + " of " + initialRoomsList.Count + " placed";

            return outputs;
        }

        // Helper methods moved from MagnetizingRooms_ES

        private void RemoveRoomFromGrid(ref int[,] grid, RoomCells roomCells)
        {
            for (int i = roomCells.x; i < roomCells.x + roomCells.w; i++)
                for (int j = roomCells.y; j < roomCells.y + roomCells.h; j++)
                    grid[i, j] = 0;
        }

        public bool GridContains(int[,] grid, int val)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    if (grid[i, j] == val)
                        return true;
            return false;
        }

        /// <summary>
        /// Returns a list of room numbers, which have missing adjacences.
        /// Later this information is used for indicating rooms which miss connections.
        /// It is indicated with red dots in Rhino environment.
        /// </summary>
        public List<int> MissingRoomAdjacences(int[,] grid, int[,] adjArray)
        {
            List<int> missingAdj = new List<int>();
            int maxRoomNum = 0;
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    if (grid[i, j] < 999)
                        maxRoomNum = Math.Max(maxRoomNum, grid[i, j]);

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
                    if (adjArray[l, 1] - 1 < missingAdj.Count)
                        missingAdj[adjArray[l, 1] - 1]++;

                exists = false;
                for (int i = 0; i < grid.GetLength(0); i++)
                    for (int j = 0; j < grid.GetLength(1); j++)
                        if (grid[i, j] == adjArray[l, 1])
                            exists = true;
                if (!exists)
                    if (adjArray[l, 0] - 1 < missingAdj.Count)
                        missingAdj[adjArray[l, 0] - 1]++;
            }

            return missingAdj;
        }

        /// <summary>
        /// This function serves for removing dead ends from the corridor structure
        /// after the main part of generation is executed already.
        /// The 'dead end' is a corridor, which is not actually required for the
        /// corridor system to be coherent.
        /// </summary>
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

        /// <summary>
        /// This function serves for removing all corridors from the corridor structure
        /// after the main part of generation is executed already.
        /// Corridors are added to the area of corresponding rooms.
        /// </summary>
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

        /// <summary>
        /// This is the function which determines the position of a room and which
        /// tries to place it.
        /// </summary>
        public bool TryPlaceNewRoomToTheGrid(ref int[,] grid, double area, int roomNumber, int[,] adjArray, double maxAdjDistance, bool isHall, SolverInputs inputs)
        {
            int[,] availableCellsGrid = new int[grid.GetLength(0), grid.GetLength(1)];
            int[,] room = new int[50, 50];

            int xDim;
            int yDim;

            List<int> adjacentRooms = new List<int>();

            for (int i = 0; i < adjArray.GetLength(0); i++)
                if (adjArray[i, 0] == roomNumber && GridContains(grid, adjArray[i, 1]))
                    adjacentRooms.Add(adjArray[i, 1]);
                else if (adjArray[i, 1] == roomNumber && GridContains(grid, adjArray[i, 0]))
                    adjacentRooms.Add(adjArray[i, 0]);

            // Let's try to define proportions for the room considering its area and
            // the requested ratio
            double ratio = 1 + random.NextDouble() * (MaxRatio - 1);
            double xDim_d = Math.Sqrt((area / ratio));
            double yDim_d = ratio * Math.Sqrt((area / ratio));

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

            // Choose the corridor generation mode according to input settings
            if (inputs.OneSideCorridorsChecked && !isHall)
            {
                if (random.Next(2) == 0)
                {
                    if (!inputs.CorridorsAsAdditionalSpacesChecked)
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
                                room[i, j] = roomNumber;
                }
                else
                {
                    if (!inputs.CorridorsAsAdditionalSpacesChecked)
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
                                room[i, j] = roomNumber;
                }
            }
            else if (inputs.TwoSidesCorridorsChecked && !isHall)
            {
                if (!inputs.CorridorsAsAdditionalSpacesChecked)
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
                            room[i, j] = roomNumber;
            }
            else if (inputs.AllSidesCorridorsChecked || isHall)
            {
                if (!inputs.CorridorsAsAdditionalSpacesChecked)
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
                            room[i, j] = roomNumber;
            }

            // availableCellsGrid contains 0 or 1 and indicated if the cell is available
            // for placing a room there.

            // Start filling availableCellsGrid: 0 = not available, 1 = available
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    availableCellsGrid[i, j] = 0;

                    // So, if we find a free cell in a grid, let's check that it one
                    // of its neighbours is a corridor cell (so the placed room will
                    // be attached to the corridor structure).
                    if (grid[i, j] == 0)
                        for (int l = -1; l <= 1; l++)
                            for (int k = -1; k <= 1; k++)
                                if ((l == 0 || k == 0) && l != k)
                                    if (i + l >= 0 && i + l < grid.GetLength(0) && j + k >= 0 && j + k < grid.GetLength(1))
                                        if (grid[i + l, j + k] == -1)
                                        {
                                            // If we found that kind of a cell, let's check that it is
                                            // close enough to rooms (only those which are placed already)
                                            // that must be connected to the room that is to be placed.
                                            if (CellsAreNearerThan(i, j, adjacentRooms, grid, maxAdjDistance))
                                            {
                                                // If so, let's check this cell as suitable for placing a new room
                                                availableCellsGrid[i, j] = 1;
                                            }
                                        }
                }

            // This list contains all possible solutions for a room placement.
            List<RoomPlacementSolution> placementSolutions = new List<RoomPlacementSolution>();

            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (availableCellsGrid[i, j] == 1)
                    {
                        if (RoomIsPlaceableHere(grid, room, i, j, RoomPosition.BottomLeft))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.BottomLeft, room
                                , GetRoomPlacementRating(grid, room, i, j, RoomPosition.BottomLeft));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                        if (RoomIsPlaceableHere(grid, room, i, j, RoomPosition.BottomRight))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.BottomRight, room
                                , GetRoomPlacementRating(grid, room, i, j, RoomPosition.BottomRight));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                        if (RoomIsPlaceableHere(grid, room, i, j, RoomPosition.TopLeft))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.TopLeft, room
                                , GetRoomPlacementRating(grid, room, i, j, RoomPosition.TopLeft));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                        if (RoomIsPlaceableHere(grid, room, i, j, RoomPosition.TopRight))
                        {
                            RoomPlacementSolution newSolution = new RoomPlacementSolution(i, j, RoomPosition.TopRight, room
                                , GetRoomPlacementRating(grid, room, i, j, RoomPosition.TopRight));
                            placementSolutions.Add(new RoomPlacementSolution(newSolution));
                        }
                    }
                }

            // Let's order a list by rating
            placementSolutions = placementSolutions.OrderBy(t => -t.rating).ToList();

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

                roomCellsList[roomNumber - 1] = new RoomCells(x, y, w, h);

                // Finally, let's place a room where it should be placed! This line will do all work for us
                PlaceRoomSolution(placementSolutions[0], placementSolutions[0].room, ref grid, isHall);
                return true;
            }
            else
                return false;
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

        /// <summary>
        /// This function checks that the given room with the given RoomPosition
        /// can be successfully placed on the grid. So if all cells that this room
        /// will want to occupy are free (0), it will return true.
        /// </summary>
        private bool RoomIsPlaceableHere(int[,] grid, int[,] room, int x, int y, RoomPosition roomPosition)
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

        /// <summary>
        /// Basically this function takes the RoomPlacementSolution and places it.
        /// It fills the corresponding cells of grid with the given room number.
        /// </summary>
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

        /// <summary>
        /// This function is fairly important.
        /// The rating is calculated according to number of rooms that share a border with
        /// this newly placed room. So we can be sure that there will be as few empty
        /// spaces between rooms as possible.
        /// </summary>
        private int GetRoomPlacementRating(int[,] grid, int[,] room, int x, int y, RoomPosition roomPosition)
        {
            int rating = 0;
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
                if (a != 0 && a != 9999)
                    rating++;

            return rating;
        }

        /// <summary>
        /// Contains the dimensions and the origin of a room on a grid.
        /// </summary>
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

        /// <summary>
        /// Contains the whole solution that was once computed. It is used to
        /// store solutions which were generated during previous iterations.
        /// </summary>
        private class GridSolution
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
        private class RoomPlacementSolution
        {
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

            public RoomPosition roomPosition;
            public int rating = 0;
            public int x;
            public int y;
            public int[,] room;
        }

        private enum RoomPosition { TopRight, BottomRight, BottomLeft, TopLeft, Undefined }

        /// <summary>
        /// IntPair generally is used for saving {(room number in the initialRoomsList) + 1, room priority}
        /// </summary>
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
    }
}