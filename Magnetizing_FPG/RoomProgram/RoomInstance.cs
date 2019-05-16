using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Magnetizing_FPG.Properties;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;

namespace Magnetizing_FPG
{
    /// <summary>
    ///  RoomInstance class contains all the information about 
    ///  one single room: name, ID, all connected rooms.
    /// </summary>
    public class RoomInstance : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the RoomInstance class.
        /// </summary>
        public RoomInstance()
        
          : base("RoomInstance", "RoomInstance",
              "RoomInstance",
             "Magnetizing_FPG", "Magnetizing_FPG")
        {
            if (entranceIds == null)
                entranceIds = new List<int>();
            RoomName = "Room " + RoomId.ToString();
            RoomId = maxId++;

            allRoomInstances.Add(this);
            m_attributes = new RoomInstanceAttributes(this);

            // if (m_attributes is RoomInstanceAttributes)
            foreach (RoomInstance room in (m_attributes as RoomInstanceAttributes).targetObjectList)
                (m_attributes as RoomInstanceAttributes).RemoveAdjacence(room as RoomInstance);

            //(m_attributes as RoomInstanceAttributes).targetObjectList.Clear();
            //(m_attributes as RoomInstanceAttributes).writerTargetObjectsListString = new string[0];
        }

        public class IntPair
        {
          public string a;
          public string b;
            public IntPair(string A, string B)
            {
                a = A;
                b = B;
            }
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
        }

        public double RoomArea = 20;
        public int RoomId;// = maxId++;
        public static int maxId = 0;
        public static List<int> entranceIds; // If there is an entrance which should be placed first, it's id will be stored here
        public string RoomName;// = "Room Name";
        public bool isHall = false; // true if the room is to be a hall (connecting-space)
        public bool hasMissingAdj = false;
        public static List<IntPair> allAdjacencesList = new List<IntPair>();
        public static List<RoomInstance> allRoomInstances = new List<RoomInstance>();

        public List<RoomInstance> AdjacentRoomsList
        {
            get {
                List<RoomInstance> list = new List<RoomInstance>();
                foreach (IntPair intPair in allAdjacencesList)
                {
                    if (intPair.a == this.InstanceGuid.ToString())
                        if (OnPingDocument().FindComponent(new Guid(intPair.b)) != null)
                        list.Add(OnPingDocument().FindComponent(new Guid(intPair.b)) as RoomInstance);
                    if (intPair.b == this.InstanceGuid.ToString())
                        if (OnPingDocument().FindComponent(new Guid(intPair.a)) != null)
                            list.Add(OnPingDocument().FindComponent(new Guid(intPair.a)) as RoomInstance);
                }
                list = list.Distinct().ToList();

                return list;
            }

        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("a", "a", "a", GH_ParamAccess.item);
        }

        public override void CreateAttributes()
        {
            m_attributes = new RoomInstanceAttributes(this);
        }
        

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //  AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Name: " + RoomName +"\nArea: " + RoomArea);

            //AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, AdjacentRoomsList.Count.ToString());

            DA.SetData(0, RoomName);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Resources.RoomInstanceIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{85beb9d6-e6cd-4499-9659-2d51784d948c}"); }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Set as Entrance", Menu_SetAsEntrance, true, RoomInstance.entranceIds.Contains(this.RoomId));
            Menu_AppendItem(menu, "Set as Hall", Menu_SetAsHall, true, isHall);

            base.AppendAdditionalComponentMenuItems(menu);
        }

        private void Menu_SetAsEntrance(object sender, EventArgs e)
        {
            if (!entranceIds.Contains(RoomId))
            {
                if (entranceIds.Count > 0)
                {
                    List<int> copyList = RoomInstance.entranceIds.ConvertAll(i => i);

                    List<RoomInstance> allConnectedRooms = new List<RoomInstance>();
                    foreach (RoomInstance room in (((m_attributes as RoomInstanceAttributes).AssignedHouseInstance as HouseInstance).RoomInstances))
                        if (room != null)
                        if (room.RoomId != RoomId)
                            allConnectedRooms.Add(room);

                    int prevCount;
                    List<RoomInstance> tempRoomList = new List<RoomInstance>();
                    do
                    {
                        tempRoomList.Clear();
                        prevCount = allConnectedRooms.Count;
                        foreach (RoomInstance room in allConnectedRooms)
                            foreach (RoomInstance roomConnected in room.AdjacentRoomsList)
                                if (roomConnected != null)
                                if (!allConnectedRooms.Contains(roomConnected) && roomConnected.RoomId != RoomId)
                                    tempRoomList.Add(roomConnected);

                        allConnectedRooms.AddRange(tempRoomList);
                    } while (prevCount != allConnectedRooms.Count);


                    foreach (int id in copyList)
                        if (id != RoomId)
                            if (allConnectedRooms.Find(room => (room as RoomInstance).RoomId == id) != null)
                            {
                                entranceIds.Remove(id);
                                //     AdjacentRoomsList.Find(room => (room as RoomInstance).RoomId == id).ExpireSolution(false);
                            }
                }
                RoomInstance.entranceIds.Add(this.RoomId);
            }
            else
                entranceIds.Remove(RoomId);

            ExpireSolution(true);
        }

        private void Menu_SetAsHall(object sender, EventArgs e)
        {
            isHall = !isHall;
            ExpireSolution(false);
        }

    }
}