# API Reference - Magnetizing Floor Plan Generator

## Overview

This document provides comprehensive API reference for the Magnetizing Floor Plan Generator components, interfaces, and data structures.

## Core Interfaces

### IHouseInstance

Represents a building instance with rooms and spatial requirements.

```csharp
public interface IHouseInstance
{
    /// <summary>
    /// The building boundary curve.
    /// </summary>
    Curve boundary { get; }

    /// <summary>
    /// The entrance point where the first room should be placed.
    /// </summary>
    Point3d startingPoint { get; }

    /// <summary>
    /// Whether the algorithm should try rotating the boundary for optimal packing.
    /// </summary>
    bool tryRotateBoundary { get; }

    /// <summary>
    /// Collection of room instances belonging to this house.
    /// </summary>
    List<IRoomInstance> RoomInstances { get; }

    /// <summary>
    /// Adjacency relationships as string pairs (e.g., "1-2", "3-4").
    /// </summary>
    List<string> adjStrList { get; }

    /// <summary>
    /// Adjacency matrix representation [n,2] where n is number of adjacencies.
    /// </summary>
    int[,] adjArray { get; set; }
}
```

### IRoomInstance

Represents an individual room with its properties and adjacency requirements.

```csharp
public interface IRoomInstance
{
    /// <summary>
    /// Unique identifier for this room instance.
    /// </summary>
    int RoomId { get; set; }

    /// <summary>
    /// Required room area in square meters.
    /// </summary>
    double RoomArea { get; set; }

    /// <summary>
    /// Human-readable room name or description.
    /// </summary>
    string RoomName { get; set; }

    /// <summary>
    /// Whether this room functions as a circulation hall/corridor.
    /// </summary>
    bool isHall { get; set; }

    /// <summary>
    /// Rooms that should be adjacent to this room.
    /// </summary>
    List<IRoomInstance> AdjacentRoomsList { get; }

    /// <summary>
    /// Indicates if this room has unmet adjacency requirements.
    /// </summary>
    bool hasMissingAdj { get; set; }
}
```

## Grasshopper Components

### HouseInstance Component

Creates a house container for room instances with GUI-based adjacency definition.

**Category**: Magnetizing_FPG  
**Subcategory**: Magnetizing_FPG

#### Inputs
| Name | Type | Description | Access |
|------|------|-------------|--------|
| Boundary | Curve | Building boundary curve | Item |
| Entrance Point | Point3d | Entrance location | Item |

#### Outputs
| Name | Type | Description | Access |
|------|------|-------------|--------|
| HouseInstance | Generic | House instance object | Item |

#### Menu Options
- **Try Rotate Boundary**: Optimizes boundary orientation for room packing

### HouseInstanceAdvanced Component

Advanced house instance with text-based room and adjacency definition.

**Category**: Magnetizing_FPG  
**Subcategory**: Magnetizing_FPG

#### Inputs
| Name | Type | Description | Access | Default |
|------|------|-------------|--------|---------|
| Boundary | Curve | Building boundary curve | Item | - |
| Entrance Point | Point3d | Entrance location | Item | - |
| Room Areas | Number | Room areas in m² | List | [40.0] |
| Room Names | Text | Room names | List | ["Room 1", ...] |
| Is Hall | Boolean | Hall flags for each room | List | [false, ...] |
| Adjacency | Text | Adjacency pairs like "1-2" | List | [] |
| Rotate Boundary | Boolean | Try boundary rotation | Item | false |

#### Outputs
| Name | Type | Description | Access |
|------|------|-------------|--------|
| HouseInstance | Generic | House instance object | Item |

### MagnetizingRooms_ES Component

Main magnetizing algorithm for floor plan generation.

**Category**: Magnetizing_FPG  
**Subcategory**: Magnetizing_FPG

#### Inputs
| Name | Type | Description | Access | Default |
|------|------|-------------|--------|---------|
| House Instance | Generic | House instance with room program | Item | - |
| Iterations | Integer | Number of optimization iterations | Item | 3 |
| MaxAdjDistance | Number | Maximum distance for adjacency satisfaction | Item | 2 |
| CellSize(m) | Number | Grid resolution in meters | Item | 1 |

#### Outputs
| Name | Type | Description | Access |
|------|------|-------------|--------|
| Corridors | Brep | Generated corridor geometry | Item |
| Room Breps | Brep | Individual room geometries | List |
| Room Names | Text | Names of successfully placed rooms | List |
| Adjacencies | Text | Achieved adjacency relationships | Item |
| MissingAdjacences | Integer | Count of unmet adjacencies per room | List |
| Boundary | Curve | Input boundary | Item |
| Boundary+Offset | Curve | Boundary with offset applied | Item |

#### Menu Options
- **Corridor Generation Modes**:
  - One-side corridors: Corridors on one side of rooms
  - Two-sides corridors: Corridors on two sides of rooms  
  - All-sides corridors: Corridors surrounding rooms
- **Post-Processing Options**:
  - Remove Dead Ends: Eliminates unnecessary corridor branches
  - Remove All Corridors: Assigns corridor space to adjacent rooms
- **Corridors as additional spaces**: Whether corridors add to or replace room area
- **Boundary offset**: Configurable boundary offset value

### SpringSystem_ES Component

Physics-based room optimization using evolutionary strategies.

**Category**: Magnetizing_FPG  
**Subcategory**: Magnetizing_FPG

#### Inputs
| Name | Type | Description | Access | Default |
|------|------|-------------|--------|---------|
| Boundary | Curve | Building boundary | Item | - |
| Rooms | Curve | Room geometries to optimize | List | - |
| Adjacencies | Text | Adjacency requirements | List | [" - "] |
| ProportionThreshold | Number | Maximum room aspect ratio | Item | 2 |
| FF Balance | Number | Fitness function balance [0,1] | Item | 0 |
| SpringCollAllGenes | Boolean | Apply physics to all solutions | Item | true |
| AdjustArea | Boolean | Scale rooms to fit boundary | Item | false |
| Reset | Boolean | Reset optimization state | Item | false |

#### Outputs
| Name | Type | Description | Access |
|------|------|-------------|--------|
| Rooms | Curve | Optimized room geometries | List |
| Adjacencies | Line | Adjacency connection lines | List |

#### Menu Options
- **Shuffle rooms at first**: Randomize initial room positions

### RoomInstance Component

Individual room definition with interactive adjacency connections.

**Category**: Magnetizing_FPG  
**Subcategory**: Magnetizing_FPG

#### Inputs
None (configured via UI interaction)

#### Outputs
| Name | Type | Description | Access |
|------|------|-------------|--------|
| a | Text | Room name output | Item |

#### Properties
- **RoomArea**: Room area in square meters (default: 40)
- **RoomName**: Display name for the room
- **RoomId**: Unique integer identifier (auto-assigned)
- **isHall**: Whether room functions as circulation space

#### Menu Options
- **Set as Entrance**: Designates room as building entrance
- **Set as Hall**: Marks room as circulation/corridor space

#### Interactive Features
- **Drag connections**: Create adjacency relationships by dragging between room instances
- **Visual feedback**: Color-coded rooms and connection arrows
- **Double-click editing**: Edit room name and area directly on canvas

## Data Structures

### GridSolution (Internal)

Represents a complete solution state during the magnetizing algorithm.

```csharp
internal class GridSolution
{
    public int[,] grid;                          // Grid state with room assignments
    public List<RoomCells> roomCellsList;        // Room positions and dimensions
    public List<int> placedRoomsOrderedList;     // Placement order of rooms
}
```

### RoomCells (Internal)

Defines room position and dimensions in grid coordinates.

```csharp
internal class RoomCells
{
    public int x;        // Grid X origin
    public int y;        // Grid Y origin  
    public int w;        // Width in grid cells
    public int h;        // Height in grid cells
}
```

### Gene (SpringSystem)

Represents a solution candidate in the evolutionary algorithm.

```csharp
public class Gene
{
    public List<Room> collection;                // Room configurations
    public static int[,] adjacencyList;          // Adjacency requirements
    public static double proportionThreshold;    // Room proportion limits
    public static double fitnessFunctionBalance; // Optimization balance factor
    
    public double FitnessFunction();             // Overall solution quality
    public Gene Clone();                         // Create solution copy
    public void MutateSomehow();                // Apply random mutations
}
```

### Room (SpringSystem)

Represents a room in the evolutionary optimization process.

```csharp
public class Room
{
    public double CenterX;    // Room center X coordinate
    public double CenterY;    // Room center Y coordinate
    public double width;      // Room width
    public double area;       // Fixed room area
    public double height;     // Calculated height (area/width)
    
    public Room Clone();      // Create room copy
}
```

## Algorithm Parameters

### Magnetizing Algorithm Configuration

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| Iterations | int | 1-2000 | 3 | Number of optimization iterations |
| MaxAdjDistance | double | 0.5-10 | 2 | Maximum distance for adjacency satisfaction |
| CellSize | double | 0.1-5 | 1 | Grid resolution in meters |
| MaxRatio | const double | - | 1.9 | Maximum allowed room aspect ratio |
| BoundaryOffset | double | 0-10 | 2.3 | Boundary expansion for room placement |

### Spring System Configuration

| Parameter | Type | Range | Default | Description |
|-----------|------|-------|---------|-------------|
| ProportionThreshold | double | 1.0-5.0 | 2.0 | Maximum room aspect ratio |
| FF Balance | double | 0.0-1.0 | 0.5 | Balance between area and adjacency fitness |
| Population Size | int | 5-50 | 15 | Number of solution candidates |
| Mutation Probability | double | 0.1-0.8 | 0.3 | Probability of gene mutation |
| Mutation Strength | double | 0.05-0.5 | 0.2 | Magnitude of mutations |

## Error Handling

### Common Exceptions

#### InvalidBoundaryException
Thrown when the provided boundary curve is invalid or self-intersecting.

#### InsufficientSpaceException  
Thrown when room areas exceed available space within the boundary.

#### AdjacencyConflictException
Thrown when adjacency requirements cannot be satisfied simultaneously.

#### GridSizeException
Thrown when grid dimensions are too small or too large for processing.

### Error States

| Component | Error Message | Cause | Solution |
|-----------|---------------|-------|----------|
| MagnetizingRooms_ES | "Der Objektverweis wurde nicht auf eine Objektinstanz festgelegt" | Using HouseInstance instead of HouseInstanceAdvanced | Use HouseInstanceAdvanced component |
| SpringSystem_ES | "Room area exceeds boundary" | Total room area > boundary area | Reduce room areas or enable AdjustArea |
| HouseInstance | "Invalid adjacency format" | Malformed adjacency strings | Use format "1-2", "3-4", etc. |

## Performance Considerations

### Computational Complexity

- **Grid size**: O(w × h) memory usage where w,h are grid dimensions
- **Room placement**: O(n × iterations) where n is room count
- **Collision detection**: O(n²) per spring system iteration
- **Evolutionary operations**: O(population × generations)

### Memory Usage

- **Peak memory**: ~10-100 MB for typical floor plans (10-20 rooms)
- **Grid storage**: Primary memory consumer for large boundaries
- **Solution caching**: Multiple solutions stored for comparison

### Performance Tips

1. **Reduce iterations** for faster results during design exploration
2. **Increase cell size** for lower resolution but faster computation  
3. **Limit room count** (>30 rooms may cause performance issues)
4. **Use smaller boundaries** when possible
5. **Enable boundary rotation** only when necessary

## Version Compatibility

### Rhino Compatibility
- **Minimum**: Rhino 7.0
- **Recommended**: Rhino 7.13 or later
- **Tested**: Rhino 7.0-8.0

### Grasshopper Compatibility  
- **Version**: Grasshopper 1.0.0007 or later
- **Dependencies**: Standard Grasshopper installation

### .NET Framework
- **Target**: .NET Framework 4.8
- **Runtime**: Requires .NET Framework 4.8 or later

## Migration Notes

### From Version 1.x to 2.x (Planned)
- **Breaking changes**: Component interface updates
- **Migration path**: Automatic component upgrade
- **New features**: Standalone library support, improved performance

### Known Issues
1. **HouseInstance compatibility**: Currently only HouseInstanceAdvanced works with MagnetizingRooms_ES
2. **Large grids**: Performance degrades with cell sizes < 0.5m
3. **Complex boundaries**: Non-convex boundaries may cause placement issues
4. **Memory usage**: Large room counts (>50) may cause memory pressure