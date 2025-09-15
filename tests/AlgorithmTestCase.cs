using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Magnetizing_FPG;

namespace FloorPlanGeneratorTests
{
    /// <summary>
    /// Represents a complete test case for the floor plan algorithm
    /// </summary>
    public class AlgorithmTestCase
    {
        public string TestName { get; set; }
        public string Description { get; set; }
        public int RandomSeed { get; set; }
        
        // Input parameters
        public TestHouseInstance HouseInstance { get; set; }
        public int Iterations { get; set; } = 100;
        public double MaxAdjDistance { get; set; } = 2.0;
        public double CellSize { get; set; } = 1.0;
        
        // Expected outputs (for validation)
        public int ExpectedRoomCount { get; set; }
        public List<string> ExpectedRoomNames { get; set; } = new List<string>();
        public double ExpectedTotalArea { get; set; }
        public bool ExpectCorridors { get; set; } = true;
        
        // Test metadata
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = "AlgorithmTestFramework";
    }

    /// <summary>
    /// Simplified house instance for testing (without Grasshopper dependencies)
    /// </summary>
    public class TestHouseInstance
    {
        public string HouseName { get; set; } = "Test House";
        public string FloorName { get; set; } = "Ground Floor";
        public List<TestRoomInstance> Rooms { get; set; } = new List<TestRoomInstance>();
        public List<string> Adjacencies { get; set; } = new List<string>(); // "1-2", "2-3" format
        public TestBoundary Boundary { get; set; }
        public TestPoint3d StartingPoint { get; set; }
        public bool TryRotateBoundary { get; set; } = false;
    }

    /// <summary>
    /// Simplified room instance for testing
    /// </summary>
    public class TestRoomInstance
    {
        public string RoomName { get; set; }
        public double RoomArea { get; set; }
        public bool IsHall { get; set; } = false;
        public bool IsEntrance { get; set; } = false;
    }

    /// <summary>
    /// Simplified boundary for testing (can be converted to Rhino.Geometry.Curve)
    /// </summary>
    public class TestBoundary
    {
        public List<TestPoint3d> Points { get; set; } = new List<TestPoint3d>();
        
        // Helper method to create rectangle
        public static TestBoundary CreateRectangle(double x, double y, double width, double height)
        {
            return new TestBoundary
            {
                Points = new List<TestPoint3d>
                {
                    new TestPoint3d(x, y, 0),
                    new TestPoint3d(x + width, y, 0),
                    new TestPoint3d(x + width, y + height, 0),
                    new TestPoint3d(x, y + height, 0),
                    new TestPoint3d(x, y, 0) // Close the rectangle
                }
            };
        }
        
        // Convert to Rhino Curve (when geometry is available)
        public Curve ToRhinoCurve()
        {
            var rhinoPoints = new List<Point3d>();
            foreach (var point in Points)
            {
                rhinoPoints.Add(new Point3d(point.X, point.Y, point.Z));
            }
            return Curve.CreateControlPointCurve(rhinoPoints, 1);
        }
    }

    /// <summary>
    /// Simplified Point3d for testing (serializable)
    /// </summary>
    public class TestPoint3d
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public TestPoint3d() { }
        
        public TestPoint3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Point3d ToRhinoPoint3d()
        {
            return new Point3d(X, Y, Z);
        }
    }
}