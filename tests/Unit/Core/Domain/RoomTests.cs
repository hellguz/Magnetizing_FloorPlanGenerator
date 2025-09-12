using System;
using FluentAssertions;
using Magnetizing_FPG.Core.Domain;
using Magnetizing_FPG.Tests.Mocks;
using Magnetizing_FPG.Tests.Unit;
using Xunit;
using Xunit.Abstractions;

namespace Magnetizing_FPG.Tests.Unit.Core.Domain
{
    /// <summary>
    /// Unit tests for the Room domain model.
    /// These tests validate the core business logic without any external dependencies.
    /// </summary>
    public class RoomTests : TestBase
    {
        public RoomTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Room_DefaultConstruction_ShouldHaveValidDefaults()
        {
            // Act
            var room = new Room();

            // Assert
            room.Id.Should().Be(0);
            room.Name.Should().BeEmpty();
            room.Area.Should().Be(0);
            room.IsHall.Should().BeFalse();
            room.Position.Should().Be(Point2d.Origin);
            room.Dimensions.Should().Be(Size2d.Empty);
            room.AdjacentRoomIds.Should().NotBeNull().And.BeEmpty();
            room.HasMissingAdjacencies.Should().BeFalse();
            room.MinWidth.Should().Be(2.0);
            room.MaxAspectRatio.Should().Be(2.0);
            room.PlacementPriority.Should().Be(0);
            room.Properties.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void Room_WithValidDimensions_ShouldCalculateHeightCorrectly()
        {
            // Arrange
            var room = new Room
            {
                Area = 20,
                Dimensions = new Size2d(5, 0) // Height will be calculated
            };

            // Act
            var height = room.Height;

            // Assert
            height.Should().Be(4.0); // 20 / 5 = 4
        }

        [Fact]
        public void Room_WithValidDimensions_ShouldCalculateAspectRatioCorrectly()
        {
            // Arrange
            var room = new Room
            {
                Dimensions = new Size2d(6, 3)
            };

            // Act
            var aspectRatio = room.AspectRatio;

            // Assert
            aspectRatio.Should().Be(2.0); // max(6/3, 3/6) = max(2, 0.5) = 2
        }

        [Fact]
        public void Room_WithValidPositionAndDimensions_ShouldCalculateBoundingRectangleCorrectly()
        {
            // Arrange
            var room = new Room
            {
                Position = new Point2d(10, 15),
                Dimensions = new Size2d(8, 6)
            };

            // Act
            var bounds = room.BoundingRectangle;

            // Assert
            bounds.Left.Should().Be(6);   // 10 - 8/2
            bounds.Right.Should().Be(14); // 10 + 8/2
            bounds.Bottom.Should().Be(12); // 15 - 6/2
            bounds.Top.Should().Be(18);   // 15 + 6/2
        }

        [Theory]
        [InlineData(0, "Room area must be greater than zero")]
        [InlineData(-5, "Room area must be greater than zero")]
        public void Room_WithInvalidArea_ShouldFailValidation(double area, string expectedError)
        {
            // Arrange
            var room = new Room { Area = area };

            // Act
            var result = room.Validate();

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Message.Should().Be(expectedError);
        }

        [Fact]
        public void Room_WithWidthBelowMinimum_ShouldFailValidation()
        {
            // Arrange
            var room = new Room
            {
                Area = 10,
                Dimensions = new Size2d(1.5, 6.67), // Width below minimum of 2.0
                MinWidth = 2.0
            };

            // Act
            var result = room.Validate();

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Message.Should().Contain("Room width (1.50m) is less than minimum (2.00m)");
        }

        [Fact]
        public void Room_WithHighAspectRatio_ShouldGenerateWarning()
        {
            // Arrange
            var room = new Room
            {
                Name = "Test Room", // Add name to avoid name warning
                Area = 20,
                Dimensions = new Size2d(10, 2), // Aspect ratio = 5 > MaxAspectRatio = 2
                MaxAspectRatio = 2.0
            };

            // Act
            var result = room.Validate();

            // Assert
            result.IsValid.Should().BeTrue(); // Warnings don't make it invalid
            result.Warnings.Should().ContainSingle()
                .Which.Message.Should().Contain("Room aspect ratio (5.00) exceeds maximum (2.00)");
        }

        [Fact]
        public void Room_Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new Room
            {
                Id = 1,
                Name = "Office",
                Area = 25,
                IsHall = false,
                Position = new Point2d(5, 10),
                Dimensions = new Size2d(5, 5),
                AdjacentRoomIds = { 2, 3 },
                Properties = { ["Type"] = "Private Office" }
            };

            // Act
            var clone = original.Clone();

            // Assert
            clone.Should().NotBeSameAs(original);
            clone.Id.Should().Be(original.Id);
            clone.Name.Should().Be(original.Name);
            clone.Area.Should().Be(original.Area);
            clone.AdjacentRoomIds.Should().BeEquivalentTo(original.AdjacentRoomIds);
            clone.AdjacentRoomIds.Should().NotBeSameAs(original.AdjacentRoomIds);
            clone.Properties.Should().BeEquivalentTo(original.Properties);
            clone.Properties.Should().NotBeSameAs(original.Properties);
        }

        [Fact]
        public void Room_OverlapsWith_ShouldDetectOverlappingRooms()
        {
            // Arrange
            var room1 = new Room
            {
                Position = new Point2d(5, 5),
                Dimensions = new Size2d(4, 4) // Bounds: [3,3] to [7,7]
            };

            var room2 = new Room
            {
                Position = new Point2d(7, 7),
                Dimensions = new Size2d(4, 4) // Bounds: [5,5] to [9,9]
            };

            var room3 = new Room
            {
                Position = new Point2d(10, 10),
                Dimensions = new Size2d(2, 2) // Bounds: [9,9] to [11,11] - no overlap
            };

            // Act & Assert
            room1.OverlapsWith(room2).Should().BeTrue();  // Overlap at [5,5] to [7,7]
            room1.OverlapsWith(room3).Should().BeFalse(); // No overlap
            room2.OverlapsWith(room3).Should().BeFalse(); // No overlap
        }

        [Fact]
        public void Room_DistanceTo_ShouldCalculateCenterToCenterDistance()
        {
            // Arrange
            var room1 = new Room { Position = new Point2d(0, 0) };
            var room2 = new Room { Position = new Point2d(3, 4) };

            // Act
            var distance = room1.DistanceTo(room2);

            // Assert
            distance.Should().Be(5.0); // 3-4-5 triangle
        }

        [Fact]
        public void Room_EdgeDistanceTo_ShouldCalculateMinimumEdgeDistance()
        {
            // Arrange
            var room1 = new Room
            {
                Position = new Point2d(0, 0),
                Dimensions = new Size2d(2, 2) // Bounds: [-1,-1] to [1,1]
            };

            var room2 = new Room
            {
                Position = new Point2d(4, 0),
                Dimensions = new Size2d(2, 2) // Bounds: [3,-1] to [5,1]
            };

            // Act
            var edgeDistance = room1.EdgeDistanceTo(room2);

            // Assert
            edgeDistance.Should().Be(2.0); // Distance from x=1 to x=3
        }

        [Fact]
        public void Room_EdgeDistanceTo_OverlappingRooms_ShouldReturnNegativeDistance()
        {
            // Arrange
            var room1 = new Room
            {
                Position = new Point2d(0, 0),
                Dimensions = new Size2d(4, 4) // Bounds: [-2,-2] to [2,2]
            };

            var room2 = new Room
            {
                Position = new Point2d(1, 1),
                Dimensions = new Size2d(2, 2) // Bounds: [0,0] to [2,2]
            };

            // Act
            var edgeDistance = room1.EdgeDistanceTo(room2);

            // Assert
            edgeDistance.Should().BeLessThan(0); // Negative indicates overlap
        }

        [Fact]
        public void Room_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var room = new Room { Id = 5, Name = "Conference Room", Area = 35.5 };

            // Act
            var result = room.ToString();

            // Assert
            result.Should().Be("Room 5: Conference Room (35.5m²)");
        }

        [Fact]
        public void Room_Equals_ShouldCompareById()
        {
            // Arrange
            var room1 = new Room { Id = 1, Name = "Room A" };
            var room2 = new Room { Id = 1, Name = "Room B" };
            var room3 = new Room { Id = 2, Name = "Room A" };

            // Act & Assert
            room1.Equals(room2).Should().BeTrue();  // Same ID
            room1.Equals(room3).Should().BeFalse(); // Different ID
            room1.GetHashCode().Should().Be(room2.GetHashCode()); // Same hash for same ID
        }

        [Fact]
        public void Room_WithTestData_ShouldWorkWithMockGeometryFactory()
        {
            // Arrange
            var testRoomData = MockGeometryFactory.StandardRoomPrograms.SimpleOffice[0];

            // Act
            var room = new Room
            {
                Id = testRoomData.Id,
                Name = testRoomData.Name,
                Area = testRoomData.Area,
                IsHall = testRoomData.IsHall
            };

            // Assert
            room.Id.Should().Be(1);
            room.Name.Should().Be("Entrance");
            room.Area.Should().Be(15);
            room.IsHall.Should().BeTrue();
        }
    }
}