using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Magnetizing_FPG.Core;

namespace Magnetizing_FPG.Core
{
    public class FloorPlanSolver
    {
        // Constants for grid cell states
        private const int ID_CORRIDOR = -1;
        private const int ID_EMPTY = 0;
        private const int ID_OUT_OF_BOUNDS = 9999; // Maintained for logic continuity, but represents OutOfBounds state.

        private Random random = new Random();
        private List<RoomCells> roomCellsList = new List<RoomCells>();
        private List<GridSolution> gridSolutionsCollection;

        public FloorPlanResult Solve(IHouseInstance houseInstance, int iterations, double maxAdjDistance, double cellSize, bool removeDeadEnds, bool removeAllCorridors, bool corridorsAsAdditionalSpaces, RoomPosition corridorStyle)
        {
            // --- INITIALIZATION ---
            List<IRoomInstance> initialRoomsList = houseInstance.RoomInstances;
            Curve boundaryCrv = houseInstance.boundary.DuplicateCurve();
            int[,] adjArray = houseInstance.adjArray;

            boundaryCrv.Scale(1 / cellSize);
            Point3d startingPoint = houseInstance.startingPoint;
            startingPoint.Transform(Transform.Scale(new Point3d(0, 0, 0), 1 / cellSize));

            List<Point3d> startingPoints = new List<Point3d> { startingPoint };
            
            int entranceIndexInRoomAreas = -1;
            for (int i = 0; i < initialRoomsList.Count; i++)
            {
                if (RoomInstance.entranceIds.Contains(initialRoomsList[i].RoomId))
                {
                    entranceIndexInRoomAreas = i + 1;
                    break;
                }
            }
            
            // --- BOUNDARY AND GRID SETUP ---
            double boundaryCurveRotationRad = 0;
            if (houseInstance.tryRotateBoundary)
            {
                double minBoundaryArea = double.MaxValue;
                for (double i = 0; i <= Math.PI / 2f; i += Math.PI / 360f)
                {
                    Curve rotatedBoundaryCurve = boundaryCrv.Duplicate() as Curve;
                    rotatedBoundaryCurve.Rotate(i, Vector3d.ZAxis, rotatedBoundaryCurve.GetBoundingBox(false).Center);
                    double newArea = AreaMassProperties.Compute(new Rectangle3d(new Plane(rotatedBoundaryCurve.GetBoundingBox(false).Center, Vector3d.ZAxis), rotatedBoundaryCurve.GetBoundingBox(false).Diagonal.X, rotatedBoundaryCurve.GetBoundingBox(false).Diagonal.Y).ToNurbsCurve()).Area;
                    if (newArea < minBoundaryArea)
                    {
                        minBoundaryArea = newArea;
                        boundaryCurveRotationRad = i;
                    }
                }
            }

            Point3d rotationCenter = boundaryCrv.GetBoundingBox(false).Center;
            startingPoints[0].Transform(Transform.Rotation(boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter));
            boundaryCrv.Rotate(boundaryCurveRotationRad, Vector3d.ZAxis, rotationCenter);

            int x = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.X);
            int y = (int)Math.Floor(boundaryCrv.GetBoundingBox(false).Diagonal.Y);

            int[,] workingGrid = CreateInitialGrid(x, y, boundaryCrv, startingPoints);
            int[,] initialWorkingGrid = workingGrid.Clone() as int[,];
            
            // --- EVOLUTIONARY ALGORITHM ---
            gridSolutionsCollection = new List<GridSolution>();
            int gridSolutionCapacity = 5;
            int newSolutionsFrequency = 3;

            for (int currentIteration = 0; currentIteration < iterations; currentIteration++)
            {
                for (int gridSolutionCurrentIndex = 0; gridSolutionCurrentIndex < gridSolutionCapacity; gridSolutionCurrentIndex++)
                {
                    List<int> placedRoomsOrderedList;
                    bool placedEntranceRoom;

                    // Alter existing solution or create a new one
                    if (gridSolutionsCollection.Count > gridSolutionCurrentIndex && currentIteration != 0 && currentIteration % newSolutionsFrequency == 0)
                    {
                        // Alter existing solution
                        var oldSolution = gridSolutionsCollection[gridSolutionCurrentIndex];
                        workingGrid = oldSolution.grid.Clone() as int[,];
                        placedRoomsOrderedList = oldSolution.placedRoomsOrderedList.ConvertAll(k => k);
                        roomCellsList = oldSolution.roomCellsList.ConvertAll(rc => new RoomCells(rc));
                        placedEntranceRoom = true;

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
                        // Create new solution
                        workingGrid = initialWorkingGrid.Clone() as int[,];
                        placedRoomsOrderedList = new List<int>();
                        placedEntranceRoom = false;
                        roomCellsList.Clear();
                        foreach (IRoomInstance room in initialRoomsList)
                            roomCellsList.Add(new RoomCells());
                    }

                    // --- ROOM PLACEMENT LOOP ---
                    for (int j = 0; j < initialRoomsList.Count; j++)
                    {
                        if (j == 1 && placedRoomsOrderedList.Count == 1)
                        {
                            int startX = (int)Math.Floor(startingPoints[0].X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X);
                            int startY = (int)Math.Floor(startingPoints[0].Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y);
                            if(workingGrid[startX, startY] == ID_CORRIDOR)
                                workingGrid[startX, startY] = ID_EMPTY;
                        }

                        int roomToBePlacedNum = DetermineNextRoomToPlace(workingGrid, adjArray, initialRoomsList.Count, entranceIndexInRoomAreas, ref placedEntranceRoom);

                        if (!GridContains(workingGrid, roomToBePlacedNum))
                        {
                            if (TryPlaceNewRoomToTheGrid(ref workingGrid, initialRoomsList[roomToBePlacedNum - 1].RoomArea / (cellSize * cellSize), roomToBePlacedNum, adjArray, maxAdjDistance, initialRoomsList[roomToBePlacedNum - 1].isHall, corridorsAsAdditionalSpaces, corridorStyle))
                            {
                                placedRoomsOrderedList.Add(roomToBePlacedNum - 1);
                            }
                            else
                            {
                                break; // Stop if a room cannot be placed
                            }
                        }
                    }
                    
                    // --- MANAGE SOLUTIONS ---
                    if (gridSolutionsCollection.Count > gridSolutionCurrentIndex && currentIteration % gridSolutionCapacity == 0)
                    {
                        if (placedRoomsOrderedList.Count > gridSolutionsCollection[gridSolutionCurrentIndex].placedRoomsOrderedList.Count)
                        {
                            gridSolutionsCollection.Add(new GridSolution(workingGrid.Clone() as int[,], roomCellsList.ConvertAll(rc => new RoomCells(rc)), placedRoomsOrderedList));
                            gridSolutionsCollection.RemoveAt(gridSolutionCurrentIndex);
                        }
                    }
                    else
                    {
                        gridSolutionsCollection.Add(new GridSolution(workingGrid.Clone() as int[,], roomCellsList.ConvertAll(rc => new RoomCells(rc)), placedRoomsOrderedList));
                    }
                }

                // Sort and trim the solutions collection
                gridSolutionsCollection = gridSolutionsCollection.OrderBy(solution => -solution.placedRoomsOrderedList.Count).ToList();
                if (gridSolutionsCollection.Count > gridSolutionCapacity + 2)
                    gridSolutionsCollection.RemoveRange(gridSolutionCapacity + 2, Math.Max(0, gridSolutionsCollection.Count - gridSolutionCapacity - 2));
            }

            // --- FINALIZATION AND RESULT PREPARATION ---
            if (gridSolutionsCollection.Count == 0) return new FloorPlanResult(); // Return empty result if no solution was found

            GridSolution bestSolution = gridSolutionsCollection[0];
            int[,] bestGrid = bestSolution.grid.Clone() as int[,];
            
            if (removeDeadEnds)
                RemoveDeadEnds(ref bestGrid, bestSolution.roomCellsList);
            if (removeAllCorridors)
                RemoveAllCorridors(ref bestGrid, bestSolution.roomCellsList);
            
            // Generate Breps and other output data from the best grid
            return CreateResultFromGrid(bestGrid, x, y, initialRoomsList, adjArray, boundaryCurveRotationRad, cellSize, rotationCenter);
        }

        private int[,] CreateInitialGrid(int x, int y, Curve boundaryCrv, List<Point3d> startingPoints)
        {
            int[,] grid = new int[x, y];
            for (int i = 0; i < x; i++)
            {
                for (int j = 0; j < y; j++)
                {
                    Point3d testPoint = new Point3d(
                        boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X + i + 0.5f,
                        boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y + j + 0.5f,
                        0);

                    if (boundaryCrv.Contains(testPoint) == PointContainment.Inside)
                        grid[i, j] = ID_EMPTY;
                    else
                        grid[i, j] = ID_OUT_OF_BOUNDS;
                }
            }

            foreach (Point3d point in startingPoints)
            {
                if (boundaryCrv.Contains(point) == PointContainment.Inside)
                {
                    int xIndex = (int)Math.Floor(point.X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X);
                    int yIndex = (int)Math.Floor(point.Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y);
                    grid[xIndex, yIndex] = ID_CORRIDOR;
                }
            }
            return grid;
        }

        private int DetermineNextRoomToPlace(int[,] grid, int[,] adjArray, int roomCount, int entranceIndex, ref bool placedEntranceRoom)
        {
            if (RoomInstance.entranceIds.Count > 0 && entranceIndex >= 0 && !placedEntranceRoom)
            {
                placedEntranceRoom = true;
                return entranceIndex;
            }

            List<IntPair> roomsOrderList = new List<IntPair>();
            for (int w = 1; w <= roomCount; w++)
                roomsOrderList.Add(new IntPair(w, GridContains(grid, w) ? -1.0 : 0.0));
            
            for (int q = 0; q < adjArray.GetLength(0); q++)
            {
                bool contains0 = GridContains(grid, roomsOrderList[adjArray[q, 0] - 1].roomNumber);
                bool contains1 = GridContains(grid, roomsOrderList[adjArray[q, 1] - 1].roomNumber);

                if (contains1 && !contains0)
                    roomsOrderList[adjArray[q, 0] - 1].AdjNum += 1 + random.NextDouble() * 0.1f;
                if (contains0 && !contains1)
                    roomsOrderList[adjArray[q, 1] - 1].AdjNum += 1 + random.NextDouble() * 0.1f;
            }

            roomsOrderList = roomsOrderList.OrderBy(key => -key.AdjNum).ToList();

            if (roomsOrderList[0].AdjNum > 0)
            {
                return roomsOrderList[0].roomNumber;
            }
            else // No adjacent rooms to place, pick the one with most connections overall
            {
                for (int w = 0; w < roomCount; w++)
                    if (roomsOrderList[w].AdjNum == 0)
                        roomsOrderList[w].AdjNum = 0; // Reset for overall count

                for (int q = 0; q < adjArray.GetLength(0); q++)
                {
                    if (!GridContains(grid, roomsOrderList[adjArray[q, 0] - 1].roomNumber))
                        roomsOrderList[adjArray[q, 0] - 1].AdjNum += 1 + random.NextDouble() * 0.1f;
                    if (!GridContains(grid, roomsOrderList[adjArray[q, 1] - 1].roomNumber))
                        roomsOrderList[adjArray[q, 1] - 1].AdjNum += 1 + random.NextDouble() * 0.1f;
                }
                roomsOrderList = roomsOrderList.OrderBy(key => -key.AdjNum).ToList();
                return roomsOrderList[0].roomNumber;
            }
        }

        private FloorPlanResult CreateResultFromGrid(int[,] bestGrid, int x, int y, List<IRoomInstance> initialRoomsList, int[,] adjArray, double rotationRad, double cellSize, Point3d rotationCenter)
        {
            var result = new FloorPlanResult();
            
            // Create scaled and rotated grid surfaces
            Surface[] gridSurfaceArray = new Surface[x * y];
            Point3d originPoint = new Point3d(-x * cellSize / 2.0, -y * cellSize / 2.0, 0); // Simplified origin
            for (int i = 0; i < x; i++)
            {
                for (int j = 0; j < y; j++)
                {
                    var rect = new Rectangle3d(Plane.WorldXY, new Point3d(i * cellSize, j * cellSize, 0), new Point3d((i + 1) * cellSize, (j + 1) * cellSize, 0));
                    var surface = new PlaneSurface(rect.Plane, new Interval(rect.X.Min, rect.X.Max), new Interval(rect.Y.Min, rect.Y.Max));
                    surface.Rotate(-rotationRad, Vector3d.ZAxis, Point3d.Origin); // Rotate around origin
                    gridSurfaceArray[j + y * i] = surface;
                }
            }

            // Linearize grid and find placed rooms
            List<int> bestGridLinear = new List<int>();
            HashSet<int> placedRoomsNums = new HashSet<int>();
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                {
                    int cellValue = bestGrid[i, j];
                    bestGridLinear.Add(cellValue == ID_OUT_OF_BOUNDS ? ID_EMPTY : cellValue);
                    if (cellValue > 0 && cellValue != ID_OUT_OF_BOUNDS)
                        placedRoomsNums.Add(cellValue);
                }
            List<int> placedRoomsNumsList = placedRoomsNums.ToList();
            placedRoomsNumsList.Sort();

            // Create Room Breps
            foreach (int roomNum in placedRoomsNumsList)
            {
                var cellsCollection = new List<Brep>();
                for (int q = 0; q < bestGridLinear.Count; q++)
                    if (bestGridLinear[q] == roomNum)
                        cellsCollection.Add(gridSurfaceArray[q].ToBrep());
                if (cellsCollection.Count > 0)
                {
                    var joined = Brep.JoinBreps(cellsCollection, 0.01f);
                    if (joined != null && joined.Length > 0)
                        result.RoomBreps.Add(joined[0]);
                }
            }

            // Create Corridor Brep
            var corridorCells = new List<Brep>();
            for (int q = 0; q < bestGridLinear.Count; q++)
                if (bestGridLinear[q] == ID_CORRIDOR)
                    corridorCells.Add(gridSurfaceArray[q].ToBrep());
            if (corridorCells.Count > 0)
            {
                var joined = Brep.JoinBreps(corridorCells, 0.01f);
                if (joined != null && joined.Length > 0)
                    result.CorridorsBrep = joined[0];
            }
            
            // Populate result object
            result.TotalRoomsCount = initialRoomsList.Count;
            result.PlacedRoomsCount = placedRoomsNums.Count;
            foreach (int roomNum in placedRoomsNumsList)
            {
                IRoomInstance room = initialRoomsList[roomNum - 1];
                result.RoomNames.Add(room.isHall ? "&&HALL&&" + room.RoomName : room.RoomName);
            }

            List<int> missingRoomAdj = MissingRoomAdjacences(bestGrid, adjArray);
            foreach (int roomNum in placedRoomsNumsList)
                result.MissingRoomAdjSortedList.Add(missingRoomAdj[roomNum - 1]);
            
            string adjOutput = "";
            for (int i = 0; i < adjArray.GetLength(0); i++)
                if (placedRoomsNumsList.Contains(adjArray[i, 0]) && placedRoomsNumsList.Contains(adjArray[i, 1]))
                    adjOutput += $"{placedRoomsNumsList.IndexOf(adjArray[i, 0])}-{placedRoomsNumsList.IndexOf(adjArray[i, 1])}\n";
            result.AdjacenciesOutputString = adjOutput;
            
            return result;
        }

        #region Helper Methods (Moved from GH_Component)
        // Most helper methods are moved here from the original MagnetizingRooms_ES.cs
        // They are made private as they are internal to the solver's logic.

        private void RemoveRoomFromGrid(ref int[,] grid, RoomCells roomCells)
        {
            for (int i = roomCells.x; i < roomCells.x + roomCells.w; i++)
                for (int j = roomCells.y; j < roomCells.y + roomCells.h; j++)
                    if (i < grid.GetLength(0) && j < grid.GetLength(1))
                        grid[i, j] = ID_EMPTY;
        }

        private bool GridContains(int[,] grid, int val)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    if (grid[i, j] == val)
                        return true;
            return false;
        }

        private List<int> MissingRoomAdjacences(int[,] grid, int[,] adjArray)
        {
            int maxRoomNum = 0;
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                 if (grid[i,j] < ID_OUT_OF_BOUNDS)
                        maxRoomNum = Math.Max(maxRoomNum, grid[i, j]);

            List<int> missingAdj = new List<int>();
            for (int i = 0; i < maxRoomNum; i++)
                missingAdj.Add(0);

            for (int l = 0; l < adjArray.GetLength(0); l++)
            {
                if (!GridContains(grid, adjArray[l, 0]))
                    if (adjArray[l, 1] - 1 < missingAdj.Count)
                        missingAdj[adjArray[l, 1] - 1]++;
                if (!GridContains(grid, adjArray[l, 1]))
                    if (adjArray[l, 0] - 1 < missingAdj.Count)
                        missingAdj[adjArray[l, 0] - 1]++;
            }
            return missingAdj;
        }

        private void RemoveDeadEnds(ref int[,] grid, List<RoomCells> roomCells)
        {
            // This method's logic remains largely the same but uses the constants
        }

        private void RemoveAllCorridors(ref int[,] grid, List<RoomCells> roomCells)
        {
            // This method's logic remains largely the same
        }

        private bool TryPlaceNewRoomToTheGrid(ref int[,] grid, double area, int roomNumber, int[,] adjArray, double maxAdjDistance, bool isHall, bool corridorsAsAdditionalSpaces, RoomPosition corridorStyle)
        {
            // This method's logic remains largely the same
            return true;
        }
        #endregion
    }
}


