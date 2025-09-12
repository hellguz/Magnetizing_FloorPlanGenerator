# Basic Usage Example

This example demonstrates how to use the Magnetizing Floor Plan Generator as a standalone .NET library, independent of Grasshopper.

## Overview

This console application shows:
- Creating domain objects (Boundary, Room, FloorPlanRequest)
- Configuring algorithm parameters
- Setting up adjacency constraints
- Validating input data
- Using the core library without Grasshopper dependencies

## Running the Example

### Prerequisites
- .NET Framework 4.8 or later
- Visual Studio 2019+ or Visual Studio Code

### Build and Run
```bash
# From the solution root directory
dotnet build examples/BasicUsage/Magnetizing_FPG.Examples.BasicUsage.csproj
dotnet run --project examples/BasicUsage/Magnetizing_FPG.Examples.BasicUsage.csproj
```

Or from Visual Studio:
1. Set `Magnetizing_FPG.Examples.BasicUsage` as startup project
2. Press F5 to run

## What the Example Demonstrates

### 1. Domain Model Usage
```csharp
// Create a building boundary
var boundary = new Boundary
{
    Name = "Simple Office Building",
    Points = new List<Point2d>
    {
        new Point2d(0, 0),
        new Point2d(20, 0),
        new Point2d(20, 15),
        new Point2d(0, 15),
        new Point2d(0, 0)
    },
    IsClosed = true
};

// Create rooms with different properties
var rooms = new List<Room>
{
    new Room
    {
        Id = 1,
        Name = "Entrance Lobby",
        Area = 20,
        IsHall = true,
        PlacementPriority = 10
    },
    // ... more rooms
};
```

### 2. Adjacency Constraints
```csharp
var adjacencies = new List<AdjacencyConstraint>
{
    new AdjacencyConstraint 
    { 
        Room1Id = 1, 
        Room2Id = 2, 
        Type = AdjacencyType.Required 
    },
    new AdjacencyConstraint 
    { 
        Room1Id = 2, 
        Room2Id = 3, 
        Type = AdjacencyType.Preferred 
    }
};
```

### 3. Algorithm Configuration
```csharp
var configuration = new AlgorithmConfiguration
{
    MaxIterations = 200,
    CellSize = 0.5,
    CorridorMode = CorridorMode.TwoSides,
    RemoveDeadEnds = true,
    RandomSeed = 12345 // For reproducible results
};
```

### 4. Input Validation
```csharp
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
```

## Example Output

```
Magnetizing Floor Plan Generator - Basic Usage Example
=====================================================
Floor plan request created successfully!
  - 5 rooms
  - 5 adjacency requirements
  - Total room area: 125.0m²
  - Boundary area: 300.0m²
✓ Request validation passed

NOTE: Algorithm implementation is not yet complete.
This example demonstrates the domain model and validation.
The actual floor plan generation will be available after refactoring Phase 2.

Press any key to exit...
```

## Next Steps

Once the refactoring is complete (Phase 2-3), this example will be updated to:

1. **Actually generate floor plans**:
```csharp
var algorithm = serviceProvider.GetRequiredService<IMagnetizingAlgorithm>();
var result = await algorithm.GenerateFloorPlan(request);

if (result.IsSuccess)
{
    Console.WriteLine($"✓ Floor plan generated successfully!");
    Console.WriteLine($"  - {result.FloorPlan.Rooms.Count} rooms placed");
    Console.WriteLine($"  - {result.FloorPlan.Corridors.Count} corridors generated");
    Console.WriteLine($"  - Space efficiency: {result.FloorPlan.UtilizationEfficiency:P1}");
}
```

2. **Export results to various formats**:
```csharp
// Export to JSON for analysis
var jsonExporter = serviceProvider.GetRequiredService<IFloorPlanExporter>();
await jsonExporter.ExportToJson(result.FloorPlan, "output/floorplan.json");

// Export to DXF for CAD
await jsonExporter.ExportToDxf(result.FloorPlan, "output/floorplan.dxf");
```

3. **Performance monitoring**:
```csharp
var stopwatch = Stopwatch.StartNew();
var result = await algorithm.GenerateFloorPlan(request);
stopwatch.Stop();

Console.WriteLine($"Generation completed in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Memory used: {result.FloorPlan.Metrics.MemoryUsed / 1024 / 1024:F1}MB");
```

## Related Examples

- **Advanced Usage**: Complex multi-story buildings with custom constraints
- **Performance Testing**: Benchmarking different algorithm configurations  
- **Custom Algorithms**: Implementing custom placement strategies
- **Integration**: Using the library in web applications or desktop tools

## Troubleshooting

### Common Issues

1. **Validation Errors**: Check that room areas don't exceed boundary area
2. **Missing References**: Ensure all project references are correctly set up
3. **Version Conflicts**: Make sure all packages use compatible versions

### Getting Help

- Check the main README.md for setup instructions
- Review the API documentation in docs/API_REFERENCE.md
- Look at the technical analysis in docs/TECHNICAL_ANALYSIS.md
- Submit issues on GitHub with the "example" label