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

        // Component configuration - used by the solver
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
            pManager.AddIntegerParameter("Random Seed", "RS", "Random seed for reproducible results. 0 = random seed each update, any other number = fixed seed for testing", GH_ParamAccess.item, 0);
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
            // 1. Prepare Inputs
            var inputs = new SolverInputs();

            GH_ObjectWrapper houseInstanceWrapper = new GH_ObjectWrapper();
            DA.GetData("House Instance", ref houseInstanceWrapper);
            inputs.HouseInstance = houseInstanceWrapper.Value as IHouseInstance;

            int iterations = 0;
            double maxAdjDistance = 0;
            double cellSize = 1;
            int randomSeed = 0;

            DA.GetData("Iterations", ref iterations);
            DA.GetData("MaxAdjDistance", ref maxAdjDistance);
            DA.GetData("CellSize(m)", ref cellSize);
            DA.GetData("Random Seed", ref randomSeed);

            inputs.Iterations = iterations;
            inputs.MaxAdjDistance = maxAdjDistance;
            inputs.CellSize = cellSize;
            inputs.RandomSeed = randomSeed;

            // Set component settings
            inputs.BoundaryOffset = this.boundaryOffset;
            inputs.OneSideCorridorsChecked = this.oneSideCorridorsChecked;
            inputs.TwoSidesCorridorsChecked = this.twoSidesCorridorsChecked;
            inputs.AllSidesCorridorsChecked = this.allSidesCorridorsChecked;
            inputs.CorridorsAsAdditionalSpacesChecked = this.corridorsAsAdditionalSpacesChecked;
            inputs.RemoveDeadEnds = this.removeDeadEndsChecked;
            inputs.RemoveAllCorridors = this.removeAllCorridorsChecked;

            // 2. Call the Solver
            var solver = new MagnetizingSolver();
            SolverOutputs outputs = solver.Execute(inputs);

            // 3. Set Outputs
            DA.SetDataList("Room Breps", outputs.RoomBreps);
            DA.SetData("Corridors", outputs.Corridors);
            DA.SetDataList("Room Names", outputs.RoomNames);
            DA.SetData("Adjacencies", outputs.Adjacencies);
            DA.SetDataList("MissingAdjacences", outputs.MissingAdjacences);
            DA.SetData("Boundary", outputs.Boundary);
            DA.SetData("Boundary+Offset", outputs.BoundaryWithOffset);

            this.Message = outputs.Message;
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