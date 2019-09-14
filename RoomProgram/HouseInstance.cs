using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FloorPlan_Generator.Properties;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace FloorPlan_Generator
{
    public class HouseInstance : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the HouseInstance class.
        /// </summary>
        public HouseInstance()
          : base("HouseInstance", "HouseInstance",
              "HouseInstance",
             "FloorPlanGen", "RoomProgram")
        {
        }

        public string HouseName = "HouseName";
        public string FloorName = "HouseFloor";
        public Curve boundary;
        public int[,] adjArray;
        public Point3d startingPoint;
        public List<string> adjStrList = new List<string>();
        public bool tryRotateBoundary = false;

        public List<RoomInstance> RoomInstances
        {
            get { return (m_attributes as HouseInstanceAttributes).roomInstancesList; }
            //  set { }
        }

        public override void CreateAttributes()
        {
            m_attributes = new HouseInstanceAttributes(this);
        }
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "B", "Boundary", GH_ParamAccess.item);
            pManager.AddPointParameter("Entrance Point", "EP", "Entrance Point", GH_ParamAccess.item);
        }


        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("HouseInstance", "HI", "HouseInstance", GH_ParamAccess.item);
            /*
            pManager.AddGenericParameter("RoomList", "RoomList", "RoomList", GH_ParamAccess.list);
            pManager.AddTextParameter("Adjacencies", "Adjacencies", "Adjacencies", GH_ParamAccess.list);
            pManager.AddTextParameter("Room Names", "Room Names", "Room Names", GH_ParamAccess.list);*/
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //     DA.GetData(0, ref boundary);

            adjStrList = new List<string>();
            List<string> roomNames = new List<string>();

            DA.GetData(0, ref boundary);
            try
            {
                DA.GetData(1, ref startingPoint);
            }
            catch (Exception e) { }

            for (int i = 0; i < RoomInstances.Count; i++)
            {
                for (int j = 0; j < RoomInstances[i].AdjacentRoomsList.Count; j++)
                    if (i + 1 < (RoomInstances.FindIndex(item => item.RoomId == (RoomInstances[i].AdjacentRoomsList[j] as RoomInstance).RoomId) + 1))
                        adjStrList.Add((i + 1) + " - " + (RoomInstances.FindIndex(item => item.RoomId == (RoomInstances[i].AdjacentRoomsList[j] as RoomInstance).RoomId) + 1) + "\n");


                roomNames.Add(RoomInstances[i].RoomName);
            }

            adjArray = new int[adjStrList.Count, 2];

            for (int i = 0; i < adjStrList.Count; i++)
            {
                adjArray[i, 0] = Int32.Parse((adjStrList[i].Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[0]));
                adjArray[i, 1] = Int32.Parse((adjStrList[i].Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[1]));

            }


            /* List<GH_ObjectWrapper> wrappersList = new List<GH_ObjectWrapper>();
             foreach (RoomInstance room in RoomInstances)
                 wrappersList.Add(new GH_ObjectWrapper(room));
                 */

            // DA.SetDataList("RoomList", wrappersList);
            // DA.SetDataList("Adjacencies", adj);
            // DA.SetDataList("Room Names", roomNames);

            DA.SetData(0, this);
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
                return Resources.HouseInstanceIcon;
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
           Menu_AppendItem(menu, "Try Rotate Boundary", Menu_TryRotateBoundaryClick, true, tryRotateBoundary);

            base.AppendAdditionalComponentMenuItems(menu);
        }
        protected void Menu_TryRotateBoundaryClick(object sender, EventArgs e)
        {
            tryRotateBoundary = !tryRotateBoundary;
            this.ExpireSolution(true);
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{9fb8241c-27d0-4683-9964-26c181c9ce36}"); }
        }
    }
}