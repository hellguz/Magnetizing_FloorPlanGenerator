using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Magnetizing_FPG.Properties;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Magnetizing_FPG
{
    /* HouseInstance class contains all the information about one
     * instance of a set of rooms that belong to one house/storey.
     */

    public class HouseInstance : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the HouseInstance class.
        /// </summary>
        public HouseInstance()
          : base("HouseInstance", "HouseInstance",
              "HouseInstance",
             "Magnetizing_FPG", "Magnetizing_FPG")
        {
        }

        public string HouseName = "HouseName";
        public string FloorName = "HouseFloor";
        public Curve boundary;
        public int[,] adjArray;
        public Point3d startingPoint;
        public List<string> adjStrList = new List<string>();
        public bool tryRotateBoundary = false;

        public List<string> RoomInstancesGuids
        {
            get { return (m_attributes as HouseInstanceAttributes).roomInstancesGuidList; }
            //  set { }
        }
        public List<RoomInstance> RoomInstances
        {
            get {
                List<RoomInstance> list = new List<RoomInstance>();
                foreach (string guid in (m_attributes as HouseInstanceAttributes).roomInstancesGuidList)
                    if (guid != "")
                    list.Add(OnPingDocument().FindComponent(new Guid(guid)) as RoomInstance);
                return list;
            }
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
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            adjStrList = new List<string>();
            List<string> roomNames = new List<string>();

            (m_attributes as HouseInstanceAttributes).AddPrevioslyConnectedRooms();

            DA.GetData(0, ref boundary);
            try
            {
                DA.GetData(1, ref startingPoint);
            }
            catch (Exception) { }


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
            /// If checked, boundary will be rotated in such a way that it is 
            /// easier to pack rectangular rooms inside it. After algorithm is 
            /// executed, the boundary is rotated back to initial state.
            Menu_AppendItem(menu, "Try Rotate Boundary", Menu_TryRotateBoundaryClick, true, tryRotateBoundary);

            base.AppendAdditionalComponentMenuItems(menu);
        }
        protected void Menu_TryRotateBoundaryClick(object sender, EventArgs e)
        {
            tryRotateBoundary = !tryRotateBoundary;
            this.ExpireSolution(false);
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