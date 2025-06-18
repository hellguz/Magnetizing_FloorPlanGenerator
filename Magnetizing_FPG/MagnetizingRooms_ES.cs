using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Magnetizing_FPG.Core;

namespace Magnetizing_FPG
{
    public class MagnetizingRooms_ES : GH_Component
    {
        // --- Member Variables for UI state ---
        private double boundaryOffset = 2.3f;
        private bool oneSideCorridorsChecked = false;
        private bool twoSidesCorridorsChecked = true;
        private bool allSidesCorridorsChecked = false;
        private bool corridorsAsAdditionalSpacesChecked = true;
        private bool removeDeadEndsChecked = true;
        private bool removeAllCorridorsChecked = false;
        private bool shouldOnlyRecomputeDeadEnds = false; // This state might need rethinking with the new architecture.

        // Store last valid result to use when only minor UI changes are made
        private FloorPlanResult lastResult = null;
        
        public MagnetizingRooms_ES()
          : base("MagnetizingRooms_ES", "Magnetizing_FPG",
              "Generates a floor plan using an evolutionary algorithm.",
              "Magnetizing_FPG", "Magnetizing_FPG")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("House Instance", "HI", "A House Instance object containing the architectural program.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iterations", "I", "Number of iterations for the solver. Generally, 300-900 works best.", GH_ParamAccess.item, 300);
            pManager.AddNumberParameter("MaxAdjDistance", "MAD", "Maximum distance between two connected rooms. Generally, 2-3 works best.", GH_ParamAccess.item, 2.0);
            pManager.AddNumberParameter("CellSize(m)", "CS(m)", "Resolution of the grid in meters. 1m is used by default.", GH_ParamAccess.item, 1.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Corridors", "C", "Corridors as a single Brep", GH_ParamAccess.item);
            pManager.AddBrepParameter("Room Breps", "Rs", "Rooms as a list of Breps", GH_ParamAccess.list);
            pManager.AddTextParameter("Room Names", "Ns", "The names of the placed rooms", GH_ParamAccess.list);
            pManager.AddTextParameter("Adjacencies", "A", "Adjacency connections for visualization (e.g., \"0-1\")", GH_ParamAccess.item);
            pManager.AddIntegerParameter("MissingAdjacencies", "!A", "Count of missing connections for each placed room", GH_ParamAccess.list);
            pManager.AddCurveParameter("Boundary", "B", "The original boundary curve", GH_ParamAccess.item);
            pManager.AddCurveParameter("Boundary+Offset", "Bo", "The boundary curve with an offset applied", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // --- Input Gathering ---
            IHouseInstance houseInstance = null;
            GH_ObjectWrapper houseInstanceWrapper = new GH_ObjectWrapper();
            if (!DA.GetData("House Instance", ref houseInstanceWrapper) || !(houseInstanceWrapper.Value is IHouseInstance))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A valid House Instance is required.");
                return;
            }
            houseInstance = houseInstanceWrapper.Value as IHouseInstance;

            int iterations = 0;
            double maxAdjDistance = 0;
            double cellSize = 1;
            DA.GetData("Iterations", ref iterations);
            DA.GetData("MaxAdjDistance", ref maxAdjDistance);
            DA.GetData("CellSize(m)", ref cellSize);

            // --- Boundary Output ---
            Curve boundaryCrv = houseInstance.boundary;
            DA.SetData("Boundary", boundaryCrv);
            Curve offsetBoundary = boundaryCrv.Offset(Plane.WorldXY, boundaryOffset, 0.001f, CurveOffsetCornerStyle.Sharp)[0];
            DA.SetData("Boundary+Offset", offsetBoundary);

            // --- Solver Execution ---
            var solver = new FloorPlanSolver();
            RoomPosition corridorStyle = RoomPosition.Undefined; // This needs to be set from the UI state
            if (oneSideCorridorsChecked) corridorStyle = RoomPosition.BottomLeft; // Example mapping
            if (twoSidesCorridorsChecked) corridorStyle = RoomPosition.BottomRight; // Example mapping
            if (allSidesCorridorsChecked) corridorStyle = RoomPosition.TopLeft; // Example mapping

            // TODO: A more robust check is needed to decide whether to re-run the full solver
            // or just post-process the last result. For now, we re-run.
            var result = solver.Solve(houseInstance, iterations, maxAdjDistance, cellSize, removeDeadEndsChecked, removeAllCorridorsChecked, corridorsAsAdditionalSpacesChecked, corridorStyle);
            lastResult = result; // Cache the result

            if (result == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Solver did not produce a valid result.");
                return;
            }
            
            // --- Output Setting ---
            DA.SetData("Corridors", result.CorridorsBrep);
            DA.SetDataList("Room Breps", result.RoomBreps);
            DA.SetDataList("Room Names", result.RoomNames);
            DA.SetData("Adjacencies", result.AdjacenciesOutputString);
            DA.SetDataList("MissingAdjacences", result.MissingRoomAdjSortedList);

            this.Message = $"{result.PlacedRoomsCount} of {result.TotalRoomsCount} placed";
            
            // --- Handle unplaced rooms UI ---
            // This part requires access back to the GH document, which complicates full decoupling.
            // For now, we handle it here. A better approach might be an event system.
            List<int> placedRoomIds = new List<int>();
            // This is tricky because the result doesn't directly contain the original Room IDs.
            // This indicates a need to pass more info through the result object.
        }

        #region UI and Menu Items

        public override Guid ComponentGuid => new Guid("{78fe6801-611b-453f-946a-2fda951393eb}");
        protected override System.Drawing.Bitmap Icon => Properties.Resources.MagnetizingRoomsIcon;

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
            Menu_AppendTextItem(menu, boundaryOffset.ToString(), (obj, e) => { }, Menu_ChangeBoundaryOffsetNumberChanged, false);

            base.AppendAdditionalComponentMenuItems(menu);
        }

        private void Menu_ChangeBoundaryOffsetNumberChanged(object sender, string text)
        {
            if (double.TryParse(text, out double newOffset))
            {
                boundaryOffset = newOffset;
                ExpireSolution(true);
            }
        }

        private void Menu_CorridorsAsAdditionalSpacesChecked(object sender, EventArgs e)
        {
            corridorsAsAdditionalSpacesChecked = !corridorsAsAdditionalSpacesChecked;
            this.ExpireSolution(true);
        }

        private void Menu_RemoveDeadEndsClick(object sender, EventArgs e)
        {
            removeDeadEndsChecked = !removeDeadEndsChecked;
            if (removeDeadEndsChecked) removeAllCorridorsChecked = false;
            shouldOnlyRecomputeDeadEnds = true; // Mark that a full re-solve might not be needed
            this.ExpireSolution(true);
        }

        private void Menu_RemoveAllCorridorsClick(object sender, EventArgs e)
        {
            removeAllCorridorsChecked = !removeAllCorridorsChecked;
            if (removeAllCorridorsChecked) removeDeadEndsChecked = false;
            shouldOnlyRecomputeDeadEnds = true; // Mark that a full re-solve might not be needed
            this.ExpireSolution(true);
        }

        private void Menu_OneSideCorClick(object sender, EventArgs e)
        {
            oneSideCorridorsChecked = true;
            twoSidesCorridorsChecked = false;
            allSidesCorridorsChecked = false;
            ExpireSolution(true);
        }

        private void Menu_TwoSidesCorClick(object sender, EventArgs e)
        {
            oneSideCorridorsChecked = false;
            twoSidesCorridorsChecked = true;
            allSidesCorridorsChecked = false;
            ExpireSolution(true);
        }

        private void Menu_AllSidesCorClick(object sender, EventArgs e)
        {
            oneSideCorridorsChecked = false;
            twoSidesCorridorsChecked = false;
            allSidesCorridorsChecked = true;
            ExpireSolution(true);
        }
        #endregion
    }
}