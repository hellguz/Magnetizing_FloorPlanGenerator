using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

public interface IHouseInstance
{
    /// <summary>
    /// The building boundary.
    /// </summary>
    Curve boundary { get; }

    /// <summary>
    /// The entrance point.
    /// </summary>
    Point3d startingPoint { get; }

    /// <summary>
    /// If the boundary should be rotated.
    /// </summary>
    bool tryRotateBoundary { get; }

    /// <summary>
    /// The list of room instances.
    /// </summary>
    List<IRoomInstance> RoomInstances { get; }

    /// <summary>
    /// The adjacency data as strings (e.g. "1-2").
    /// </summary>
    List<string> adjStrList { get; }

    int[,] adjArray { get; set; }
}

public interface IRoomInstance
{
    /// <summary>
    /// A unique room identifier.
    /// </summary>
    int RoomId { get; set; }

    /// <summary>
    /// The room area (in m²).
    /// </summary>
    double RoomArea { get; set; }

    /// <summary>
    /// The room name.
    /// </summary>
    string RoomName { get; set; }

    /// <summary>
    /// Whether the room is a hall.
    /// </summary>
    bool isHall { get; set; }

    /// <summary>
    /// A list of rooms adjacent to this room.
    /// </summary>
    List<IRoomInstance> AdjacentRoomsList { get; }

    bool hasMissingAdj { get; set; }
}



namespace Magnetizing_FPG
{
    /// <summary>
    /// A GH_Component that implements IHouseInstance.
    /// This advanced house builds its own internal rooms (of type InternalRoomInstance)
    /// and wires up adjacency according to input.
    /// </summary>
    public class HouseInstanceAdvanced : GH_Component, IHouseInstance
    {
        // IHouseInstance interface properties
        public Curve boundary { get; private set; }
        public Point3d startingPoint { get; private set; }
        public bool tryRotateBoundary { get; private set; }
        public List<IRoomInstance> RoomInstances { get; private set; } = new List<IRoomInstance>();
        public List<string> adjStrList { get; private set; } = new List<string>();
        // Additional: Provide an "adjArray" property for backward compatibility.
        public int[,] adjArray
        {
            get
            {
                if (adjStrList == null || adjStrList.Count == 0)
                    return new int[0, 0];
                int n = adjStrList.Count;
                int[,] arr = new int[n, 2];
                for (int i = 0; i < n; i++)
                {
                    string[] parts = adjStrList[i].Split('-');
                    if (parts.Length == 2)
                    {
                        int a, b;
                        if (int.TryParse(parts[0], out a) && int.TryParse(parts[1], out b))
                        {
                            arr[i, 0] = a;
                            arr[i, 1] = b;
                        }
                    }
                }
                return arr;
            }
            set { }
        }
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
            Curve boundary = null;
            if (!DA.GetData(0, ref boundary)) return;
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
            this.boundary = boundary;
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

            // Wire up adjacency for each pair (e.g., "1-2" links room 1 and room 2).
            foreach (string s in adjStrList)
            {
                string[] parts = s.Split('-');
                if (parts.Length == 2)
                {
                    int a, b;
                    if (int.TryParse(parts[0], out a) && int.TryParse(parts[1], out b))
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

        public override Guid ComponentGuid
        {
            get { return new Guid("E2D7B0F4-1111-2222-3333-444455556666"); }
        }

        protected override Bitmap Icon
        {
            get { return null; }
        }

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

        bool IRoomInstance.hasMissingAdj { get; set; }

        public override string ToString()
        {
            return $"{RoomName} ({RoomArea} m²)";
        }
    }
}
