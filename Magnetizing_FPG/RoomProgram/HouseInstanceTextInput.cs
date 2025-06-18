using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Magnetizing_FPG
{
    /// <summary>
    /// A GH_Component that implements IHouseInstance from text-based inputs. 
    /// This advanced component builds its own internal room definitions
    /// and wires up adjacency without requiring graphical RoomInstance components. 
    /// </summary>
    public class HouseInstanceAdvanced : GH_Component, IHouseInstance
    {
        // IHouseInstance interface properties
        public Curve boundary { get; private set; } 
        public Point3d startingPoint { get; private set; } 
        public bool tryRotateBoundary { get; private set; } 
        public List<IRoomInstance> RoomInstances { get; private set; } = new List<IRoomInstance>(); 
        public List<string> adjStrList { get; private set; } = new List<string>(); 
        public int[,] adjArray { get; set; }

        public HouseInstanceAdvanced()
          : base("HouseInstanceAdvanced", "HIAdv",
                 "Advanced House Instance which takes information about the rooms in text forms. Does not need RoomInstance objects.",
                 "Magnetizing_FPG", "Magnetizing_FPG")
        {
        }


        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "B", "House boundary", GH_ParamAccess.item); 
            pManager.AddPointParameter("Entrance Point", "EP", "Entrance point", GH_ParamAccess.item); 
            pManager.AddNumberParameter("Room Areas", "RA", "List of room areas (m²)", GH_ParamAccess.list); 
            pManager.AddTextParameter("Room Names", "RN", "List of room names", GH_ParamAccess.list); 
            pManager.AddBooleanParameter("Is Hall", "IH", "List of hall flags", GH_ParamAccess.list); 
            pManager.AddTextParameter("Adjacency", "AD", "Adjacency list as strings (e.g. \"1-2\"). Room numbering starts with 1.", GH_ParamAccess.list); 
            pManager.AddBooleanParameter("Rotate Boundary", "RB", "Try rotating boundary", GH_ParamAccess.item, false); 
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("HouseInstance", "HI", "House Instance", GH_ParamAccess.item); 
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve basic house inputs.
            Curve boundaryInput = null;
            if (!DA.GetData(0, ref boundaryInput)) return;
            Point3d entrance = Point3d.Unset;
            if (!DA.GetData(1, ref entrance)) return;
            List<double> areas = new List<double>();
            DA.GetDataList(2, areas);
            List<string> names = new List<string>();
            DA.GetDataList(3, names);
            List<bool> halls = new List<bool>();
            DA.GetDataList(4, halls);
            List<string> adj = new List<string>();
            DA.GetDataList(5, adj);
            bool rb = false;
            DA.GetData(6, ref rb);

            // Set properties.
            this.boundary = boundaryInput; 
            this.startingPoint = entrance; 
            this.tryRotateBoundary = rb; 

            // Process room areas, names and hall flags.
            if (areas.Count == 0)
                areas.Add(40.0); 
            int roomCount = areas.Count; 

            // Build final room names (auto-generate if necessary)
            List<string> finalNames = new List<string>();
            for (int i = 0; i < roomCount; i++)
            {
                if (i < names.Count && !string.IsNullOrEmpty(names[i]))
                    finalNames.Add(names[i]); 
                else
                    finalNames.Add("Room " + (i + 1).ToString()); 
            }

            // Build final hall flags (default to false if missing)
            List<bool> finalHalls = new List<bool>();
            for (int i = 0; i < roomCount; i++)
            {
                if (i < halls.Count)
                    finalHalls.Add(halls[i]); 
                else
                    finalHalls.Add(false); 
            }

            // Create internal room instances.
            RoomInstances.Clear(); 
            for (int i = 0; i < roomCount; i++)
            {
                InternalRoomInstance room = new InternalRoomInstance();
                room.RoomId = i + 1; 
                room.RoomArea = areas[i]; 
                room.RoomName = finalNames[i]; 
                room.isHall = finalHalls[i]; 
                RoomInstances.Add(room); 
            }

            // Process and store adjacency strings.
            adjStrList.Clear(); 
            foreach (string s in adj)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    adjStrList.Add(s.Replace(" ", "")); 
            }
            
            // Build the integer array for adjacencies
            adjArray = new int[adjStrList.Count, 2];
            for (int i = 0; i < adjStrList.Count; i++)
            {
                string[] parts = adjStrList[i].Split('-'); 
                if (parts.Length == 2) 
                {
                    if (int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int b)) 
                    {
                        adjArray[i, 0] = a; 
                        adjArray[i, 1] = b; 
                    }
                }
            }


            // Wire up adjacency for each pair (e.g., "1-2" links room 1 and room 2).
            foreach (string s in adjStrList) 
            {
                string[] parts = s.Split('-'); 
                if (parts.Length == 2) 
                {
                    if (int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int b)) 
                    {
                        if (a >= 1 && b >= 1 && a <= roomCount && b <= roomCount)
                        {
                            IRoomInstance roomA = RoomInstances[a - 1]; 
                            IRoomInstance roomB = RoomInstances[b - 1]; 
                            if (!roomA.AdjacentRoomsList.Contains(roomB))
                                roomA.AdjacentRoomsList.Add(roomB); 
                            if (!roomB.AdjacentRoomsList.Contains(roomA))
                                roomB.AdjacentRoomsList.Add(roomA); 
                        }
                    }
                }
            }

            // Output this advanced house instance.
            DA.SetData(0, this); 
        }

        public override Guid ComponentGuid => new Guid("E2D7B0F4-1111-2222-3333-444455556666");

        protected override Bitmap Icon => null;
    }

    /// <summary>
    /// A plain class implementing IRoomInstance. 
    /// This internal room does not rely on any GH_Component GUI elements. 
    /// </summary>
    public class InternalRoomInstance : IRoomInstance
    {
        public int RoomId { get; set; } 
        public double RoomArea { get; set; } 
        public string RoomName { get; set; } 
        public bool isHall { get; set; } 
        public List<IRoomInstance> AdjacentRoomsList { get; private set; } = new List<IRoomInstance>(); 
        public bool hasMissingAdj { get; set; }

        public override string ToString()
        {
            return $"{RoomName} ({RoomArea} m²)"; 
        }
    }
}

