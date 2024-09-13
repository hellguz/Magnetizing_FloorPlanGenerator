using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace FloorPlan_Generator
{
    public class ShneiderKoenig : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ShneiderKoenig()
          : base("ShneiderKoenig", "ASpi",
              "",
              "FloorPlanGen", "Study_1")
        {
        }

        private List<Curve> rooms = new List<Curve>();
        List<string> adjStrList;
        private Random random = new Random();
        double proportionThreshold = 0;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "Boundary", "Boundary", GH_ParamAccess.item);
            pManager.AddCurveParameter("Rooms", "Rooms", "Rooms", GH_ParamAccess.list);
            pManager.AddTextParameter("Adjacences", "Adjacences", "Adjacences as list of string \"1 - 3, 2 - 4,..\"", GH_ParamAccess.list, " - ");
            pManager.AddNumberParameter("ProportionThreshold", "ProportionThreshold", "ProportionThreshold, >= 1", GH_ParamAccess.item, 2);
            pManager.AddBooleanParameter("Reset", "Reset", "Reset", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Rooms", "Rooms", "Rooms", GH_ParamAccess.list);
            pManager.AddLineParameter("Adjacences", "Adjacences", "Adjacence lines", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //List<Curve> rooms = new List<Curve>();
            Curve boundary = new PolylineCurve();
            bool shouldInstantiateRooms = false;
            adjStrList = new List<string>();// { "1 - 2", "3 - 0", "2 - 4" };
            List<Line> adjLines = new List<Line>();

            DA.GetData("Reset", ref shouldInstantiateRooms);
            DA.GetData("Boundary", ref boundary);
            if (shouldInstantiateRooms || rooms.Count == 0)
            {
                rooms.Clear();
                DA.GetDataList(1, rooms);
            }

            DA.GetData("ProportionThreshold", ref proportionThreshold);
            DA.GetDataList("Adjacences", adjStrList);

            Shuffle(ref adjStrList);


            rooms = AdjacentContraction(rooms, adjStrList, boundary, out adjLines);
            rooms = CollisionDetectionMain(rooms, boundary);

            List<GH_Curve> GH_rooms = new List<GH_Curve>();
            foreach (Curve c in rooms)
                GH_rooms.Add(new GH_Curve(c));

            DA.SetDataList(0, GH_rooms);
            DA.SetDataList(1, adjLines);
        }

        private List<Curve> CollisionDetectionMain(List<Curve> roomCurves, Curve boundary)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < roomCurves.Count; i++)
                indexes.Add(i);

            Shuffle(ref indexes);

            for (int l = 0; l < roomCurves.Count; l++)
            {
                int i = indexes[l];

                // Check roomCurves[i] and roomCurves[j] intersection
                // Then let's move both rooms by 1/2 of the rebounding vector
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

                // Check roomCurves[i] and boundary intersection
                // Let's do it twice to be sure that X and Y positions are both defined perfectly
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
                                roomCurves[i].Translate( new Vector3d(0, (intersectResult.reboundingVector.Y > 0 ? -1 : 1) * (roomCurves[i].GetBoundingBox(false).Diagonal.Y - Math.Abs(intersectResult.reboundingVector.Y)), 0));
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

        private IntersectResult Intersect2Curves(Curve a, Curve b)
        {
            IntersectResult result = new IntersectResult();
            if (Curve.PlanarCurveCollision(a, b, Plane.WorldXY, 0.01f))
            {
                Curve[] unionCurveArray = Curve.CreateBooleanIntersection(a, b);
                if (unionCurveArray.Length > 0)
                {
                    result.intersect = true;
                    result.unionCurve = unionCurveArray[0];

                    // Find the smallest dimesion of unionCurve
                    Point3d minPoint = result.unionCurve.GetBoundingBox(false).Min;
                    Point3d maxPoint = result.unionCurve.GetBoundingBox(false).Max;

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


                    roomCurves[adj.aIndex].Translate(-attractVector / 2 + aDim / 2);
                    roomCurves[adj.bIndex].Translate(attractVector / 2 + bDim / 2);
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

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{7b00cbd7-910f-47cf-9601-348f8d999506}"); }
        }
    }
}
