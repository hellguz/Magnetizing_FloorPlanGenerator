using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Magnetizing_FPG.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Magnetizing_FPG.Examples.BasicUsage
{
    /// <summary>
    /// Basic usage example showing how to use the Magnetizing Floor Plan Generator
    /// as a standalone library without Grasshopper.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Magnetizing Floor Plan Generator - Basic Usage Example");
            Console.WriteLine("=====================================================");
            
            // Setup logging
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            // TODO: Add service registrations once we have the core services implemented
            // services.AddMagnetizingFloorPlanGenerator();
            
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            
            try
            {
                // Create a simple floor plan request
                var request = CreateSimpleOfficeRequest();
                
                logger.LogInformation("Created floor plan request: {Request}", request);
                
                // TODO: Once we implement the algorithm services, this will actually work:
                // var algorithm = serviceProvider.GetRequiredService<IMagnetizingAlgorithm>();
                // var result = await algorithm.GenerateFloorPlan(request);
                
                // For now, just demonstrate the domain model
                Console.WriteLine("Floor plan request created successfully!");
                Console.WriteLine($"  - {request.Rooms.Count} rooms");
                Console.WriteLine($"  - {request.Adjacencies.Count} adjacency requirements");
                Console.WriteLine($"  - Total room area: {request.TotalRoomArea:F1}m²");
                Console.WriteLine($"  - Boundary area: {request.Boundary.Area:F1}m²");
                
                // Validate the request
                var validation = request.Validate();
                if (validation.IsValid)
                {
                    Console.WriteLine("✓ Request validation passed");
                }
                else
                {
                    Console.WriteLine("✗ Request validation failed:");
                    Console.WriteLine(validation.GetDetailedReport());
                }
                
                // TODO: Remove this once we have the actual algorithm
                Console.WriteLine("\nNOTE: Algorithm implementation is not yet complete.");
                Console.WriteLine("This example demonstrates the domain model and validation.");
                Console.WriteLine("The actual floor plan generation will be available after refactoring Phase 2.");
                
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during floor plan generation");
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        
        /// <summary>
        /// Creates a simple office floor plan request for demonstration.
        /// </summary>
        private static FloorPlanRequest CreateSimpleOfficeRequest()
        {
            // Create a rectangular boundary (20m x 15m)
            var boundary = new Boundary
            {
                Name = "Simple Office Building",
                Points = new List<Point2d>
                {
                    new Point2d(0, 0),
                    new Point2d(20, 0),
                    new Point2d(20, 15),
                    new Point2d(0, 15),
                    new Point2d(0, 0) // Close the polygon
                },
                IsClosed = true
            };
            
            // Create rooms
            var rooms = new List<Room>
            {
                new Room
                {
                    Id = 1,
                    Name = "Entrance Lobby",
                    Area = 20,
                    IsHall = true,
                    PlacementPriority = 10 // Higher priority for entrance
                },
                new Room
                {
                    Id = 2,
                    Name = "Conference Room",
                    Area = 40,
                    IsHall = false,
                    MinWidth = 4.0
                },
                new Room
                {
                    Id = 3,
                    Name = "Office 1",
                    Area = 25,
                    IsHall = false,
                    Properties = { ["Type"] = "Private Office" }
                },
                new Room
                {
                    Id = 4,
                    Name = "Office 2", 
                    Area = 25,
                    IsHall = false,
                    Properties = { ["Type"] = "Private Office" }
                },
                new Room
                {
                    Id = 5,
                    Name = "Storage",
                    Area = 15,
                    IsHall = false,
                    MaxAspectRatio = 3.0 // Allow longer, narrow storage
                }
            };
            
            // Define adjacency requirements
            var adjacencies = new List<AdjacencyConstraint>
            {
                new AdjacencyConstraint { Room1Id = 1, Room2Id = 2, Type = AdjacencyType.Required },
                new AdjacencyConstraint { Room1Id = 1, Room2Id = 3, Type = AdjacencyType.Required },
                new AdjacencyConstraint { Room1Id = 1, Room2Id = 4, Type = AdjacencyType.Required },
                new AdjacencyConstraint { Room1Id = 2, Room2Id = 3, Type = AdjacencyType.Preferred },
                new AdjacencyConstraint { Room1Id = 3, Room2Id = 4, Type = AdjacencyType.Preferred }
            };
            
            // Configure algorithm
            var configuration = new AlgorithmConfiguration
            {
                MaxIterations = 200,
                CellSize = 0.5, // Fine grid for small building
                CorridorMode = CorridorMode.TwoSides,
                RemoveDeadEnds = true,
                RandomSeed = 12345 // For reproducible results
            };
            
            // Create the request
            var request = new FloorPlanRequest
            {
                Boundary = boundary,
                EntrancePoint = new Point2d(10, 0), // Center of south wall
                Rooms = rooms,
                Adjacencies = adjacencies,
                Configuration = configuration,
                Metadata = 
                {
                    ["BuildingType"] = "Small Office",
                    ["Client"] = "Example Corp",
                    ["Created"] = DateTime.UtcNow
                }
            };
            
            return request;
        }
    }
}