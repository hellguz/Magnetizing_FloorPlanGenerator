using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Collections;

namespace FloorPlan_Generator
{

    public class PointCloudStructure
    {
        public PointCloudStructure()
        {
        }
        public PointCloudStructure(Point3d p, int n)
        {
            point = p;
            num = n;
        }
        Point3d point;
        int num = 0;
    }

    public class GHcTensorFields_Quad : GH_Component
    {
        public Point3d GridFirstPoint;
        public Point3d GridLastPoint;
        public int gridRes;
        public List<Vector3d> GridVectors;
        public double step;
        public double roadDensity;
        public double angle;
        public Point3dList pointCloud;
        public List<PointCloudStructure> pointCloudStructureList;
        Curve boundary;

        /// <summary>
        /// Initializes a new instance of the TensorFieldsGHc class.
        /// </summary>
        public GHcTensorFields_Quad()
          : base("Tensor Fields QUad", "Tensor Fields QUad",
              "Tensor Fields QUad",
              "FloorPlanGen", "Study_6")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "Points", "Points", GH_ParamAccess.list);
            pManager.AddVectorParameter("GridVectors", "GridVectors", "GridVectors", GH_ParamAccess.list);
            pManager.AddPointParameter("FirstPt", "FirstPt", "First point of GridVectors", GH_ParamAccess.item);
            pManager.AddPointParameter("LastPt", "LastPt", "Last point of GridVectors", GH_ParamAccess.item);
            pManager.AddCurveParameter("Boundary", "Boundary", "Boundary", GH_ParamAccess.item);
            //pManager.AddNumberParameter("RoadDensity", "RoadDensity", "RoadDensity", GH_ParamAccess.item);
            pManager.AddNumberParameter("RoadDensity", "RoadDensity", "RoadDensity", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("Angle", "Angle", "Angle", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("Iterations", "Iterations", "Iterations, usually it should be equal 3", GH_ParamAccess.item, 3);
            pManager.AddNumberParameter("Step", "Step", "Step", GH_ParamAccess.item, 10);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("RoadNetwork", "RoadNetwork", "RoadNetwork", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int iterations = 3;
            List<Point3d> iPoints = new List<Point3d>();
            GridVectors = new List<Vector3d>();
            roadDensity = 0;
            double blockWidth = 0;
            List<Curve> outputCurves = new List<Curve>();
            pointCloudStructureList = new List<PointCloudStructure>();

            DA.GetDataList(0, iPoints);
            DA.GetDataList(1, GridVectors);
            DA.GetData(2, ref GridFirstPoint);
            DA.GetData(3, ref GridLastPoint);
            DA.GetData(4, ref boundary);
            DA.GetData(5, ref roadDensity);
            DA.GetData(6, ref angle);
            DA.GetData(7, ref iterations);
            DA.GetData(8, ref step);

            // step = blockWidth;
            // roadDensity = step - 10;

            gridRes = (int)Math.Sqrt(GridVectors.Count) - 1;
            pointCloud = new Point3dList();

            int sign = 1;
            Vector3d prevTensor = new Vector3d(0, 1, 0);
            Point3d nextPt = new Point3d();
            Vector3d nextTensor = new Vector3d();
            List<Point3d> currPointList = new List<Point3d>();
            List<Point3d> prevPointList = new List<Point3d>();











            for (int i = 0; i < 1; i++)
            {/////////////////////////////////////////////////////
                List<Curve> currCurves = new List<Curve>();

                for (int j = 0; j < iPoints.Count; j++) // J ======= iPoints
                {
                    sign = 1;
                    currPointList.Clear();
                    prevPointList.Clear();

                    for (int k = 0; k < 2; k++)
                    {
                        prevPointList = new List<Point3d>(currPointList);
                        currPointList.Clear();
                        //  prevTensor = new Vector3d((i + 1) % 2, (i) % 2, 0);
                        prevTensor = new Vector3d(0, 0, 0);

                        if (GetTensor(iPoints[j], prevTensor) != Vector3d.Unset)
                        {
                            nextPt = iPoints[j] + sign * GetTensor(iPoints[j], prevTensor);

                            currPointList.Add(iPoints[j]);
                            prevTensor = GetTensor(iPoints[j], prevTensor);
                        }

                        int f = 0;
                        while (CheckPt(nextPt) && f < 40)
                        {
                            currPointList.Add(nextPt);
                            pointCloudStructureList.Add(new PointCloudStructure(nextPt, currPointList.Count - 1));
                            nextTensor = GetTensor(currPointList[currPointList.Count - 1], prevTensor);
                            nextPt = currPointList[currPointList.Count - 1] + sign * nextTensor;
                            f++;
                            prevTensor = nextTensor;
                        }


                        if (pointCloud.Count > 0)
                        {
                            Point3d pt = pointCloud[pointCloud.ClosestIndex(nextPt)];
                            // if ((pt.DistanceTo(nextPt) <= roadDensity) && f > 0)
                            currPointList.Add(pt);
                        }

                        outputCurves.Add(new PolylineCurve(currPointList));
                        currCurves.Add(new PolylineCurve(currPointList));
                        sign = -1;
                    }

                    pointCloud.AddRange(currPointList);
                    pointCloud.AddRange(prevPointList);
                }
              /*  iPoints.Clear();

                foreach (PolylineCurve curve in currCurves)
                {
                    if (curve != null && curve.GetLength() > 0.1)
                    {
                        // if (curve.DivideEquidistant(blockWidth) != null)
                        //   iPoints.AddRange(new List<Point3d>(curve.DivideEquidistant(blockWidth)));
                        List<Point3d> points = new List<Point3d>();
                        for (int q = 0; q < curve.PointCount; q++)
                            points.Add(curve.Point(q));
                        iPoints.AddRange(points);
                    }
                }
                */

                angle += 90;
            }//////////////////////////////////////////////

            Point3dList tempList1 = pointCloud;
            pointCloud.Clear();

            for (int i = 0; i < 1; i++)
            {/////////////////////////////////////////////////////
                List<Curve> currCurves = new List<Curve>();

                for (int j = 0; j < iPoints.Count; j++) // J ======= iPoints
                {
                    sign = 1;
                    currPointList.Clear();
                    prevPointList.Clear();

                    for (int k = 0; k < 2; k++)
                    {
                        prevPointList = new List<Point3d>(currPointList);
                        currPointList.Clear();
                        //  prevTensor = new Vector3d((i + 1) % 2, (i) % 2, 0);
                        prevTensor = new Vector3d(0, 0, 0);

                        if (GetTensor(iPoints[j], prevTensor) != Vector3d.Unset)
                        {
                            nextPt = iPoints[j] + sign * GetTensor(iPoints[j], prevTensor);

                            currPointList.Add(iPoints[j]);
                            prevTensor = GetTensor(iPoints[j], prevTensor);
                        }

                        int f = 0;
                        while (CheckPt(nextPt) && f < 40)
                        {
                            currPointList.Add(nextPt);
                            pointCloudStructureList.Add(new PointCloudStructure(nextPt, currPointList.Count - 1));
                            nextTensor = GetTensor(currPointList[currPointList.Count - 1], prevTensor);
                            nextPt = currPointList[currPointList.Count - 1] + sign * nextTensor;
                            f++;
                            prevTensor = nextTensor;
                        }


                        if (pointCloud.Count > 0)
                        {
                            Point3d pt = pointCloud[pointCloud.ClosestIndex(nextPt)];
                            // if ((pt.DistanceTo(nextPt) <= roadDensity) && f > 0)
                            currPointList.Add(pt);
                        }

                        outputCurves.Add(new PolylineCurve(currPointList));
                        currCurves.Add(new PolylineCurve(currPointList));
                        sign = -1;
                    }

                    pointCloud.AddRange(currPointList);
                    pointCloud.AddRange(prevPointList);
                }
                iPoints.Clear();

                foreach (PolylineCurve curve in currCurves)
                {
                    if (curve != null && curve.GetLength() > 0.1)
                    {
                        // if (curve.DivideEquidistant(blockWidth) != null)
                        //   iPoints.AddRange(new List<Point3d>(curve.DivideEquidistant(blockWidth)));
                        List<Point3d> points = new List<Point3d>();
                        for (int q = 0; q < curve.PointCount; q++)
                            points.Add(curve.Point(q));
                        iPoints.AddRange(points);
                    }
                }

                angle += 90;
            }//////////////////////////////////////////////


            Point3dList tempList2 = pointCloud;
            pointCloud.Clear();


            pointCloud.AddRange(tempList1);
            pointCloud.AddRange(tempList2);




            for (int i = 0; i < 2; i++)
            {/////////////////////////////////////////////////////
                List<Curve> currCurves = new List<Curve>();

                for (int j = 0; j < iPoints.Count; j++) // J ======= iPoints
                {
                    sign = 1;
                    currPointList.Clear();
                    prevPointList.Clear();

                    for (int k = 0; k < 2; k++)
                    {
                        prevPointList = new List<Point3d>(currPointList);
                        currPointList.Clear();
                        //  prevTensor = new Vector3d((i + 1) % 2, (i) % 2, 0);
                        prevTensor = new Vector3d(0, 0, 0);

                        if (GetTensor(iPoints[j], prevTensor) != Vector3d.Unset)
                        {
                            nextPt = iPoints[j] + sign * GetTensor(iPoints[j], prevTensor);

                            currPointList.Add(iPoints[j]);
                            prevTensor = GetTensor(iPoints[j], prevTensor);
                        }

                        int f = 0;
                        while (CheckPt(nextPt) && f < 40)
                        {
                            currPointList.Add(nextPt);
                            pointCloudStructureList.Add(new PointCloudStructure(nextPt, currPointList.Count - 1));
                            nextTensor = GetTensor(currPointList[currPointList.Count - 1], prevTensor);
                            nextPt = currPointList[currPointList.Count - 1] + sign * nextTensor;
                            f++;
                            prevTensor = nextTensor;
                        }


                        if (pointCloud.Count > 0)
                        {
                            Point3d pt = pointCloud[pointCloud.ClosestIndex(nextPt)];
                            // if ((pt.DistanceTo(nextPt) <= roadDensity) && f > 0)
                            currPointList.Add(pt);
                        }

                        outputCurves.Add(new PolylineCurve(currPointList));
                        currCurves.Add(new PolylineCurve(currPointList));
                        sign = -1;
                    }

                   // pointCloud.AddRange(currPointList);
                    //pointCloud.AddRange(prevPointList);
                }
                iPoints.Clear();

                PolylineCurve curve = currCurves[0] as PolylineCurve;
                //foreach (PolylineCurve curve in currCurves)
               // {
                    if (curve != null && curve.GetLength() > 0.1)
                    {
                        // if (curve.DivideEquidistant(blockWidth) != null)
                        //   iPoints.AddRange(new List<Point3d>(curve.DivideEquidistant(blockWidth)));
                        List<Point3d> points = new List<Point3d>();
                        for (int q = 0; q < curve.PointCount; q++)
                            points.Add(curve.Point(q));
                        iPoints.AddRange(points);
                    }
             //   }

                angle += 90;
            }//////////////////////////////////////////////

            


            DA.SetDataList(0, outputCurves);
        }


        public Vector3d GetTensor(Point3d point, Vector3d prevTensor)
        {
            Vector3d tensor = GetTensorInPoint(point);
            tensor.Rotate(Rhino.RhinoMath.ToRadians(angle), Vector3d.ZAxis);
            tensor.Unitize();
            prevTensor.Unitize();
            if (tensor != Vector3d.Unset)
            {
                if (Math.Abs(Vector3d.VectorAngle(tensor, prevTensor)) < Rhino.RhinoMath.ToRadians(90))
                    return (tensor * step);
                else
                    return (tensor * -step);
            }

            return Vector3d.Unset;
        }

        public Vector3d GetTensorInPoint(Point3d point)
        {
            return GridVectors[GetGridPointIndex(point)];
        }

        public int GetGridPointIndex(Point3d point)
        {
            int xI = (int)((point.X - GridFirstPoint.X) / (GridLastPoint.X - GridFirstPoint.X) * gridRes + 0.5);
            int yI = (int)((point.Y - GridFirstPoint.Y) / (GridLastPoint.Y - GridFirstPoint.Y) * gridRes + 0.5);
            int index = xI * (gridRes + 1) + yI;

            return Rhino.RhinoMath.Clamp(index, 0, (gridRes + 1) * (gridRes + 1) - 1);
        }

        public bool CheckPt(Point3d point)
        {
            if (pointCloud.Count > 0)
            {
                double t = pointCloud[pointCloud.ClosestIndex(point)].DistanceTo(point);
                if (t <= roadDensity && t > 0)
                    return false;
            }

           // if (boundary.Contains(point) != PointContainment.Inside)
            //    return false;


            return true;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        /// 
        /// 
        /// 
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{b02654b4-ffbc-41eb-a64a-13a6f2a7b8ed}"); }
        }
    }
}