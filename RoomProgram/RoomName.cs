using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace FloorPlan_Generator
{
    public class RoomName : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the RoomName class.
        /// </summary>
        public RoomName()
          : base("RoomNames", "RN",
              "RoomNames",
             "FloorPlanGen", "RoomProgram")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("House Instance", "HI", "House Instance with rooms", GH_ParamAccess.item);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Room Names", "N", "Room Names", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_ObjectWrapper wrapper = new GH_ObjectWrapper();
            DA.GetData(0, ref wrapper);
            List<string> roomNames = new List<string>();
            foreach (RoomInstance room in (wrapper.Value as HouseInstance).RoomInstances)
                roomNames.Add(room.RoomName);

            DA.SetDataList(0, roomNames);
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
            get { return new Guid("{b051a4a4-2da1-4b62-ba8c-9616eeab1ab7}"); }
        }
    }
}