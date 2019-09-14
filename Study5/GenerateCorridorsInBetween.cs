using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Magnetizing_FPG
{
    public class GenerateCorridorsInBetween : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GenerateCorridorsInBetween class.
        /// </summary>
        public GenerateCorridorsInBetween()
          : base("GenerateCorridorsInBetween", "GenerateCorridorsInBetween",
              "GenerateCorridorsInBetween",
              "Magnetizing_FPG", "Study_5")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "Boundary", "Boundary", GH_ParamAccess.item);
            pManager.AddCurveParameter("Rooms", "Rooms", "Rooms", GH_ParamAccess.list);
            pManager.AddNumberParameter("CellSize", "CellSize", "CellSize", GH_ParamAccess.item, 1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Corridors", "Corridors", "Corridors", GH_ParamAccess.list);
            pManager.AddIntegerParameter("XGridDim", "XGridDim", "", GH_ParamAccess.item);
            pManager.AddIntegerParameter("NumberGrid", "NumberGrid", "NumberGrid", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve boundary = null;
            List<Curve> rooms = new List<Curve>();
            List<Line> corridors = new List<Line>();
            double cellSize = 1;

            DA.GetData("Boundary", ref boundary);
            DA.GetDataList("Rooms", rooms);
            DA.GetData("CellSize", ref cellSize);

            int gridXCapacity = (int)((boundary.GetBoundingBox(false).Diagonal.X) / cellSize);
            int gridYCapacity = (int)((boundary.GetBoundingBox(false).Diagonal.Y) / cellSize);

            int[,] grid = new int[gridXCapacity, gridYCapacity];
            int[,] corridorsGrid = new int[gridXCapacity, gridYCapacity];

            InitializeGrid(ref grid, boundary.GetBoundingBox(false).Corner(true, true, true), cellSize, rooms);

            List<int> numberGrid = new List<int>();
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                    numberGrid.Add(grid[i, j]);


            // DO IT FOR HORIZONTALS
            for (int i = 0; i < corridorsGrid.GetLength(0) ; i++)
                for (int j = 0; j < corridorsGrid.GetLength(1) - 1; j++)
                {
                    if (grid[i, j] != grid[i, j + 1])
                        corridorsGrid[i, j] = 1;
                    else
                        corridorsGrid[i, j] = 0;
                }

            corridors.AddRange(GatherCorridorList(corridorsGrid, boundary, Direction.Horizontal));

            // DO IT FOR VERTICALS
            for (int i = 0; i < corridorsGrid.GetLength(0) - 1; i++)
                for (int j = 0; j < corridorsGrid.GetLength(1) ; j++)
                {
                    if (grid[i, j] != grid[i + 1, j])
                        corridorsGrid[i, j] = 1;
                    else
                        corridorsGrid[i, j] = 0;
                }

            corridors.AddRange(GatherCorridorList(corridorsGrid, boundary, Direction.Vertical));



            DA.SetDataList("Corridors", corridors);
            DA.SetData("XGridDim", gridXCapacity);
            DA.SetDataList("NumberGrid", numberGrid);
        }

        

        void InitializeGrid(ref int[,] grid, Point3d origin, double cellSize, List<Curve> rooms)
        {
            for (int i = 0; i < grid.GetLength(0); i++)
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    grid[i, j] = 0;
                    for (int l = 0; l < rooms.Count; l++)
                        if (rooms[l].Contains(origin + new Vector3d(cellSize * i + cellSize / 2, cellSize * j + cellSize / 2, 0)) == PointContainment.Inside)
                        {
                            grid[i, j] = l + 1;
                            break;
                        }
                }
        }

        enum Direction { Horizontal, Vertical };

        List<Line> GatherCorridorList(int[,] grid, Curve boundary, Direction direction)
        {
            List<Point3d> coordGrid = new List<Point3d>();
            double cellSizeX = boundary.GetBoundingBox(false).Diagonal.X / (grid.GetLength(0));
            double cellSizeY = boundary.GetBoundingBox(false).Diagonal.Y / (grid.GetLength(1));

            List<Line> corridors = new List<Line>();

            for (int i = 0; i < grid.GetLength(0) - 1; i++)
                for (int j = 0; j < grid.GetLength(1) - 1; j++)
                    if (grid[i, j] == 1)
                    {
                        if (direction == Direction.Horizontal)
                        {
                            if (i == 0 || grid[i - 1, j] != 1)
                            {
                                int enumer = 0;
                                while (i + enumer + 1 < grid.GetLength(0) && grid[i + enumer + 1, j] == 1)
                                    enumer++;

                                Point3d pointA = new Point3d(boundary.GetBoundingBox(false).Corner(true, true, true).X + cellSizeX * (i)
                                    , boundary.GetBoundingBox(false).Corner(true, true, true).Y + cellSizeY * (j + 1), 0);
                                Point3d pointB = new Point3d(boundary.GetBoundingBox(false).Corner(true, true, true).X + cellSizeX * (i + enumer + 1)
                                    , boundary.GetBoundingBox(false).Corner(true, true, true).Y + cellSizeY * (j + 1), 0);

                                corridors.Add(new Line(pointA, pointB));
                            }
                        }
                        else
                        {
                            if (j == 0 || grid[i, j - 1] != 1)
                            {
                                int enumer = 0;
                                while (j + enumer + 1 < grid.GetLength(1) && grid[i, enumer + 1 + j] == 1)
                                    enumer++;

                                Point3d pointA = new Point3d(boundary.GetBoundingBox(false).Corner(true, true, true).X + cellSizeX * (i + 1)
                                    , boundary.GetBoundingBox(false).Corner(true, true, true).Y + cellSizeY * (j), 0);
                                Point3d pointB = new Point3d(boundary.GetBoundingBox(false).Corner(true, true, true).X + cellSizeX * (i + 1)
                                    , boundary.GetBoundingBox(false).Corner(true, true, true).Y + cellSizeY * (j + enumer + 1), 0);

                                corridors.Add(new Line(pointA, pointB));
                            }
                        }
                    }

            return corridors;
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
            get { return new Guid("{a6d3f654-052a-48b7-8970-497425706aaf}"); }
        }
    }
}