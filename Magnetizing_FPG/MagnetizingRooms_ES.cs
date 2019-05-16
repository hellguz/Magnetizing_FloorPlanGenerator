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
    public class MagnetizingRooms_ES : GH_Component
    {

        Random random = new Random();

        List<RoomCells> roomCellsList = new List<RoomCells>();
        List<GridSolution> gridSolutionsCollection;

        // MaxRatio stated for maximum allowed proportions of every room. 
        const double MaxRatio = 1.9f;
        
        double boundaryOffset = 2.3f;

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
        public MagnetizingRooms_ES()
          : base("MagnetizingRooms_ES", "Magnetizing_FPG",
              "MagnetizingRooms_ES",
              "Magnetizing_FPG", "Magnetizing_FPG")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("House Instance", "HI", "One or more House Instances. It already contains the information about " +
                "boundary, areas of rooms, starting point and so on.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iterations", "I", "Iterations counter. Generally value between 300-900 works best.", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("MaxAdjDistance", "MAD", "Max distance between 2 connected rooms. Generally 2-3 works best.", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("CellSize(m)", "CS(m)", "Resolution of grid in meters, 1m is used by default", GH_ParamAccess.item, 1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Corridors", "C", "Corridors as Breps", GH_ParamAccess.item);
            pManager.AddBrepParameter("Room Breps", "Rs", "Rooms as Breps list", GH_ParamAccess.list);
            pManager.AddTextParameter("Room Names", "Ns", "Room Names", GH_ParamAccess.list);
            pManager.AddTextParameter("Adjacencies", "A", "Adjacencies as list of string \"1 - 3, 2 - 4,..\"", GH_ParamAccess.item);
            pManager.AddIntegerParameter("MissingAdjacences", "!A", "Missing Adjacences for every room of the list", GH_ParamAccess.list);
            pManager.AddCurveParameter("Boundary", "B", "Boundary output", GH_ParamAccess.item);
            pManager.AddCurveParameter("Boundary+Offset", "Bo", "Boundary offset output", GH_ParamAccess.item);
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
            List<string> adjStrList = new List<string>();
            double maxAdjDistance = 0;
            int entranceIndexInRoomAreas = -1;
            HouseInstance houseInstance = new HouseInstance();
            double oneCellSize = 1;

            GH_ObjectWrapper houseInstanceWrapper = new GH_ObjectWrapper();
            DA.GetData("House Instance", ref houseInstanceWrapper);
            houseInstance = houseInstanceWrapper.Value as HouseInstance;

            DA.GetData("CellSize(m)", ref oneCellSize);

            // initialRoomsList contains all (RoomInstance) objects with their attributes.
            List<RoomInstance> initialRoomsList = houseInstance.RoomInstances;
            Curve boundaryCrv = houseInstance.boundary;
            int[,] adjArray = houseInstance.adjArray;
            adjStrList = houseInstance.adjStrList;

            DA.SetData("Boundary", boundaryCrv);
            boundaryCrv = boundaryCrv.Offset(Plane.WorldXY, boundaryOffset, 0.001f, CurveOffsetCornerStyle.Sharp)[0];
            DA.SetData("Boundary+Offset", boundaryCrv);

            
            boundaryCrv.Scale(1 / oneCellSize);
            houseInstance.startingPoint.Transform(Transform.Scale(new Point3d(0, 0, 0), 1 / oneCellSize));

            // startingPoints contains a list of Points which were provided as points for placing the first (entrance) room.
            // IMPORTANT: currently the algorithm can't work with a list of points, it uses only the first one from the list.
            List<Point3d> startingPoints = new List<Point3d>();
            startingPoints.Add(houseInstance.startingPoint);

            // Let's go through all the rooms from the input and try to find the entrance room. It must be placed first.
            // RoomInstance.entranceIds is a static field, it contains a list of all entrance rooms which are on grasshopper workingGrid.
            // For example, if we have 3 room structures on the workingGrid (3 houses), then the list will contain 3 rooms: one for every house.
            for (int i = 0; i < initialRoomsList.Count; i++)
                if (RoomInstance.entranceIds.Contains(initialRoomsList[i].RoomId))
                {
                    entranceIndexInRoomAreas = i + 1;
                    break;
                }

            int x = 0;
            int y = 0;

            DA.GetData("Iterations", ref iterations);
            DA.GetData("MaxAdjDistance", ref maxAdjDistance);

            // Let's deal with setting boundary curve. The curve is rotated so it fits best into rectangle. 
            // Then a workingGrid of cells (Breps) should be generated so it covers the whole boundary.
            // As we generated a workingGrid of cells on top of the rotated boundary, they should be rotated back again,
            // together with the boundary itself.
            // Moreover, we should rotate the starting point also.

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

            Point3d originPoint = boundaryCrv.GetBoundingBox(false).Corner(true, true, true);
            Vector3d diagonal = boundaryCrv.GetBoundingBox(false).Diagonal;

            // gridSurfaceArray will contain all cell Breps for our workingGrid. They will be used to form
            // rooms and corridors in the very end of the algorithm.
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

            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    gridSurfaceArray[i + x * j].Scale(oneCellSize);


            // So the cells are rotated back again, they can be used now.

            // workingGrid is an array[int, int], containing the current state of a room structure.
            //
            // If the cell = -1         -> it is a corridor
            //             =  0         -> it is free
            //             =  N (1-...) -> it is a room 
            //             =  9999      -> it is outside initial curve boundary, therefore it cannot be used
            //                             (sorry for that, was too lazy to write smth more complicated)

            // If the initial curve boundary was not rectangular, we should make sure that the algorithm
            // will not place rooms outside it. Let's fill all cells with 9999 (sorry for that, was too lazy to write smth more complicated)
            int[,] workingGrid = new int[x, y];
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    if (boundaryCrv.Contains(new Point3d(boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X + i + 0.5f
                    , boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y + j + 0.5f, 0)) == PointContainment.Inside)
                        workingGrid[i, j] = 0;
                    else
                        workingGrid[i, j] = 9999;


            // If there is no startingPoint provided, let's assume that the center of 
            // the grid is a starting point.
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


            // Actual algorithm starts.

            // Let's save the initial state of workingGrid, so we can restore it in each iteration beginning
            int[,] initialWorkingGrid = workingGrid.Clone() as int[,];

            // placedRoomsOrderedList contains the current order of rooms based on their priority. Rooms which
            // have more adjacences with already placed rooms have higher priority. For example if
            // room_1, room_2, room_3 are already placed and room_4 (adj. to 1, 2), room_5 (adj. to 4, 6),
            // room_6 (adj. to 1, 2, 3) are not placed yet,
            // the placedRoomsOrderedList list will look like {room_6, room_4, room_5}.
            List<int> placedRoomsOrderedList = new List<int>();
            bool placedEntranceRoom;

            // If there is no need to execute the whole sequence, for example when user only 
            // checkes shouldOnlyRecomputeDeadEnds variable and does not change initial input,
            // then let's set iterations to 0 and do not execute the main part.
            // Otherwise, let's initialize gridSolutionsCollection list. It will
            // contain the whole number of best solutions (explained in details further).
            if (shouldOnlyRecomputeDeadEnds && gridSolutionsCollection != null && gridSolutionsCollection.Count > 0)
            {
                shouldOnlyRecomputeDeadEnds = false;
                iterations = 0;
            }
            else
                gridSolutionsCollection = new List<GridSolution>();

            // This is the main part of the algorithm.
            for (int currentIteration = 0; currentIteration < iterations; currentIteration++)
            {
                // gridSolutionCurrentIndex indicates current solution from gridSolution that we are working with.
                // So, each THIRD (explained below) iteration we try to alter a number of best solutions that we get from previous
                // iteration, and if these new solutions are better, we exchange them with initial ones.

                // gridSolutionCapacity is number of solutions that we keep after each iteration.
                // After every iteration the gridSolution list is sorted and then shortened to its capacity.
                int gridSolutionCapacity = 5;
                for (int gridSolutionCurrentIndex = 0; gridSolutionCurrentIndex < gridSolutionCapacity; gridSolutionCurrentIndex++)
                {
                    // If (iteration % newSolutionsFrequency != 0) -> some new solutions are generated, so we don't get stuck with local maximum.
                    int newSolutionsFrequency = 3;

                    // If it is time to alter one of previous solutions...
                    if (gridSolutionsCollection.Count > gridSolutionCurrentIndex && currentIteration != 0 && currentIteration % newSolutionsFrequency == 0)
                    {
                        // ... then let's fill all variables used in algorithm with already precomputed data
                        // from previous iteration.
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

                        // Then let's remove last placed 1-5 rooms from the solution. So we can try to place
                        // them differently and maybe a new and better way of placement would appear.
                        int roomRemovalCount = 1 + random.Next(5);
                        for (int j = 0; j < roomRemovalCount; j++)
                            if (placedRoomsOrderedList.Count > 1)
                            {
                                RemoveRoomFromGrid(ref workingGrid, roomCellsList[placedRoomsOrderedList[placedRoomsOrderedList.Count - 1]]);
                                placedRoomsOrderedList.RemoveAt(placedRoomsOrderedList.Count - 1);
                            }
                    }
                    // If it is time to generate a new solution, let's initialize all
                    // required variables and proceed.
                    else
                    {
                        workingGrid = initialWorkingGrid.Clone() as int[,];
                        placedRoomsOrderedList = new List<int>();
                        placedEntranceRoom = false;

                        roomCellsList.Clear();
                        foreach (RoomInstance room in initialRoomsList)
                            roomCellsList.Add(new RoomCells());
                    }

                    // Let's try to place each room in case it is not placed yet
                    for (int j = 0; j < initialRoomsList.Count; j++)
                    {
                        // In the very beginning of the algorithm's execution we have set a value 
                        // in a starting point of a workingGrid from 0 to -1. When the first room is already
                        // placed, we can remove that first starting point. Otherwise, it can affect the
                        // structure of rooms and cause some empty spaces in between them to appear.
                        if (j == 1 && workingGrid[(int)Math.Floor(startingPoints[0].X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X),
                                (int)Math.Floor(startingPoints[0].Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y)] == -1 && placedRoomsOrderedList.Count == 1)
                        {
                            workingGrid[(int)Math.Floor(startingPoints[0].X - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).X),
                                (int)Math.Floor(startingPoints[0].Y - boundaryCrv.GetBoundingBox(false).Corner(true, true, true).Y)] = 0;
                        }

                        // This list contains all rooms that are to be placed, together with their 
                        // priority.
                        // IntPair contains {(room number in the initialRoomsList) + 1, room priority}
                        List<IntPair> roomsOrderList = new List<IntPair>();

                        for (int w = 1; w <= initialRoomsList.Count; w++)
                            if (!GridContains(workingGrid, w))
                                roomsOrderList.Add(new IntPair(w, 0));
                            else
                                roomsOrderList.Add(new IntPair(w, -1));

                        // Let's fill the priority of every room and then sort the list
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

                        // roomToBePlacedNum contains the number of next room which should be placed on the workingGrid.
                        int roomToBePlacedNum;

                        // So if the entrance room is not place yet, let's place it first!
                        if (RoomInstance.entranceIds.Count > 0 && entranceIndexInRoomAreas >= 0 && placedEntranceRoom == false)
                        {
                            roomToBePlacedNum = entranceIndexInRoomAreas;
                            placedEntranceRoom = true;
                        }
                        // If at least one unplaced room is adjacent to at least one placed room, then place it
                        else if (roomsOrderList[0].AdjNum > 0)
                            roomToBePlacedNum = roomsOrderList[0].roomNumber;
                        // If no, then place the most adjacent room overall
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

                        // Check again that workingGrid does not contain roomToBePlacedNum. 
                        // It should not happen, but whatever ¯\_(ツ)_/¯
                        if (!GridContains(workingGrid, roomToBePlacedNum))
                        {
                            // This is the function which determines the position of a room and which 
                            // tries to place it. However, if the room can't be placed anywhere,
                            // the iteration stops. Then results are evaluated and the next iteration starts.
                            if (TryPlaceNewRoomToTheGrid(ref workingGrid, initialRoomsList[roomToBePlacedNum - 1].RoomArea / oneCellSize / oneCellSize 
                                , roomToBePlacedNum, adjArray, maxAdjDistance, initialRoomsList[roomToBePlacedNum - 1].isHall))
                                placedRoomsOrderedList.Add(roomToBePlacedNum - 1);
                            else
                                break;
                        }
                    }

                    // If the new solution is one of altered previous solutions and it is better
                    // than its predecessor, then let's exchange them. 
                    // Other decision could be just to add a new solution to the list and do not
                    // remove the predecessor, however, it is not made like this intentionally.
                    // This decision badly affects the variety of results.
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

                // Let's shorten the gridSolutionsCollection a bit, so it won't
                // get too big.
                gridSolutionsCollection = gridSolutionsCollection.OrderBy(solution => -solution.placedRoomsOrderedList.Count).ToList();
                if (gridSolutionsCollection.Count > gridSolutionCapacity + 2)
                    gridSolutionsCollection.RemoveRange(gridSolutionCapacity + 2, Math.Max(0, gridSolutionsCollection.Count - gridSolutionCapacity - 2));
            }

            // Not it's time to sort gridSolutionsCollection! It is sorted according to the number of placed rooms
            // in every solution.
            gridSolutionsCollection = gridSolutionsCollection.OrderBy(solution => -solution.placedRoomsOrderedList.Count).ToList() as List<GridSolution>;


            // bestGrid contains the best solution after all iterations. Only this
            // solution is used further for generating output data.
            int[,] bestGrid = gridSolutionsCollection[0].grid.Clone() as int[,];

            if (removeDeadEndsChecked)
                RemoveDeadEnds(ref bestGrid, gridSolutionsCollection[0].roomCellsList);

            if (removeAllCorridorsChecked)
                RemoveAllCorridors(ref bestGrid, gridSolutionsCollection[0].roomCellsList);

            // the list that contains all values from bestGrid, but in linear array.
            List<int> bestGridLinear = new List<int>();

            List<int> placedRoomsNums = new List<int>();
            // Remove all '9999' cells, they stand for cells that are outside the curve boundary of the building
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                {
                    // Again sorry for that lazy solution ¯\_(ツ)_/¯
                    if (bestGrid[i, j] != 9999)
                        bestGridLinear.Add(bestGrid[i, j]);
                    else
                        bestGridLinear.Add(0);

                    if (!placedRoomsNums.Contains(bestGrid[i, j]) && bestGrid[i, j] != 0 && bestGrid[i, j] != -1 && bestGrid[i, j] != 9999)
                        placedRoomsNums.Add(bestGrid[i, j]);
                }
            placedRoomsNums.Sort();

            // Indicate all RoomInstances that are not placed in the workingGrid on the graph in grasshopper window
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

            // missingRoomAdj is not the list that we're looking for. It considers wrong list of rooms (all of them, instead of only placed ones)
            // So we have to fix it a bit
            List<int> missingRoomAdjSortedList = new List<int>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
                missingRoomAdjSortedList.Add(missingRoomAdj[placedRoomsNums[i] - 1]);


            List<string> roomNames = new List<string>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
            {
                if (!initialRoomsList[placedRoomsNums[i] - 1].isHall)
                    roomNames.Add(initialRoomsList[placedRoomsNums[i] - 1].RoomName);
                else
                    roomNames.Add("&&HALL&&" + initialRoomsList[placedRoomsNums[i] - 1].RoomName);
            }


            // That should not be there I guess

            /*            // At the end let's convert all needed rooms to halls   
            for (int i = 0; i < bestGrid.GetLength(0); i++)
                for (int j = 0; j < bestGrid.GetLength(1); j++)
                    if (bestGrid[i, j] > 0 && bestGrid[i, j] <= initialRoomsList.Count)
                        if (initialRoomsList[bestGrid[i, j] - 1].isHall)
                            bestGrid[i, j] = -1;
            */


            // Now let's convert all cells to corresponding rooms
            List<Brep> roomBrepsList = new List<Brep>();
            for (int i = 0; i < placedRoomsNums.Count; i++)
            {
                List<Brep> cellsCollection = new List<Brep>();
                for (int q = 0; q < bestGridLinear.Count; q++)
                    if (bestGridLinear[q] == placedRoomsNums[i])
                        cellsCollection.Add(gridSurfaceArray[q].ToBrep());

                if (Brep.JoinBreps(cellsCollection, 0.01f) != null)
                    roomBrepsList.Add(Brep.JoinBreps(cellsCollection, 0.01f)[0]);

            }

            // Now let's convert all cells to united corridors structure
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

            string adjacenciesOutputString = "";
            for (int i = 0; i < adjArray.GetLength(0); i++)
                if (placedRoomsNums.Contains(adjArray[i, 0]) && placedRoomsNums.Contains(adjArray[i, 1]))
                    adjacenciesOutputString += placedRoomsNums.IndexOf(adjArray[i, 0]) + "-" + placedRoomsNums.IndexOf(adjArray[i, 1]) + "\n";

            DA.SetDataList("Room Breps", roomBrepsList);
            DA.SetData("Corridors", corridorsBrep);
            DA.SetDataList("MissingAdjacences", missingRoomAdjSortedList);
            DA.SetData("Adjacencies", adjacenciesOutputString);
            DA.SetDataList("Room Names", roomNames);

            this.Message = gridSolutionsCollection[0].placedRoomsOrderedList.Count + " of " + initialRoomsList.Count + " placed";
        }

        /// <summary>
        /// This function serves for removing dead ends from the corridor structure 
        /// after the main part of generation is executed already.
        /// The 'dead end' is a corridor, which is not actually required for the 
        /// corridor system to be coherent.
        /// </summary>
        /// <param name="grid"> workingGrid</param>
        /// <param name="roomCellsList"> List of all dimensions and positions of all rooms which are placed on a grid</param>
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

        /// <summary>
        /// This function serves for removing all corridors from the corridor structure 
        /// after the main part of generation is executed already.
        /// Corridors are added to the area of corresponding rooms.
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="roomCellsList"></param>
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
        /// Returns a list of room numbers, which have missing adjacences.
        /// Later this information is used for indicating rooms which miss connections.
        /// It is indicated with red dots in Rhino environment.
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="adjArray"></param>
        /// <returns></returns>
        public List<int> MissingRoomAdjacences(int[,] grid, int[,] adjArray)
        {
            List<int> missingAdj = new List<int>();// (adjArray.GetLength(0));
            int maxRoomNum = 0;
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                 if (grid[i,j] < 999)
                        maxRoomNum = Math.Max(maxRoomNum, grid[i, j]);

            //for (int i = 0; i < adjArray.GetLength(0); i++)
              //  maxRoomNum = 
                //maxRoomNum = Math.Max(Math.Max(maxRoomNum, adjArray[i, 0]), adjArray[i, 1]);

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
        /// This is the function which determines the position of a room and which 
        /// tries to place it. 
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="area"></param>
        /// <param name="roomNumber"></param>
        /// <param name="adjArray"></param>
        /// <param name="maxAdjDistance"></param>
        /// <param name="isHall"></param>
        /// <returns></returns>
        public bool TryPlaceNewRoomToTheGrid(ref int[,] grid, double area, int roomNumber, int[,] adjArray, double maxAdjDistance, bool isHall = false)
        {
            int[,] availableCellsGrid = new int[grid.GetLength(0), grid.GetLength(1)];  //= workingGrid;
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
            //yDim = (int)Math.Floor(area / (int)xDim);
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
                                room[i, j] = roomNumber;
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
                                room[i, j] = roomNumber;
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
                            room[i, j] = roomNumber;
            }
            else if (allSidesCorridorsChecked || isHall)
            {
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
                                                // AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "availableCell! " + roomNum + ": " + i + "_" + j);
                                            }
                                        }
                }

            // This list contains all possible solutions for a room placement. It means,
            // that for every suitable cell from availableCellsGrid the algorithm
            // tries to place the room in all different ways (RoomPosition contains 
            // these different orientations that a room can take). Then, solutions
            // that are successful are placed into this list.
            //
            // It is also important to mention, that each of these solutions gets its 
            // rating. It is calculated according to number of rooms that share a border with
            // this newly placed room. So we can be sure that there will be as few empty
            // spaces between rooms as possible.
            // For instance, if there are 2 options for a room position: one in which a room is
            // sharing one border with another room (so 3 other sides are naked), and the other one
            // in which a room is placed in a way that it shares 3 sides with other rooms (and only
            // one side is naked) -> the rating of the second solutions will obviously be higher.
            // This is how we ensure that the final solution will be as compact as possible.
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

        private void RemoveRoomFromGrid(ref int[,] grid, RoomCells roomCells)
        {
            for (int i = roomCells.x; i < roomCells.x + roomCells.w; i++)
                for (int j = roomCells.y; j < roomCells.y + roomCells.h; j++)
                    grid[i, j] = 0;
        }

        /// <summary>
        /// Basically this function takes the RoomPlacementSolution and places it.
        /// It fills the corresponding cells of grid with the given room number.
        /// </summary>
        /// <param name="solution"></param>
        /// <param name="room"></param>
        /// <param name="grid"></param>
        /// <param name="isHall"></param>
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
        /// 
        /// For instance, if there are 2 options for a room position: one in which a room is
        /// sharing one border with another room (so 3 other sides are naked), and the other one
        /// in which a room is placed in a way that it shares 3 sides with other rooms (and only
        /// one side is naked) -> the rating of the second solutions will obviously be higher.
        /// This is how we ensure that the final solution will be as compact as possible.
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="room"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="roomPosition"></param>
        /// <returns></returns>
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
        /// This function checks that the given room with the given RoomPosition
        /// can be successfully placed on the grid. So if all cells that this room
        /// will want to occupy are free (0), it will return true.
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="room"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="roomPosition"></param>
        /// <returns></returns>
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
        
        public bool GridContains(int[,] grid, int val)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    if (grid[i, j] == val)
                        return true;
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
            get { return new Guid("{78fe6801-611b-453f-946a-2fda951393eb}"); }
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

            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Boundary offset:", (obj, e) => { }, false);
            Menu_AppendTextItem(menu, boundaryOffset.ToString()
               , (obj, e) => { }, Menu_ChangeBoundaryOffsetNumberChanged, false);

            base.AppendAdditionalComponentMenuItems(menu);
        }


        public void Menu_ChangeBoundaryOffsetNumberChanged(object sender, string text)
        {
            try
            {
                this.boundaryOffset = Double.Parse(text);
            }
            catch (Exception) { }

            //this.ExpireSolution(false);
        }

        public void Menu_ChangeBoundaryOffsetNumberPressed(object sender, EventArgs e)
        {
            this.ExpireSolution(false);
        }

        public void Menu_CorridorsAsAdditionalSpacesChecked(object sender, EventArgs e)
        {
            corridorsAsAdditionalSpacesChecked = !corridorsAsAdditionalSpacesChecked;
            this.ExpireSolution(false);
        }

       

        public void Menu_RemoveDeadEndsClick(object sender, EventArgs e)
        {
            removeDeadEndsChecked = !removeDeadEndsChecked;

            if (removeDeadEndsChecked)
                removeAllCorridorsChecked = false;

            shouldOnlyRecomputeDeadEnds = true;
            this.ExpireSolution(false);
        }

        public void Menu_RemoveAllCorridorsClick(object sender, EventArgs e)
        {
            removeAllCorridorsChecked = !removeAllCorridorsChecked;
            if (removeAllCorridorsChecked)
                removeDeadEndsChecked = false;

            shouldOnlyRecomputeDeadEnds = true;
            this.ExpireSolution(false);
        }

        public void Menu_OneSideCorClick(object sender, EventArgs e)
        {
            if (!oneSideCorridorsChecked)
            {
                oneSideCorridorsChecked = !oneSideCorridorsChecked;

                twoSidesCorridorsChecked = !oneSideCorridorsChecked;
                allSidesCorridorsChecked = !oneSideCorridorsChecked;

                ExpireSolution(false);
            }
        }

        public void Menu_TwoSidesCorClick(object sender, EventArgs e)
        {
            if (!twoSidesCorridorsChecked)
            {
                twoSidesCorridorsChecked = !twoSidesCorridorsChecked;

                oneSideCorridorsChecked = !twoSidesCorridorsChecked;
                allSidesCorridorsChecked = !twoSidesCorridorsChecked;

                ExpireSolution(false);
            }
        }

        public void Menu_AllSidesCorClick(object sender, EventArgs e)
        {
            if (!allSidesCorridorsChecked)
            {
                allSidesCorridorsChecked = !allSidesCorridorsChecked;

                oneSideCorridorsChecked = !allSidesCorridorsChecked;
                twoSidesCorridorsChecked = !allSidesCorridorsChecked;

                ExpireSolution(false);
            }
        }
    }
}