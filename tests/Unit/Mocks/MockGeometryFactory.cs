using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace Magnetizing_FPG.Tests.Mocks
{
    /// <summary>
    /// Factory for creating mock Grasshopper/Rhino geometry objects for testing.
    /// This allows us to test the algorithm logic without requiring a full Rhino installation.
    /// </summary>
    public static class MockGeometryFactory
    {
        /// <summary>
        /// Creates a rectangular boundary curve for testing.
        /// </summary>
        /// <param name="width">Width of the rectangle</param>
        /// <param name="height">Height of the rectangle</param>
        /// <param name="center">Center point of the rectangle</param>
        /// <returns>Rectangular polyline curve</returns>
        public static PolylineCurve CreateRectangularBoundary(double width, double height, Point3d center = default)
        {
            if (center == default) center = Point3d.Origin;
            
            var points = new[]
            {
                new Point3d(center.X - width/2, center.Y - height/2, 0),
                new Point3d(center.X + width/2, center.Y - height/2, 0),
                new Point3d(center.X + width/2, center.Y + height/2, 0),
                new Point3d(center.X - width/2, center.Y + height/2, 0),
                new Point3d(center.X - width/2, center.Y - height/2, 0) // Close the curve
            };
            
            return new PolylineCurve(points);
        }

        /// <summary>
        /// Creates an L-shaped boundary for testing complex geometry.
        /// </summary>
        public static PolylineCurve CreateLShapedBoundary(double width, double height, double cutoutWidth, double cutoutHeight)
        {
            var points = new[]
            {
                new Point3d(0, 0, 0),
                new Point3d(width, 0, 0),
                new Point3d(width, height - cutoutHeight, 0),
                new Point3d(width - cutoutWidth, height - cutoutHeight, 0),
                new Point3d(width - cutoutWidth, height, 0),
                new Point3d(0, height, 0),
                new Point3d(0, 0, 0) // Close the curve
            };
            
            return new PolylineCurve(points);
        }

        /// <summary>
        /// Creates a room rectangle for testing.
        /// </summary>
        public static Rectangle3d CreateRoomRectangle(double width, double height, Point3d center = default)
        {
            if (center == default) center = Point3d.Origin;
            
            var plane = new Plane(center, Vector3d.ZAxis);
            return new Rectangle3d(plane, width, height);
        }

        /// <summary>
        /// Creates a test entrance point.
        /// </summary>
        public static Point3d CreateEntrancePoint(Point3d location = default)
        {
            return location == default ? new Point3d(0, 0, 0) : location;
        }

        /// <summary>
        /// Creates standard test boundaries for different scenarios.
        /// </summary>
        public static class StandardBoundaries
        {
            public static PolylineCurve SmallOffice => CreateRectangularBoundary(20, 15);
            public static PolylineCurve MediumOffice => CreateRectangularBoundary(40, 30);
            public static PolylineCurve LargeOffice => CreateRectangularBoundary(60, 45);
            public static PolylineCurve LShapeOffice => CreateLShapedBoundary(30, 25, 10, 10);
            public static PolylineCurve NarrowBuilding => CreateRectangularBoundary(50, 8);
            public static PolylineCurve SquareBuilding => CreateRectangularBoundary(25, 25);
        }

        /// <summary>
        /// Creates collections of rooms for testing different scenarios.
        /// </summary>
        public static class StandardRoomPrograms
        {
            /// <summary>
            /// Simple 3-room program: entrance + 2 offices.
            /// </summary>
            public static List<TestRoomData> SimpleOffice => new List<TestRoomData>
            {
                new TestRoomData { Id = 1, Name = "Entrance", Area = 15, IsHall = true },
                new TestRoomData { Id = 2, Name = "Office 1", Area = 25, IsHall = false },
                new TestRoomData { Id = 3, Name = "Office 2", Area = 20, IsHall = false }
            };

            /// <summary>
            /// Medium complexity: 6 rooms with various adjacencies.
            /// </summary>
            public static List<TestRoomData> MediumOffice => new List<TestRoomData>
            {
                new TestRoomData { Id = 1, Name = "Lobby", Area = 30, IsHall = true },
                new TestRoomData { Id = 2, Name = "Reception", Area = 15, IsHall = false },
                new TestRoomData { Id = 3, Name = "Conference Room", Area = 40, IsHall = false },
                new TestRoomData { Id = 4, Name = "Office 1", Area = 25, IsHall = false },
                new TestRoomData { Id = 5, Name = "Office 2", Area = 25, IsHall = false },
                new TestRoomData { Id = 6, Name = "Storage", Area = 10, IsHall = false }
            };

            /// <summary>
            /// Large program for stress testing.
            /// </summary>
            public static List<TestRoomData> LargeOffice => new List<TestRoomData>
            {
                new TestRoomData { Id = 1, Name = "Main Lobby", Area = 50, IsHall = true },
                new TestRoomData { Id = 2, Name = "Reception", Area = 20, IsHall = false },
                new TestRoomData { Id = 3, Name = "Waiting Area", Area = 25, IsHall = false },
                new TestRoomData { Id = 4, Name = "Conference Room A", Area = 35, IsHall = false },
                new TestRoomData { Id = 5, Name = "Conference Room B", Area = 30, IsHall = false },
                new TestRoomData { Id = 6, Name = "Office 1", Area = 20, IsHall = false },
                new TestRoomData { Id = 7, Name = "Office 2", Area = 20, IsHall = false },
                new TestRoomData { Id = 8, Name = "Office 3", Area = 25, IsHall = false },
                new TestRoomData { Id = 9, Name = "Office 4", Area = 25, IsHall = false },
                new TestRoomData { Id = 10, Name = "Storage", Area = 15, IsHall = false },
                new TestRoomData { Id = 11, Name = "Server Room", Area = 12, IsHall = false },
                new TestRoomData { Id = 12, Name = "Break Room", Area = 18, IsHall = false }
            };
        }

        /// <summary>
        /// Standard adjacency patterns for testing.
        /// </summary>
        public static class StandardAdjacencies
        {
            /// <summary>
            /// Simple linear adjacency: 1-2-3.
            /// </summary>
            public static List<string> Linear => new List<string> { "1-2", "2-3" };

            /// <summary>
            /// Hub pattern: central room connected to all others.
            /// </summary>
            public static List<string> HubPattern => new List<string> { "1-2", "1-3", "1-4", "1-5", "1-6" };

            /// <summary>
            /// Complex adjacency network for testing sophisticated placement.
            /// </summary>
            public static List<string> ComplexNetwork => new List<string> 
            { 
                "1-2", "1-3", "2-4", "2-5", "3-6", "4-7", "5-7", "6-8", "7-8", "8-9"
            };
        }
    }

    /// <summary>
    /// Test data structure for room information.
    /// </summary>
    public class TestRoomData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Area { get; set; }
        public bool IsHall { get; set; }
        public List<int> AdjacentRoomIds { get; set; } = new List<int>();
    }
}