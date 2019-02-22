using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Linq;
using Rhino;
using ClipperLib;

using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;
using System.Windows.Forms;
using Magnetizing_FPG.Properties;

namespace Magnetizing_FPG
{
    public class SpringSystem_ES : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public SpringSystem_ES()
          : base("SpringSystem_ES", "SpringSystem_ES",
              "Evolutionary Strategy + SpringSystem",
              "Magnetizing_FPG", "Magnetizing_FPG")
        {
        }

        private List<Curve> rooms = new List<Curve>();
        private List<Curve> originalInputRooms = new List<Curve>();
        private List<Curve> currentInputRooms = new List<Curve>();
        List<string> adjStrList;
        private Random random = new Random();
        double proportionThreshold = 0;
        GeneCollection geneCollection;
        double boundaryArea = 0;
        bool Menu_ShuffleRoomsAtFirst = false;


        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "Boundary", "Boundary", GH_ParamAccess.item);
            pManager.AddCurveParameter("Rooms", "Rooms", "Rooms as curves", GH_ParamAccess.list);
            pManager.AddTextParameter("Adjacencies", "Adjacencies", "Adjacencies as list of string \"1 - 3, 2 - 4,..\"", GH_ParamAccess.list, " - ");
            pManager.AddNumberParameter("ProportionThreshold", "ProportionThreshold", "ProportionThreshold, >= 1", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("FF Balance", "FF Balance", "FF Balance\n[0,1]", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("SpringCollAllGenes", "SpringCollAllGenes", "Spring collisions in all genes, not only in the best one. " +
                "It makes execution slower, but provides much better results, because every gene is getting better faster and they compete" +
                "with each other more honestly", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("AdjustArea", "AdjustArea", "Adjust sum of rooms areas to the area of the boundary.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "Reset", "Reset", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Rooms", "Rooms", "Rooms", GH_ParamAccess.list);
            pManager.AddLineParameter("Adjacencies", "Adjacences", "Adjacence lines", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve boundary = new PolylineCurve();
            bool shouldInstantiateRooms = false;
            adjStrList = new List<string>();
            List<Line> adjLines = new List<Line>();
            bool shouldClearGenes = false;
            bool springCollAllGenes = false;
            bool adjustArea = false;
            
            DA.GetData("Reset", ref shouldInstantiateRooms);
            DA.GetData("Boundary", ref boundary);

            currentInputRooms.Clear();
            DA.GetDataList(1, currentInputRooms);

            if (shouldInstantiateRooms || rooms.Count == 0 || boundaryArea != AreaMassProperties.Compute(boundary).Area
                || originalInputRooms == null || !RoomListsAreEqual(originalInputRooms, currentInputRooms))
            {
                shouldClearGenes = true;
                boundaryArea = AreaMassProperties.Compute(boundary).Area;
                rooms.Clear();
                originalInputRooms.Clear();
                DA.GetDataList(1, rooms);
                DA.GetDataList(1, originalInputRooms);
            }

            DA.GetData("ProportionThreshold", ref proportionThreshold);
            DA.GetData("FF Balance", ref Gene.fitnessFunctionBalance);
            DA.GetDataList("Adjacencies", adjStrList);
            DA.GetData("SpringCollAllGenes", ref springCollAllGenes);
            DA.GetData("AdjustArea", ref adjustArea);
            
            int[,] adjArray = new int[adjStrList.Count, 2];

            for (int i = 0; i < adjStrList.Count; i++)
            {
                adjArray[i, 0] = Int32.Parse((adjStrList[i].Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[0]));
                adjArray[i, 1] = Int32.Parse((adjStrList[i].Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[1]));

            }

            Gene.proportionThreshold = proportionThreshold;
            Gene.adjacencyList = adjArray;

            if (shouldClearGenes)
            {
                // Adapt the summarized rooms area to the boundary area if it is needed
                if (adjustArea)
                {
                    double roomSumArea = 0;
                    foreach (Curve room in rooms)
                        roomSumArea += AreaMassProperties.Compute(room).Area;

                    foreach (Curve room in rooms)
                        room.Transform(Transform.Scale(room.GetBoundingBox(false).Center, Math.Sqrt(boundaryArea / roomSumArea)));// AreaMassProperties.Compute(room).Area;

                }

                shouldClearGenes = false;
                geneCollection = new GeneCollection(15, boundary);

                // Let's create some starting genes
                foreach (Gene gene in geneCollection.genes)
                    if (Menu_ShuffleRoomsAtFirst)
                        gene.InstantiateRandomly(rooms);
                    else
                        gene.Instantiate(rooms);
            }

            
            geneCollection.Iterate();

            if (springCollAllGenes)
                for (int l = 0; l < geneCollection.genes.Count; l++)
                {
                    Gene gene = geneCollection.genes[l];
                    rooms = gene.GetCurves();

                    rooms = CollisionDetectionMain(rooms, boundary);

                    gene.collection.Clear();
                    gene.Instantiate(rooms);
                }


            rooms = geneCollection.GetBest();
            AdjacentContraction(rooms, adjStrList, boundary, out adjLines);

            List<GH_Curve> GH_rooms = new List<GH_Curve>();
            foreach (Curve c in rooms)
                GH_rooms.Add(new GH_Curve(c));

            DA.SetDataList(0, GH_rooms);
            DA.SetDataList(1, adjLines);
        }

        protected bool RoomListsAreEqual(List<Curve> a, List<Curve> b)
        {
            if (a == null || b == null)
                return false;
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                Vector3d differenceV = a[i].GetBoundingBox(false).Center - b[i].GetBoundingBox(false).Center;
                if (Math.Abs(differenceV.X) + Math.Abs(differenceV.Y) > 0.001f)
                {
                    //AddRuntimeMessage(GH_RuntimeMessageLevel.Error, (Math.Abs(differenceV.X) + Math.Abs(differenceV.Y).ToString()));
                    return false;
                }
            }

            return true;
        }

        protected List<Curve> CopyCurveList(List<Curve> a)
        {
            List<Curve> output = new List<Curve>();
            foreach (Curve curve in a)
                output.Add(curve.DuplicateCurve());

            return output;
        }

        private List<Curve> CollisionDetectionMain(List<Curve> roomCurves, Curve boundary)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < roomCurves.Count; i++)
                indexes.Add(i);


            // Check roomCurves[i] and roomCurves[j] intersection
            // Then let's move both rooms by 1/2 of the rebounding vector
            for (int l = 0; l < roomCurves.Count; l++)
            {
                int i = indexes[l];

                for (int j = i + 1; j < roomCurves.Count; j++)

                {
                    IntersectResult intersectResult = new IntersectResult();
                    intersectResult = Intersect2Curves(roomCurves[i], roomCurves[j]);

                    if (intersectResult.intersect)
                    {
                        if (intersectResult.reboundingVector.X != 0)
                        {

                            // Change the proportions of rooms
                            double iScaleFactor = 1 - Math.Abs(intersectResult.reboundingVector.X / roomCurves[i].GetBoundingBox(false).Diagonal.X / 2);
                            Point3d iScaleAnchor = roomCurves[i].GetBoundingBox(false).Center + new Vector3d(intersectResult.reboundingVector.X > 0 ?
                                roomCurves[i].GetBoundingBox(false).Diagonal.X / 2 : -roomCurves[i].GetBoundingBox(false).Diagonal.X / 2, 0, 0);


                            double jScaleFactor = 1 - Math.Abs(intersectResult.reboundingVector.X / roomCurves[j].GetBoundingBox(false).Diagonal.X / 2);
                            Point3d jScaleAnchor = roomCurves[j].GetBoundingBox(false).Center + new Vector3d(intersectResult.reboundingVector.X > 0 ?
                                -roomCurves[j].GetBoundingBox(false).Diagonal.X / 2 : roomCurves[j].GetBoundingBox(false).Diagonal.X / 2, 0, 0);

                            roomCurves[j].Transform(Transform.Scale(new Plane(jScaleAnchor, Vector3d.ZAxis), jScaleFactor, 1 / jScaleFactor, 1));
                            roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), iScaleFactor, 1 / iScaleFactor, 1));


                            // If the proportions of both rooms are in [0.5; 2] -> ok
                            // Else - change the position of the rooms and return previous scale
                            if (!(GetRoomXYProportion(roomCurves[i]) > 1 / proportionThreshold &&
                            GetRoomXYProportion(roomCurves[i]) < proportionThreshold &&
                            GetRoomXYProportion(roomCurves[j]) > 1 / proportionThreshold &&
                            GetRoomXYProportion(roomCurves[j]) < proportionThreshold))
                            {
                                roomCurves[j].Transform(Transform.Scale(new Plane(jScaleAnchor, Vector3d.ZAxis), 1 / jScaleFactor, jScaleFactor, 1));
                                roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), 1 / iScaleFactor, iScaleFactor, 1));

                                roomCurves[i].Translate(new Vector3d(intersectResult.reboundingVector.X / 2, 0, 0));
                                roomCurves[j].Translate(new Vector3d(-intersectResult.reboundingVector.X / 2, 0, 0));

                            }
                        }
                        else
                        {
                            // Change the proportions of rooms
                            double iScaleFactor = 1 - Math.Abs(intersectResult.reboundingVector.Y / roomCurves[i].GetBoundingBox(false).Diagonal.Y / 2);
                            Point3d iScaleAnchor = roomCurves[i].GetBoundingBox(false).Center + new Vector3d(0, intersectResult.reboundingVector.Y > 0 ?
                                roomCurves[i].GetBoundingBox(false).Diagonal.Y / 2 : -roomCurves[i].GetBoundingBox(false).Diagonal.Y / 2, 0);


                            double jScaleFactor = 1 - Math.Abs(intersectResult.reboundingVector.Y / roomCurves[j].GetBoundingBox(false).Diagonal.Y / 2);
                            Point3d jScaleAnchor = roomCurves[j].GetBoundingBox(false).Center + new Vector3d(0, intersectResult.reboundingVector.Y > 0 ?
                                -roomCurves[j].GetBoundingBox(false).Diagonal.Y / 2 : roomCurves[j].GetBoundingBox(false).Diagonal.Y / 2, 0);

                            roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), 1 / iScaleFactor, iScaleFactor, 1));
                            roomCurves[j].Transform(Transform.Scale(new Plane(jScaleAnchor, Vector3d.ZAxis), 1 / jScaleFactor, jScaleFactor, 1));


                            // If the proportions of both rooms are in [0.5; 2]
                            // Else - change the position of the rooms
                            if (!(GetRoomXYProportion(roomCurves[i]) > 1 / proportionThreshold &&
                              GetRoomXYProportion(roomCurves[i]) < proportionThreshold &&
                              GetRoomXYProportion(roomCurves[j]) > 1 / proportionThreshold &&
                              GetRoomXYProportion(roomCurves[j]) < proportionThreshold))
                            {
                                roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), iScaleFactor, 1 / iScaleFactor, 1));
                                roomCurves[j].Transform(Transform.Scale(new Plane(jScaleAnchor, Vector3d.ZAxis), jScaleFactor, 1 / jScaleFactor, 1));

                                roomCurves[i].Translate(new Vector3d(0, intersectResult.reboundingVector.Y / 2, 0));
                                roomCurves[j].Translate(new Vector3d(0, -intersectResult.reboundingVector.Y / 2, 0));
                            }
                        }
                    }
                    else
                    {
                        if (Curve.PlanarClosedCurveRelationship(roomCurves[i], roomCurves[j], Plane.WorldXY, 0.0001f) != RegionContainment.Disjoint)
                        {
                            BoundingBox aRoomBB = roomCurves[j].GetBoundingBox(false);
                            BoundingBox bRoomBB = roomCurves[i].GetBoundingBox(false);
                            double xDist = Math.Abs((aRoomBB.Center - bRoomBB.Center).X) - (aRoomBB.Diagonal.X / 2 - bRoomBB.Diagonal.X / 2);
                            double yDist = Math.Abs((aRoomBB.Center - bRoomBB.Center).Y) - (aRoomBB.Diagonal.Y / 2 - bRoomBB.Diagonal.Y / 2);
                            if (xDist > yDist)
                            {
                                if ((aRoomBB.Center - bRoomBB.Center).X > 0)
                                    roomCurves[i].Translate(new Vector3d(xDist - bRoomBB.Diagonal.X, 0, 0));
                                else
                                    roomCurves[i].Translate(new Vector3d(-xDist + bRoomBB.Diagonal.X, 0, 0));
                            }
                            else
                            {
                                if ((aRoomBB.Center - bRoomBB.Center).Y > 0)
                                    roomCurves[i].Translate(new Vector3d(0, yDist - bRoomBB.Diagonal.Y, 0));
                                else
                                    roomCurves[i].Translate(new Vector3d(0, -yDist + bRoomBB.Diagonal.Y, 0));
                            }
                        }
                    }

                }
            }


            // Check roomCurves[i] and boundary intersection
            // Let's do it twice to be sure that X and Y positions are both defined perfectly
            for (int l = 0; l < roomCurves.Count; l++)
            {
                int i = indexes[l];
                for (int t = 0; t < 1; t++)
                {
                    IntersectResult intersectResult = new IntersectResult();
                    intersectResult = Intersect2Curves(roomCurves[i], boundary);

                    if (intersectResult.intersect)
                    {
                        double boundaryProportionThreshold = 1;// proportionThreshold;

                        if (Math.Abs(intersectResult.reboundingVector.X) > 0.01f)
                        {

                            // Change the proportions of rooms
                            double iScaleFactor = Math.Abs(intersectResult.reboundingVector.X / roomCurves[i].GetBoundingBox(false).Diagonal.X);
                            Point3d iScaleAnchor = roomCurves[i].GetBoundingBox(false).Center + new Vector3d(intersectResult.reboundingVector.X > 0 ?
                                -roomCurves[i].GetBoundingBox(false).Diagonal.X / 2 : roomCurves[i].GetBoundingBox(false).Diagonal.X / 2, 0, 0);

                            roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), iScaleFactor, 1 / iScaleFactor, 1));


                            // If the proportions of both rooms are in [0.5; 2] -> ok
                            // Else - change the position of the rooms and return previous scale
                            if (!(GetRoomXYProportion(roomCurves[i]) > 1 / boundaryProportionThreshold &&
                            GetRoomXYProportion(roomCurves[i]) < boundaryProportionThreshold))
                            {
                                roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), 1 / iScaleFactor, iScaleFactor, 1));
                                roomCurves[i].Translate(new Vector3d((intersectResult.reboundingVector.X > 0 ? -1 : 1) * (roomCurves[i].GetBoundingBox(false).Diagonal.X - Math.Abs(intersectResult.reboundingVector.X)), 0, 0));
                            }
                        }
                        else if (Math.Abs(intersectResult.reboundingVector.Y) > 0.01f)
                        { // Change the proportions of rooms
                            double iScaleFactor = Math.Abs(intersectResult.reboundingVector.Y / roomCurves[i].GetBoundingBox(false).Diagonal.Y);
                            Point3d iScaleAnchor = roomCurves[i].GetBoundingBox(false).Center + new Vector3d(0, intersectResult.reboundingVector.Y > 0 ?
                                -roomCurves[i].GetBoundingBox(false).Diagonal.Y / 2 : roomCurves[i].GetBoundingBox(false).Diagonal.Y / 2, 0);

                            roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), 1 / iScaleFactor, iScaleFactor, 1));


                            // If the proportions of both rooms are in [0.5; 2] -> ok
                            // Else - change the position of the rooms and return previous scale
                            if (!(GetRoomXYProportion(roomCurves[i]) > 1 / boundaryProportionThreshold &&
                            GetRoomXYProportion(roomCurves[i]) < boundaryProportionThreshold))
                            {
                                roomCurves[i].Transform(Transform.Scale(new Plane(iScaleAnchor, Vector3d.ZAxis), iScaleFactor, 1 / iScaleFactor, 1));
                                roomCurves[i].Translate(new Vector3d(0, (intersectResult.reboundingVector.Y > 0 ? -1 : 1) * (roomCurves[i].GetBoundingBox(false).Diagonal.Y - Math.Abs(intersectResult.reboundingVector.Y)), 0));
                            }
                        }
                    }
                    else
                    {
                        if (Curve.PlanarClosedCurveRelationship(roomCurves[i], boundary, Plane.WorldXY, 0.0001f) != RegionContainment.AInsideB)
                        {
                            BoundingBox boundaryBB = boundary.GetBoundingBox(false);
                            BoundingBox roomBB = roomCurves[i].GetBoundingBox(false);
                            double xDist = Math.Abs((boundaryBB.Center - roomBB.Center).X) - boundaryBB.Diagonal.X / 2 - roomBB.Diagonal.X / 2;
                            double yDist = Math.Abs((boundaryBB.Center - roomBB.Center).Y) - boundaryBB.Diagonal.Y / 2 - roomBB.Diagonal.Y / 2;
                            if (xDist > yDist)
                            {
                                if ((boundaryBB.Center - roomBB.Center).X > 0)
                                    roomCurves[i].Translate(new Vector3d(xDist + roomBB.Diagonal.X, 0, 0));
                                else
                                    roomCurves[i].Translate(new Vector3d(-xDist - roomBB.Diagonal.X, 0, 0));
                            }
                            else
                            {
                                if ((boundaryBB.Center - roomBB.Center).Y > 0)
                                    roomCurves[i].Translate(new Vector3d(0, yDist + roomBB.Diagonal.Y, 0));
                                else
                                    roomCurves[i].Translate(new Vector3d(0, -yDist - roomBB.Diagonal.Y, 0));
                            }
                        }
                    }
                }
            }
            return roomCurves;
        }

        public PolylineCurve PathToPolyline(Path path, int clipperPrecision = 100)
        {
            List<Point3d> points = new List<Point3d>();
            foreach (IntPoint intPoint in path)
                points.Add(new Point3d(intPoint.X / (double)clipperPrecision, intPoint.Y / (double)clipperPrecision, 0));
            PolylineCurve polyline = new PolylineCurve(points);

            return polyline;
        }

        public Path CurveToPath(Curve curve, int clipperPrecision = 100)
        {
            Path points = new Path();

            for (int i = 0; i < 4; i++)
                points.Add(new IntPoint((int)(curve.GetBoundingBox(false).GetCorners()[i].X * clipperPrecision),
                    (int)(curve.GetBoundingBox(false).GetCorners()[i].Y * clipperPrecision)));

            return points;
        }

        private IntersectResult Intersect2Curves(Curve a, Curve b)
        {
            int clipperPrecision = 100;
            IntersectResult result = new IntersectResult();
            if (Curve.PlanarCurveCollision(a, b, Plane.WorldXY, 0.001f))
            {
                Clipper clipper = new Clipper();
                Path subjectA = CurveToPath(a, clipperPrecision);
                Path subjectB = CurveToPath(b, clipperPrecision);
                Paths solution = new Paths();

                clipper.AddPath(subjectA, PolyType.ptClip, true);
                clipper.AddPath(subjectB, PolyType.ptSubject, true);

                clipper.Execute(ClipType.ctIntersection, solution, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

                if (solution.Count > 0)
                {
                    result.intersect = true;
                    PolylineCurve pl = PathToPolyline(solution[0], clipperPrecision);
                    result.unionCurve = pl;

                    Point3d minPoint = pl.GetBoundingBox(false).Min;
                    Point3d maxPoint = pl.GetBoundingBox(false).Max;

                    if (maxPoint.X - minPoint.X > maxPoint.Y - minPoint.Y)
                    {
                        result.reboundingVector = new Vector2d(0, -(maxPoint.Y - minPoint.Y));
                        if (AreaMassProperties.Compute(a).Centroid.Y > AreaMassProperties.Compute(b).Centroid.Y)
                            result.reboundingVector.Y *= -1;
                    }
                    else
                    {
                        result.reboundingVector = new Vector2d(-(maxPoint.X - minPoint.X), 0);
                        if (AreaMassProperties.Compute(a).Centroid.X > AreaMassProperties.Compute(b).Centroid.X)
                            result.reboundingVector.X *= -1;
                    }

                }
            }
            else
            {
                result.intersect = false;
                result.reboundingVector = Vector2d.Unset;
                result.unionCurve = null;
            }
            return result;
        }

        private struct IntersectResult
        {
            public Vector2d reboundingVector;
            public bool intersect;
            public Curve unionCurve;
        }

        private double GetRoomXYProportion(Curve room)
        {
            return (room.GetBoundingBox(false).Diagonal.X / (double)room.GetBoundingBox(false).Diagonal.Y);
        }


        private void Shuffle(ref List<Curve> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                Curve value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private void Shuffle(ref List<string> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                string value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
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

        private List<Curve> AdjacentContraction(List<Curve> roomCurves, List<string> adjacenceStrings, Curve boundary, out List<Line> adjLines)
        {

            List<Adjacence> adjacences = new List<Adjacence>();

            foreach (string adjString in adjacenceStrings)
            {
                adjacences.Add(new Adjacence());
                adjacences[adjacences.Count - 1].aIndex = Int32.Parse((adjString.Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[0]));
                adjacences[adjacences.Count - 1].bIndex = Int32.Parse((adjString.Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries)[1]));

            }

            foreach (Adjacence adj in adjacences)
            {
                Vector3d attractVector = roomCurves[adj.aIndex].GetBoundingBox(false).Center - roomCurves[adj.bIndex].GetBoundingBox(false).Center;

                Vector3d aDim = roomCurves[adj.aIndex].GetBoundingBox(false).Diagonal;
                Vector3d bDim = roomCurves[adj.bIndex].GetBoundingBox(false).Diagonal;

                if (Math.Abs(attractVector.X) - (aDim.X + bDim.X) / 2 > 0.001f ||
                    Math.Abs(attractVector.Y) - (aDim.Y + bDim.Y) / 2 > 0.001f)
                {

                    if (Math.Abs(attractVector.X) > Math.Abs(attractVector.Y))
                    {
                        attractVector.Y = 0;
                        aDim.Y = 0;
                        bDim.Y = 0;
                    }
                    else
                    {
                        attractVector.X = 0;
                        aDim.X = 0;
                        bDim.X = 0;
                    }

                    Point3d attractCenter = roomCurves[adj.aIndex].GetBoundingBox(false).Center + attractVector / 2;
                    if (attractVector.X != 0)
                        if (attractCenter.X > roomCurves[adj.aIndex].GetBoundingBox(false).Center.X)
                            bDim *= -1;
                        else
                            aDim *= -1;
                    else
                        if (attractCenter.Y > roomCurves[adj.aIndex].GetBoundingBox(false).Center.Y)
                        bDim *= -1;
                    else
                        aDim *= -1;


                    // roomCurves[adj.aIndex].Translate(-attractVector / 2 + aDim / 2);
                    // roomCurves[adj.bIndex].Translate(attractVector / 2 + bDim / 2);
                }
            }

            adjLines = new List<Line>();

            foreach (Adjacence adj in adjacences)
            {
                adjLines.Add(new Line(roomCurves[adj.aIndex].GetBoundingBox(false).Center, roomCurves[adj.bIndex].GetBoundingBox(false).Center));
            }

            return roomCurves;
        }

        private class Adjacence
        {
            public int aIndex;
            public int bIndex;
        }

        public class Room
        {
            public double CenterX = 0;
            public double CenterY = 0;

            public double width = 0;
            // height is used as a getter/setter function, so this variable doesn't exist actually

            public double area = 0;

            public Room(double CenterX, double CenterY, double width, double height)
            {
                this.CenterX = CenterX;
                this.CenterY = CenterY;
                this.width = width;
                this.area = width * height;
            }

            public Room()
            {
            }

            public Room(Point3d centerPoint, double width, double height)
            {
                this.CenterX = centerPoint.X;
                this.CenterY = centerPoint.Y;
                this.width = width;
                this.area = width * height;
            }

            public Room(Curve roomCurve)
            {
                this.CenterX = roomCurve.GetBoundingBox(false).Center.X;
                this.CenterY = roomCurve.GetBoundingBox(false).Center.Y;
                this.width = roomCurve.GetBoundingBox(false).Diagonal.X;
                this.area = roomCurve.GetBoundingBox(false).Diagonal.X * roomCurve.GetBoundingBox(false).Diagonal.Y;
            }

            // IMPORTANT: think about what precision means
            public Path GetClipperLibPath(int precision = 100)
            {

                IntPoint a = new IntPoint((int)((CenterX - width / 2) * precision), (int)((CenterY - height / 2) * precision));
                IntPoint b = new IntPoint((int)((CenterX - width / 2) * precision), (int)((CenterY + height / 2) * precision));
                IntPoint c = new IntPoint((int)((CenterX + width / 2) * precision), (int)((CenterY + height / 2) * precision));
                IntPoint d = new IntPoint((int)((CenterX + width / 2) * precision), (int)((CenterY - height / 2) * precision));

                Path output = new Path(new List<IntPoint>() { a, b, c, d });
                return output;
            }

            public Room Clone()
            {
                Room r = new Room(CenterX, CenterY, width, this.height);
                return r;
            }

            public double height
            {
                get
                {
                    return area / width;
                }
                set
                {
                    width = area / value;
                }
            }

        }

        public class Gene
        {
            Random random = new Random(Guid.NewGuid().GetHashCode());
            public List<Room> collection = new List<Room>();
            public static int[,] adjacencyList;// = new int[0, 2];
            public static Curve boundary;
            public static double proportionThreshold = 2f;
            public static double fitnessFunctionBalance = 0.5f;


            public Gene(int[,] adjacences)
            {
                adjacencyList = adjacences;
            }

            public Gene()
            {
            }

            public double FitnessFunctionG()
            {
                // return 3;
                double fitnessFunctionVar = 0;
                int precision = 100;

                Clipper cc = new ClipperLib.Clipper();

                Paths solution = new Paths();
                Paths subjects = new Paths();
                Paths clips = new Paths();



                foreach (Room room in collection)
                    subjects.Add(new Path(room.GetClipperLibPath(precision)));

                IntPoint boundaryA = new IntPoint(
                    (int)((boundary.GetBoundingBox(false).Center.X - boundary.GetBoundingBox(false).Diagonal.X / 2) * precision)
                    , (int)((boundary.GetBoundingBox(false).Center.Y - boundary.GetBoundingBox(false).Diagonal.Y / 2) * precision));

                IntPoint boundaryB = new IntPoint(
                    (int)((boundary.GetBoundingBox(false).Center.X - boundary.GetBoundingBox(false).Diagonal.X / 2) * precision)
                    , (int)((boundary.GetBoundingBox(false).Center.Y + boundary.GetBoundingBox(false).Diagonal.Y / 2) * precision));

                IntPoint boundaryC = new IntPoint(
                    (int)((boundary.GetBoundingBox(false).Center.X + boundary.GetBoundingBox(false).Diagonal.X / 2) * precision)
                    , (int)((boundary.GetBoundingBox(false).Center.Y + boundary.GetBoundingBox(false).Diagonal.Y / 2) * precision));

                IntPoint boundaryD = new IntPoint(
                    (int)((boundary.GetBoundingBox(false).Center.X + boundary.GetBoundingBox(false).Diagonal.X / 2) * precision)
                    , (int)((boundary.GetBoundingBox(false).Center.Y - boundary.GetBoundingBox(false).Diagonal.Y / 2) * precision));


                clips.Add(new Path(new List<IntPoint>() { boundaryA, boundaryB, boundaryC, boundaryD }));

                cc.AddPaths(subjects, PolyType.ptSubject, true);
                cc.AddPaths(clips, PolyType.ptClip, true);


                cc.Execute(ClipType.ctIntersection, solution, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

                foreach (Path path in solution)
                {
                    fitnessFunctionVar += Clipper.Area(path);
                }


                return fitnessFunctionVar;
            }

            public double FitnessFunctionT()
            {
                double distanceSum = 0;
                for (int i = 0; i < adjacencyList.GetLength(0); i++)
                {
                    Room a = collection[adjacencyList[i, 0]].Clone();
                    Room b = collection[adjacencyList[i, 1]].Clone();

                    double distX = Math.Abs(a.CenterX - b.CenterX) - a.width / 2f - b.width / 2f;
                    double distY = Math.Abs(a.CenterY - b.CenterY) - a.height / 2f - b.height / 2f;

                    distanceSum += Math.Max(Math.Abs(distX), Math.Abs(distY));
                }
                return distanceSum;
            }

            public double FitnessFunction()
            {
                return (FitnessFunctionG() * fitnessFunctionBalance + 1 / (FitnessFunctionT()) * (1 - fitnessFunctionBalance));
            }

            public Gene Clone()
            {
                List<Room> newColl = new List<Room>(collection);
                Gene t = new Gene();
                // t.collection = newColl;
                foreach (Room r in collection)
                    t.collection.Add(r.Clone());

                return t;
            }

            public void InstantiateRandomly(List<Curve> inputCollection)
            {
                for (int i = 0; i < inputCollection.Count; i++)
                {
                    collection.Add(new Room(inputCollection[i]));

                    // set width
                    collection[collection.Count - 1].width = Math.Sqrt(collection[collection.Count - 1].area
                        / proportionThreshold) + random.NextDouble()
                        * (Math.Sqrt(collection[collection.Count - 1].area * proportionThreshold)
                        - Math.Sqrt(collection[collection.Count - 1].area / proportionThreshold));

                    // set X and Y
                    collection[collection.Count - 1].CenterX = random.NextDouble()
                        * (boundary.GetBoundingBox(false).Diagonal.X - collection[collection.Count - 1].width)
                        + boundary.GetBoundingBox(false).Corner(true, true, true).X
                     + collection[collection.Count - 1].width / 2;

                    collection[collection.Count - 1].CenterY = random.NextDouble()
                        * (boundary.GetBoundingBox(false).Diagonal.Y - collection[collection.Count - 1].height)
                        + boundary.GetBoundingBox(false).Corner(true, true, true).Y
                        + collection[collection.Count - 1].height / 2;
                }
            }

            public Gene Instantiate(List<Curve> inputCollection)
            {
                for (int i = 0; i < inputCollection.Count; i++)
                {
                    collection.Add(new Room(inputCollection[i]));
                }
                return this;
            }

            public List<Curve> GetCurves()
            {
                List<Curve> curvesList = new List<Curve>();
                foreach (Room room in collection)
                {
                    curvesList.Add(new Rectangle3d(new Plane(new Point3d(room.CenterX - room.width / 2, room.CenterY - room.height / 2, 0), Vector3d.ZAxis), room.width, room.height).ToNurbsCurve());
                }
                return curvesList;
            }

            public void MutateSomehow()
            {
                double mutationProb = 0.3f;
                double mutationChangeK = 0.2f; // (boundary.width or .height or ... ) * mutationChangeK = max change
                foreach (Room room in collection)
                {
                    if (random.NextDouble() < mutationProb)
                    {
                        if (random.NextDouble() < mutationProb)
                            room.CenterX += boundary.GetBoundingBox(false).Diagonal.X * mutationChangeK * (random.NextDouble() - 0.5f);

                        if (random.NextDouble() < mutationProb)
                            room.CenterY += boundary.GetBoundingBox(false).Diagonal.Y * mutationChangeK * (random.NextDouble() - 0.5f);

                        if (random.NextDouble() < mutationProb)
                            room.width = Math.Sqrt(room.area / proportionThreshold) + random.NextDouble()
                                * (Math.Sqrt(room.area * proportionThreshold) - Math.Sqrt(room.area / proportionThreshold));

                        // Check whether the room is outside the boundary

                        if (room.CenterX - room.width / 2 < boundary.GetBoundingBox(false).Corner(true, true, true).X)
                            room.CenterX = boundary.GetBoundingBox(false).Corner(true, true, true).X + room.width / 2;

                        if (room.CenterX + room.width / 2 > boundary.GetBoundingBox(false).Corner(false, false, false).X)
                            room.CenterX = boundary.GetBoundingBox(false).Corner(false, false, false).X - room.width / 2;

                        if (room.CenterY - room.height / 2 < boundary.GetBoundingBox(false).Corner(true, true, true).Y)
                            room.CenterY = boundary.GetBoundingBox(false).Corner(true, true, true).Y + room.height / 2;

                        if (room.CenterY + room.height / 2 > boundary.GetBoundingBox(false).Corner(false, false, false).Y)
                            room.CenterY = boundary.GetBoundingBox(false).Corner(false, false, false).Y - room.height / 2;
                    }
                }
            }

        }

        /// <summary>
        /// This class serves for containing the collection of genes and doing
        /// some operations with them (sorting, iterating,..).
        /// </summary>
        public class GeneCollection
        {
            Random random = new Random(Guid.NewGuid().GetHashCode());
            public List<Gene> genes;
            int genesNumber;

            public GeneCollection(int genesNumber, Curve boundary)
            {
                Gene.boundary = boundary;
                this.genesNumber = genesNumber;
                genes = new List<Gene>();
                for (int i = 0; i < genesNumber; i++)
                    genes.Add(new Gene());
            }

            public void SortGenes()
            {
                genes = genes.OrderBy(i => -i.FitnessFunction()).ToList();

            }

            /// <summary>
            /// Well, so this function produces new genes via mutations, cross-over and 
            /// generating new ones.
            /// </summary>
            public void Iterate()
            {
                double newGenesNumK = 4;
                // mutations + cross-over + newbies

                #region CROSS-OVER
                int[,] mutationIndexes = new int[(int)(genesNumber * newGenesNumK), 2];
                for (int i = 0; i < genesNumber * newGenesNumK; i++)
                {
                    mutationIndexes[i, 0] = random.Next(genesNumber);
                    mutationIndexes[i, 1] = random.Next(genesNumber);
                }

                for (int i = 0; i < genesNumber * newGenesNumK; i++)
                {
                    genes.Add(CrossOverGenes(genes[mutationIndexes[i, 0]], genes[mutationIndexes[i, 1]]));
                }

                #endregion

                #region MUTATION
                for (int i = 0; i < genesNumber * newGenesNumK; i++)
                {
                    Gene mutGene = genes[random.Next(0, genes.Count)].Clone();

                    mutGene.MutateSomehow();
                    genes.Add(mutGene);
                }
                #endregion

                #region NEWBIES
                for (int i = 0; i < genesNumber * newGenesNumK; i++)
                {
                    Gene newGene = new Gene();
                    newGene.InstantiateRandomly(genes[0].GetCurves());
                    genes.Add(newGene);
                }
                #endregion

                SortGenes();
                genes.RemoveRange(genesNumber, genes.Count - 1 - genesNumber);

            }

            private Gene CrossOverGenes(Gene a, Gene b)
            {
                double mutationProb = 0.5f;
                Gene c = a.Clone();

                for (int i = 0; i < a.collection.Count; i++)
                    if (random.NextDouble() > mutationProb)
                        c.collection[i] = b.collection[i].Clone();

                return c;
            }

            public List<Curve> GetBest()
            {
                SortGenes();
                return genes[0].GetCurves();
            }
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Resources.SpringSystem_ESIcon;
            }
        }

        /// <summary>
        /// When Menu_ShuffleRoomsAtFirstChecked, in the very beginning of algorithm's work
        /// all the rooms will be shuffled randomly.  It serves for producing various results.
        /// But when SpringSystem_ES component takes results from MagnetizingRooms_ES, rooms 
        /// already have their approximate position and, therefore, must not be shuffled.
        /// (initially Menu_ShuffleRoomsAtFirstChecked = false)
        /// </summary>
        /// <param name="menu"></param>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {

            Menu_AppendItem(menu, "Shuffle rooms at first", Menu_ShuffleRoomsAtFirstChecked, true, Menu_ShuffleRoomsAtFirst);

            base.AppendAdditionalComponentMenuItems(menu);
        }

        public void Menu_ShuffleRoomsAtFirstChecked(object sender, EventArgs e)
        {
            Menu_ShuffleRoomsAtFirst = !Menu_ShuffleRoomsAtFirst;
            this.ExpireSolution(true);
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{7b02cbd7-910f-47cf-9601-348f8d999506}"); }
        }
    }
}
