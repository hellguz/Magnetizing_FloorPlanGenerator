using System;
using System.Collections.Generic;
using System.Linq;

namespace Magnetizing_FPG.CoreAlgorithm
{
    /// <summary>
    /// Core magnetizing floor plan algorithm without Grasshopper dependencies
    /// Extracted from MagnetizingRooms_ES.cs
    /// </summary>
    public class CoreMagnetizingAlgorithm
    {
        // Algorithm parameters
        public int RandomSeed { get; set; } = Environment.TickCount;
        private Random random;

        // Constants from original algorithm
        private const double MaxRatio = 1.9;
        private const double BoundaryOffset = 2.3;

        // Algorithm state
        private List<RoomCells> roomCellsList = new List<RoomCells>();
        private List<GridSolution> gridSolutionsCollection;

        public CoreMagnetizingAlgorithm()
        {
            random = new Random(RandomSeed);
        }

        public CoreMagnetizingAlgorithm(int randomSeed)
        {
            RandomSeed = randomSeed;
            random = new Random(RandomSeed);
        }

        /// <summary>
        /// Main algorithm execution method
        /// </summary>
        public AlgorithmResult GenerateFloorPlan(AlgorithmInput input)
        {
            var result = new AlgorithmResult();
            result.RandomSeed = RandomSeed;

            try
            {
                // Reinitialize random with current seed for deterministic behavior
                random = new Random(RandomSeed);

                // Convert input to algorithm format
                var boundary = input.House.Boundary;
                var rooms = input.House.Rooms;
                var adjArray = input.House.AdjacencyArray ?? new int[0, 2];

                // Scale boundary if needed
                var scaledBoundary = ScaleBoundary(boundary, input.CellSize);

                // Find entrance room
                int entranceIndex = FindEntranceRoom(rooms);

                // Initialize grid
                var gridSize = CalculateGridSize(scaledBoundary);
                int[,] workingGrid = InitializeGrid(scaledBoundary, gridSize, input.House.StartingPoint, input.CellSize);
                int[,] initialWorkingGrid = (int[,])workingGrid.Clone();

                // Initialize room cells list
                roomCellsList.Clear();
                foreach (var room in rooms)
                    roomCellsList.Add(new RoomCells());

                // Run main algorithm
                gridSolutionsCollection = new List<GridSolution>();
                RunMainAlgorithm(workingGrid, initialWorkingGrid, rooms, adjArray,
                    input.Iterations, input.MaxAdjDistance, input.CellSize, entranceIndex);

                // Get best solution
                if (gridSolutionsCollection.Count > 0)
                {
                    gridSolutionsCollection = gridSolutionsCollection.OrderBy(solution => -solution.placedRoomsOrderedList.Count).ToList();
                    var bestGrid = gridSolutionsCollection[0].grid;

                    // Generate result
                    result.Success = true;
                    result.Grid = bestGrid;
                    result.PlacedRoomsCount = gridSolutionsCollection[0].placedRoomsOrderedList.Count;
                    result.TotalRoomsCount = rooms.Count;
                    result.GridWidth = gridSize.width;
                    result.GridHeight = gridSize.height;
                    result.Message = $"{result.PlacedRoomsCount} of {result.TotalRoomsCount} rooms placed";

                    // Generate room rectangles and missing adjacencies
                    GenerateOutputGeometry(result, bestGrid, gridSize, scaledBoundary, input.CellSize);
                    result.MissingAdjacencies = CalculateMissingAdjacencies(bestGrid, adjArray);
                }
                else
                {
                    result.Success = false;
                    result.Message = "No valid solutions found";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Algorithm failed: {ex.Message}";
                result.ErrorDetails = ex.ToString();
            }

            return result;
        }

        private SimpleBoundary ScaleBoundary(SimpleBoundary boundary, double cellSize)
        {
            var scaledPoints = new List<SimplePoint>();
            foreach (var point in boundary.Points)
            {
                scaledPoints.Add(new SimplePoint(point.X / cellSize, point.Y / cellSize, point.Z / cellSize));
            }
            return new SimpleBoundary(scaledPoints);
        }

        private int FindEntranceRoom(List<SimpleRoom> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].IsEntrance)
                    return i + 1; // 1-based indexing like original
            }
            return -1;
        }

        private (int width, int height) CalculateGridSize(SimpleBoundary boundary)
        {
            var bounds = boundary.GetBoundingBox();
            int width = (int)Math.Floor(bounds.Width);
            int height = (int)Math.Floor(bounds.Height);
            return (width, height);
        }

        private int[,] InitializeGrid(SimpleBoundary boundary, (int width, int height) gridSize, SimplePoint startingPoint, double cellSize)
        {
            var bounds = boundary.GetBoundingBox();
            int[,] grid = new int[gridSize.width, gridSize.height];

            // Initialize grid cells
            for (int i = 0; i < gridSize.width; i++)
            {
                for (int j = 0; j < gridSize.height; j++)
                {
                    var cellCenter = new SimplePoint(
                        bounds.MinX + i + 0.5,
                        bounds.MinY + j + 0.5,
                        0
                    );

                    if (boundary.Contains(cellCenter))
                        grid[i, j] = 0; // Free space
                    else
                        grid[i, j] = 9999; // Outside boundary
                }
            }

            // Set starting point
            var scaledStartingPoint = new SimplePoint(
                startingPoint.X / cellSize,
                startingPoint.Y / cellSize,
                startingPoint.Z / cellSize
            );

            if (boundary.Contains(scaledStartingPoint))
            {
                int xIndex = (int)Math.Floor(scaledStartingPoint.X - bounds.MinX);
                int yIndex = (int)Math.Floor(scaledStartingPoint.Y - bounds.MinY);
                if (xIndex >= 0 && xIndex < gridSize.width && yIndex >= 0 && yIndex < gridSize.height)
                {
                    grid[xIndex, yIndex] = -1; // Starting corridor
                }
            }

            return grid;
        }

        private void RunMainAlgorithm(int[,] workingGrid, int[,] initialWorkingGrid, List<SimpleRoom> rooms,
            int[,] adjArray, int iterations, double maxAdjDistance, double cellSize, int entranceIndex)
        {
            const int gridSolutionCapacity = 5;
            const int newSolutionsFrequency = 3;

            for (int currentIteration = 0; currentIteration < iterations; currentIteration++)
            {
                for (int gridSolutionCurrentIndex = 0; gridSolutionCurrentIndex < gridSolutionCapacity; gridSolutionCurrentIndex++)
                {
                    List<int> placedRoomsOrderedList;
                    bool placedEntranceRoom;

                    // Initialize iteration state
                    if (gridSolutionsCollection.Count > gridSolutionCurrentIndex &&
                        currentIteration != 0 && currentIteration % newSolutionsFrequency == 0)
                    {
                        // Restore from previous solution and modify
                        RestoreAndModifyPreviousSolution(workingGrid, gridSolutionCurrentIndex, out placedRoomsOrderedList);
                        placedEntranceRoom = true;
                    }
                    else
                    {
                        // Start new solution
                        workingGrid = (int[,])initialWorkingGrid.Clone();
                        placedRoomsOrderedList = new List<int>();
                        placedEntranceRoom = false;

                        roomCellsList.Clear();
                        foreach (var room in rooms)
                            roomCellsList.Add(new RoomCells());
                    }

                    // Try to place each room
                    PlaceRooms(workingGrid, rooms, adjArray, maxAdjDistance, cellSize,
                        entranceIndex, ref placedRoomsOrderedList, ref placedEntranceRoom);

                    // Store solution
                    StoreSolution(workingGrid, placedRoomsOrderedList, gridSolutionCurrentIndex,
                        currentIteration, gridSolutionCapacity);
                }

                // Clean up solutions
                CleanupSolutions(gridSolutionCapacity);
            }
        }

        private void RestoreAndModifyPreviousSolution(int[,] workingGrid, int gridSolutionCurrentIndex,
            out List<int> placedRoomsOrderedList)
        {
            var solution = gridSolutionsCollection[gridSolutionCurrentIndex];

            // Restore grid
            for (int i = 0; i < workingGrid.GetLength(0); i++)
                for (int j = 0; j < workingGrid.GetLength(1); j++)
                    workingGrid[i, j] = solution.grid[i, j];

            // Restore placed rooms list
            placedRoomsOrderedList = new List<int>(solution.placedRoomsOrderedList);

            // Restore room cells
            roomCellsList.Clear();
            foreach (var roomCells in solution.roomCellsList)
                roomCellsList.Add(new RoomCells(roomCells));

            // Remove last 1-5 rooms to try different placement
            int roomRemovalCount = 1 + random.Next(5);
            for (int j = 0; j < roomRemovalCount; j++)
            {
                if (placedRoomsOrderedList.Count > 1)
                {
                    int roomToRemove = placedRoomsOrderedList[placedRoomsOrderedList.Count - 1];
                    RemoveRoomFromGrid(ref workingGrid, roomCellsList[roomToRemove]);
                    placedRoomsOrderedList.RemoveAt(placedRoomsOrderedList.Count - 1);
                }
            }
        }

        private void PlaceRooms(int[,] workingGrid, List<SimpleRoom> rooms, int[,] adjArray,
            double maxAdjDistance, double cellSize, int entranceIndex,
            ref List<int> placedRoomsOrderedList, ref bool placedEntranceRoom)
        {
            var boundary = GetBoundaryFromGrid(workingGrid); // We'll implement this if needed

            for (int j = 0; j < rooms.Count; j++)
            {
                // Remove starting point after first room is placed
                if (j == 1 && placedRoomsOrderedList.Count == 1)
                {
                    ClearStartingPoint(workingGrid, boundary);
                }

                // Determine next room to place
                int roomToBePlacedNum = DetermineNextRoom(workingGrid, rooms.Count, adjArray,
                    entranceIndex, placedEntranceRoom);

                if (roomToBePlacedNum > 0)
                {
                    // Try to place the room
                    if (TryPlaceNewRoomToTheGrid(ref workingGrid, rooms[roomToBePlacedNum - 1].RoomArea / cellSize / cellSize,
                        roomToBePlacedNum, adjArray, maxAdjDistance, rooms[roomToBePlacedNum - 1].IsHall))
                    {
                        placedRoomsOrderedList.Add(roomToBePlacedNum - 1);
                        if (roomToBePlacedNum == entranceIndex)
                            placedEntranceRoom = true;
                    }
                    else
                    {
                        break; // Can't place room, stop this iteration
                    }
                }
            }
        }

        private SimpleBoundary GetBoundaryFromGrid(int[,] grid)
        {
            // For now, return a simple rectangular boundary based on grid size
            return SimpleBoundary.CreateRectangle(0, 0, grid.GetLength(0), grid.GetLength(1));
        }

        private void ClearStartingPoint(int[,] grid, SimpleBoundary boundary)
        {
            // Find and clear starting point (-1 cells that are not needed)
            // This is a simplified version - in full implementation you'd use the actual starting point
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (grid[i, j] == -1)
                    {
                        grid[i, j] = 0; // Clear starting point
                        return; // Only clear the first one found
                    }
                }
            }
        }

        private int DetermineNextRoom(int[,] grid, int roomCount, int[,] adjArray,
            int entranceIndex, bool placedEntranceRoom)
        {
            // If entrance room not placed, place it first
            if (entranceIndex > 0 && !placedEntranceRoom)
                return entranceIndex;

            // Create room priority list
            var roomsOrderList = new List<IntPair>();
            for (int w = 1; w <= roomCount; w++)
            {
                if (!GridContains(grid, w))
                    roomsOrderList.Add(new IntPair(w, 0));
                else
                    roomsOrderList.Add(new IntPair(w, -1));
            }

            // Calculate priorities based on adjacencies
            if (adjArray != null)
            {
                for (int q = 0; q < adjArray.GetLength(0); q++)
                {
                    if (GridContains(grid, roomsOrderList[adjArray[q, 1] - 1].roomNumber) &&
                        !GridContains(grid, roomsOrderList[adjArray[q, 0] - 1].roomNumber))
                    {
                        var currentRoom = roomsOrderList[adjArray[q, 0] - 1];
                        roomsOrderList[adjArray[q, 0] - 1] = new IntPair(currentRoom.roomNumber,
                            currentRoom.AdjNum + 1 + random.NextDouble() * 0.1);
                    }

                    if (GridContains(grid, roomsOrderList[adjArray[q, 0] - 1].roomNumber) &&
                        !GridContains(grid, roomsOrderList[adjArray[q, 1] - 1].roomNumber))
                    {
                        var currentRoom = roomsOrderList[adjArray[q, 1] - 1];
                        roomsOrderList[adjArray[q, 1] - 1] = new IntPair(currentRoom.roomNumber,
                            currentRoom.AdjNum + 1 + random.NextDouble() * 0.1);
                    }
                }
            }

            roomsOrderList = roomsOrderList.OrderBy(key => -key.AdjNum).ToList();

            // Return highest priority unplaced room
            if (roomsOrderList[0].AdjNum > 0)
                return roomsOrderList[0].roomNumber;

            // If no adjacent rooms, find most connected room overall
            roomsOrderList = new List<IntPair>();
            for (int w = 1; w <= roomCount; w++)
            {
                if (!GridContains(grid, w))
                    roomsOrderList.Add(new IntPair(w, 0));
                else
                    roomsOrderList.Add(new IntPair(w, -1));
            }

            if (adjArray != null)
            {
                for (int q = 0; q < adjArray.GetLength(0); q++)
                {
                    if (!GridContains(grid, roomsOrderList[adjArray[q, 0] - 1].roomNumber))
                    {
                        var currentRoom = roomsOrderList[adjArray[q, 0] - 1];
                        roomsOrderList[adjArray[q, 0] - 1] = new IntPair(currentRoom.roomNumber,
                            currentRoom.AdjNum + 1 + random.NextDouble() * 0.1);
                    }

                    if (!GridContains(grid, roomsOrderList[adjArray[q, 1] - 1].roomNumber))
                    {
                        var currentRoom = roomsOrderList[adjArray[q, 1] - 1];
                        roomsOrderList[adjArray[q, 1] - 1] = new IntPair(currentRoom.roomNumber,
                            currentRoom.AdjNum + 1 + random.NextDouble() * 0.1);
                    }
                }
            }

            roomsOrderList = roomsOrderList.OrderBy(key => -key.AdjNum).ToList();
            return roomsOrderList[0].roomNumber;
        }

        /// <summary>
        /// Core room placement algorithm - extracted from original TryPlaceNewRoomToTheGrid
        /// </summary>
        private bool TryPlaceNewRoomToTheGrid(ref int[,] grid, double area, int roomNumber,
            int[,] adjArray, double maxAdjDistance, bool isHall = false)
        {
            // Calculate room dimensions
            double ratio = 1 + random.NextDouble() * (MaxRatio - 1);
            double xDim_d = Math.Sqrt(area / ratio);
            double yDim_d = ratio * Math.Sqrt(area / ratio);

            int xDim = Math.Max(1, (int)Math.Round(xDim_d));
            int yDim = Math.Max(1, (int)Math.Round(yDim_d));

            // Randomly swap dimensions
            if (random.Next(2) == 0)
            {
                (xDim, yDim) = (yDim, xDim);
            }

            // Create room layout with corridors
            int[,] room = CreateRoomLayout(xDim, yDim, roomNumber, isHall);

            // Find adjacent rooms
            var adjacentRooms = FindAdjacentRooms(roomNumber, adjArray, grid);

            // Find available positions
            var placementSolutions = FindPlacementSolutions(grid, room, adjacentRooms, maxAdjDistance);

            if (placementSolutions.Count > 0)
            {
                // Sort by rating and place best solution
                placementSolutions = placementSolutions.OrderBy(t => -t.rating).ToList();
                var bestSolution = placementSolutions[0];

                // Calculate final position
                int finalX = bestSolution.x;
                int finalY = bestSolution.y;
                if (bestSolution.roomPosition == RoomPosition.BottomLeft || bestSolution.roomPosition == RoomPosition.TopLeft)
                    finalX -= room.GetLength(0) - 1;
                if (bestSolution.roomPosition == RoomPosition.BottomLeft || bestSolution.roomPosition == RoomPosition.BottomRight)
                    finalY -= room.GetLength(1) - 1;

                // Store room position
                roomCellsList[roomNumber - 1] = new RoomCells(finalX, finalY, room.GetLength(0), room.GetLength(1));

                // Place room on grid
                PlaceRoomSolution(bestSolution, room, ref grid, isHall);
                return true;
            }

            return false;
        }

        private int[,] CreateRoomLayout(int xDim, int yDim, int roomNumber, bool isHall)
        {
            // Simplified corridor generation - using all-sides corridors for halls
            if (isHall)
            {
                var room = new int[xDim + 2, yDim + 2];
                for (int i = 0; i <= xDim + 1; i++)
                {
                    for (int j = 0; j <= yDim + 1; j++)
                    {
                        if (i == 0 || j == 0 || i == xDim + 1 || j == yDim + 1)
                            room[i, j] = -1; // Corridor
                        else
                            room[i, j] = roomNumber; // Room
                    }
                }
                return room;
            }
            else
            {
                // Regular room without corridors for simplicity
                var room = new int[xDim, yDim];
                for (int i = 0; i < xDim; i++)
                    for (int j = 0; j < yDim; j++)
                        room[i, j] = roomNumber;
                return room;
            }
        }

        private List<int> FindAdjacentRooms(int roomNumber, int[,] adjArray, int[,] grid)
        {
            var adjacentRooms = new List<int>();

            if (adjArray != null)
            {
                for (int i = 0; i < adjArray.GetLength(0); i++)
                {
                    if (adjArray[i, 0] == roomNumber && GridContains(grid, adjArray[i, 1]))
                        adjacentRooms.Add(adjArray[i, 1]);
                    else if (adjArray[i, 1] == roomNumber && GridContains(grid, adjArray[i, 0]))
                        adjacentRooms.Add(adjArray[i, 0]);
                }
            }

            return adjacentRooms;
        }

        private List<RoomPlacementSolution> FindPlacementSolutions(int[,] grid, int[,] room,
            List<int> adjacentRooms, double maxAdjDistance)
        {
            var solutions = new List<RoomPlacementSolution>();
            int[,] availableCellsGrid = CalculateAvailableCells(grid, adjacentRooms, maxAdjDistance);

            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (availableCellsGrid[i, j] == 1)
                    {
                        // Try all four orientations
                        TryAddPlacementSolution(solutions, grid, room, i, j, RoomPosition.BottomLeft);
                        TryAddPlacementSolution(solutions, grid, room, i, j, RoomPosition.BottomRight);
                        TryAddPlacementSolution(solutions, grid, room, i, j, RoomPosition.TopLeft);
                        TryAddPlacementSolution(solutions, grid, room, i, j, RoomPosition.TopRight);
                    }
                }
            }

            return solutions;
        }

        private void TryAddPlacementSolution(List<RoomPlacementSolution> solutions, int[,] grid,
            int[,] room, int x, int y, RoomPosition position)
        {
            if (RoomIsPlaceableHere(grid, room, x, y, position))
            {
                int rating = GetRoomPlacementRating(grid, room, x, y, position);
                solutions.Add(new RoomPlacementSolution(x, y, position, room, rating));
            }
        }

        private int[,] CalculateAvailableCells(int[,] grid, List<int> adjacentRooms, double maxAdjDistance)
        {
            int[,] availableCells = new int[grid.GetLength(0), grid.GetLength(1)];

            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    availableCells[i, j] = 0;

                    if (grid[i, j] == 0) // Free cell
                    {
                        // Check if adjacent to corridor
                        bool nearCorridor = IsNearCorridor(grid, i, j);

                        if (nearCorridor)
                        {
                            // Check distance to required adjacent rooms
                            if (CellsAreNearerThan(i, j, adjacentRooms, grid, maxAdjDistance))
                            {
                                availableCells[i, j] = 1;
                            }
                        }
                    }
                }
            }

            return availableCells;
        }

        private bool IsNearCorridor(int[,] grid, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if ((dx == 0 || dy == 0) && dx != dy) // Only orthogonal neighbors
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < grid.GetLength(0) && ny >= 0 && ny < grid.GetLength(1))
                        {
                            if (grid[nx, ny] == -1) // Corridor
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        // Continue in next part due to length...

        private bool GridContains(int[,] grid, int val)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    if (grid[i, j] == val)
                        return true;
            return false;
        }

        private void RemoveRoomFromGrid(ref int[,] grid, RoomCells roomCells)
        {
            for (int i = roomCells.x; i < roomCells.x + roomCells.w; i++)
                for (int j = roomCells.y; j < roomCells.y + roomCells.h; j++)
                    if (i >= 0 && i < grid.GetLength(0) && j >= 0 && j < grid.GetLength(1))
                        grid[i, j] = 0;
        }

        private void StoreSolution(int[,] workingGrid, List<int> placedRoomsOrderedList,
            int gridSolutionCurrentIndex, int currentIteration, int gridSolutionCapacity)
        {
            if (gridSolutionsCollection.Count > gridSolutionCurrentIndex && currentIteration % gridSolutionCapacity == 0)
            {
                if (placedRoomsOrderedList.Count > gridSolutionsCollection[gridSolutionCurrentIndex].placedRoomsOrderedList.Count)
                {
                    gridSolutionsCollection.Add(new GridSolution((int[,])workingGrid.Clone(),
                        roomCellsList.ConvertAll(r => new RoomCells(r)), new List<int>(placedRoomsOrderedList)));
                    gridSolutionsCollection.RemoveAt(gridSolutionCurrentIndex);
                }
            }
            else
            {
                gridSolutionsCollection.Add(new GridSolution((int[,])workingGrid.Clone(),
                    roomCellsList.ConvertAll(r => new RoomCells(r)), new List<int>(placedRoomsOrderedList)));
            }
        }

        private void CleanupSolutions(int gridSolutionCapacity)
        {
            gridSolutionsCollection = gridSolutionsCollection.OrderBy(solution => -solution.placedRoomsOrderedList.Count).ToList();
            if (gridSolutionsCollection.Count > gridSolutionCapacity + 2)
                gridSolutionsCollection.RemoveRange(gridSolutionCapacity + 2,
                    Math.Max(0, gridSolutionsCollection.Count - gridSolutionCapacity - 2));
        }

        private void GenerateOutputGeometry(AlgorithmResult result, int[,] grid, (int width, int height) gridSize,
            SimpleBoundary boundary, double cellSize)
        {
            result.RoomRectangles = new List<SimpleRectangle>();
            result.RoomNames = new List<string>();

            // Implementation would convert grid cells back to scaled rectangles
            // This is a simplified version
            var placedRooms = new HashSet<int>();
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    int cellValue = grid[i, j];
                    if (cellValue > 0 && cellValue != 9999 && !placedRooms.Contains(cellValue))
                    {
                        placedRooms.Add(cellValue);
                        // Create rectangle for room (simplified)
                        result.RoomRectangles.Add(new SimpleRectangle(i * cellSize, j * cellSize, cellSize, cellSize));
                        result.RoomNames.Add($"Room_{cellValue}");
                    }
                }
            }
        }

        private List<int> CalculateMissingAdjacencies(int[,] grid, int[,] adjArray)
        {
            var missingAdj = new List<int>();
            if (adjArray == null) return missingAdj;

            int maxRoomNum = 0;
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    if (grid[i, j] < 999)
                        maxRoomNum = Math.Max(maxRoomNum, grid[i, j]);

            for (int i = 0; i < maxRoomNum; i++)
                missingAdj.Add(0);

            for (int l = 0; l < adjArray.GetLength(0); l++)
            {
                bool room1Exists = GridContains(grid, adjArray[l, 0]);
                bool room2Exists = GridContains(grid, adjArray[l, 1]);

                if (!room1Exists && adjArray[l, 1] - 1 < missingAdj.Count)
                    missingAdj[adjArray[l, 1] - 1]++;

                if (!room2Exists && adjArray[l, 0] - 1 < missingAdj.Count)
                    missingAdj[adjArray[l, 0] - 1]++;
            }

            return missingAdj;
        }

        // Placeholder implementations for complex methods that need full algorithm logic
        private bool CellsAreNearerThan(int x, int y, List<int> targetCellsList, int[,] grid, double maxDistance = 2)
        {
            // Simplified distance check - full implementation would use pathfinding
            return true; // For now, allow all placements
        }

        private bool RoomIsPlaceableHere(int[,] grid, int[,] room, int x, int y, RoomPosition roomPosition)
        {
            // Check if room can be placed at position with given orientation
            var bounds = GetRoomBounds(room, x, y, roomPosition);

            if (bounds.minX < 0 || bounds.maxX >= grid.GetLength(0) ||
                bounds.minY < 0 || bounds.maxY >= grid.GetLength(1))
                return false;

            for (int i = bounds.minX; i <= bounds.maxX; i++)
                for (int j = bounds.minY; j <= bounds.maxY; j++)
                    if (grid[i, j] != 0)
                        return false;

            return true;
        }

        private int GetRoomPlacementRating(int[,] grid, int[,] room, int x, int y, RoomPosition roomPosition)
        {
            // Simplified rating - count adjacent occupied cells
            int rating = 0;
            var bounds = GetRoomBounds(room, x, y, roomPosition);

            // Check perimeter for adjacent rooms
            for (int i = bounds.minX - 1; i <= bounds.maxX + 1; i++)
            {
                for (int j = bounds.minY - 1; j <= bounds.maxY + 1; j++)
                {
                    if (i >= 0 && i < grid.GetLength(0) && j >= 0 && j < grid.GetLength(1))
                    {
                        if (grid[i, j] != 0 && grid[i, j] != 9999)
                            rating++;
                    }
                }
            }

            return rating;
        }

        private (int minX, int minY, int maxX, int maxY) GetRoomBounds(int[,] room, int x, int y, RoomPosition position)
        {
            int roomWidth = room.GetLength(0);
            int roomHeight = room.GetLength(1);

            switch (position)
            {
                case RoomPosition.TopRight:
                    return (x, y, x + roomWidth - 1, y + roomHeight - 1);
                case RoomPosition.BottomRight:
                    return (x, y - roomHeight + 1, x + roomWidth - 1, y);
                case RoomPosition.BottomLeft:
                    return (x - roomWidth + 1, y - roomHeight + 1, x, y);
                case RoomPosition.TopLeft:
                    return (x - roomWidth + 1, y, x, y + roomHeight - 1);
                default:
                    return (x, y, x + roomWidth - 1, y + roomHeight - 1);
            }
        }

        private void PlaceRoomSolution(RoomPlacementSolution solution, int[,] room, ref int[,] grid, bool isHall = false)
        {
            var bounds = GetRoomBounds(room, solution.x, solution.y, solution.roomPosition);

            switch (solution.roomPosition)
            {
                case RoomPosition.TopRight:
                    for (int i = 0; i < room.GetLength(0); i++)
                        for (int j = 0; j < room.GetLength(1); j++)
                            grid[solution.x + i, solution.y + j] = room[i, j];
                    break;
                case RoomPosition.BottomRight:
                    for (int i = 0; i < room.GetLength(0); i++)
                        for (int j = 0; j < room.GetLength(1); j++)
                            grid[solution.x + i, solution.y - j] = room[i, room.GetLength(1) - 1 - j];
                    break;
                case RoomPosition.BottomLeft:
                    for (int i = 0; i < room.GetLength(0); i++)
                        for (int j = 0; j < room.GetLength(1); j++)
                            grid[solution.x - i, solution.y - j] = room[room.GetLength(0) - 1 - i, room.GetLength(1) - 1 - j];
                    break;
                case RoomPosition.TopLeft:
                    for (int i = 0; i < room.GetLength(0); i++)
                        for (int j = 0; j < room.GetLength(1); j++)
                            grid[solution.x - i, solution.y + j] = room[room.GetLength(0) - 1 - i, j];
                    break;
            }
        }

        // Supporting classes and enums
        private class RoomCells
        {
            public int x, y, w, h;

            public RoomCells() { }
            public RoomCells(int X, int Y, int W, int H) { x = X; y = Y; w = W; h = H; }
            public RoomCells(RoomCells other) { x = other.x; y = other.y; w = other.w; h = other.h; }
        }

        private class GridSolution
        {
            public int[,] grid;
            public List<RoomCells> roomCellsList = new List<RoomCells>();
            public List<int> placedRoomsOrderedList = new List<int>();

            public GridSolution(int[,] Grid, List<RoomCells> RoomCellsList, List<int> RoomOrder)
            {
                grid = Grid;
                roomCellsList = RoomCellsList;
                placedRoomsOrderedList = RoomOrder;
            }
        }

        private class RoomPlacementSolution
        {
            public int x, y, rating;
            public RoomPosition roomPosition;
            public int[,] room;

            public RoomPlacementSolution(int roomX, int roomY, RoomPosition position, int[,] mRoom, int mScore)
            {
                x = roomX; y = roomY; roomPosition = position; rating = mScore;
                room = (int[,])mRoom.Clone();
            }
        }

        private enum RoomPosition { TopRight, BottomRight, BottomLeft, TopLeft, Undefined }

        private struct IntPair
        {
            public int roomNumber;
            public double AdjNum;

            public IntPair(int a1, double b1) { roomNumber = a1; AdjNum = b1; }
        }
    }

    /// <summary>
    /// Input data for the core algorithm
    /// </summary>
    public class AlgorithmInput
    {
        public SimpleHouse House { get; set; }
        public int Iterations { get; set; } = 100;
        public double MaxAdjDistance { get; set; } = 2.0;
        public double CellSize { get; set; } = 1.0;
    }

    /// <summary>
    /// Result from the core algorithm
    /// </summary>
    public class AlgorithmResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string ErrorDetails { get; set; } = "";
        public int RandomSeed { get; set; }

        // Algorithm results
        public int[,] Grid { get; set; }
        public int PlacedRoomsCount { get; set; }
        public int TotalRoomsCount { get; set; }
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }

        // Output geometry
        public List<SimpleRectangle> RoomRectangles { get; set; } = new List<SimpleRectangle>();
        public List<string> RoomNames { get; set; } = new List<string>();
        public List<int> MissingAdjacencies { get; set; } = new List<int>();
    }
}