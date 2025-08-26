# Technical Report: Magnetizing Floor Plan Generator

## 1. Overview

**Magnetizing Floor Plan Generator** is a C# library for **Rhino/Grasshopper** designed for the procedural generation of architectural floor plans, specifically for public buildings. It provides algorithmic solutions to the complex spatial layout problem, transforming a list of rooms and their required adjacencies into a viable 2D floor plan within a given boundary.

The project employs two distinct generative strategies: a primary grid-based evolutionary algorithm and a secondary spring-system-based genetic algorithm. The tool is structured as a Grasshopper Assembly (`.gha`) file, exposing custom components that architects and designers can use directly within the Rhino visual programming environment.

## 2. Core Architecture & Implementation

The repository contains two primary algorithmic approaches for floor plan generation.

### 2.1. Main Algorithm: Grid-Based Evolutionary Strategy (`MagnetizingRooms_ES.cs`)

This is the main engine of the generator. It operates on a discrete grid of cells representing the floor plan area.

-   **Grid System**: The building boundary is discretized into a 2D integer array (`int[,] workingGrid`). Each cell has a state:
    -   `0`: Empty/available space.
    -   `-1`: Corridor.
    -   `N > 0`: Belongs to room with ID `N`.
    -   `9999`: Outside the boundary (unavailable).

-   **Quasi-Evolutionary Strategy**: The algorithm is an iterative optimization process that maintains a collection of the best-performing solutions (`List<GridSolution>`).
    1.  **Initialization**: The process starts with a grid containing only starting points (entrances) marked as corridors.
    2.  **Iteration**: In each cycle, the algorithm either generates a completely new solution or "mutates" an existing high-scoring solution.
    3.  **Mutation**: Mutation involves taking a good solution, removing the last 1-5 rooms that were placed, and attempting to re-place them and subsequent rooms in a new configuration. This allows the algorithm to escape local maxima.
    4.  **Selection**: The collection of solutions is constantly sorted by a fitness function (primarily, the number of rooms successfully placed), and only the top solutions are kept for the next generation.

-   **Room Placement Logic**:
    1.  **Prioritization**: The algorithm prioritizes which room to place next. The entrance room is always placed first. Subsequently, priority is given to rooms that have the most adjacencies to already-placed rooms.
    2.  **Position Finding**: To place a room, the algorithm searches for all available cells (`0`) that are adjacent to a corridor cell (`-1`) and within a maximum distance (`maxAdjDistance`) of its required neighbors.
    3.  **Placement Evaluation**: For each potential starting cell, the algorithm evaluates multiple orientations (e.g., placing the room with the cell as its top-left, top-right, bottom-left, or bottom-right corner). Each potential placement is given a "rating" based on how many of its sides touch existing rooms or corridors. Placements that result in a more compact layout receive a higher rating. The best-rated valid placement is chosen.

-   **Corridor Generation**: Corridors are not generated as a separate post-process. Instead, they are generated as an integral part of each room. When a room is created, it is defined as a block of cells for the room itself, surrounded by a perimeter of corridor cells (`-1`). The user can choose between one-sided, two-sided, or all-sided corridors.

-   **Post-Processing**: After the final solution is selected, optional cleanup steps can be run:
    -   **Remove Dead Ends**: Identifies and removes corridor segments that are not essential for connectivity by converting them into room space.
    -   **Remove All Corridors**: A more aggressive option that converts all corridor cells into adjacent room space, creating layouts without explicit circulation paths.

### 2.2. Alternative Algorithm: Spring System & Genetic Algorithm (`SpringSystem_ES.cs`)

This component provides an alternative, physics-based approach operating in continuous space rather than on a discrete grid.

-   **Representation**: A "Gene" represents a complete floor plan, where the layout is a collection of `Room` objects. Each `Room` has continuous properties: `CenterX`, `CenterY`, `width`, and `area` (height is derived).

-   **Genetic Algorithm**:
    -   **Fitness Function**: Genes are evaluated based on a weighted sum of two metrics:
        1.  Geometric Fitness (`FitnessFunctionG`): The total area of all rooms that falls *inside* the building boundary. This uses the **Clipper** library for efficient polygon intersection calculations.
        2.  Topological Fitness (`FitnessFunctionT`): The inverse of the sum of distances between the centers of rooms that are required to be adjacent.
    -   **Evolution**: The algorithm evolves a `GeneCollection` by applying genetic operators:
        -   **Cross-Over**: Creates a new child gene by randomly combining rooms from two parent genes.
        -   **Mutation**: Randomly alters a gene by slightly changing the position (`CenterX`, `CenterY`) or proportions (`width`) of its rooms.
        -   **Newbies**: Injects entirely new, randomly generated genes into the population to maintain diversity.

-   **Collision & Overlap Resolution**: After each iteration, a "spring system" simulation runs.
    -   It detects intersections between rooms and between rooms and the boundary.
    -   It first attempts to resolve overlaps by rescaling the rooms (e.g., making them narrower but taller) while preserving their area.
    -   If rescaling fails or violates the maximum proportion constraints (`proportionThreshold`), it resolves the collision by physically translating the rooms apart.

## 3. Project Structure & Key Files

The project is a single Visual Studio solution (`Magnetizing_FPG.sln`) containing one C# project (`Magnetizing_FPG.csproj`).

```
├── Magnetizing\_FPG.sln
├── Magnetizing\_FPG.csproj
├── README.md
│
├── FloorPlan\_Generator\_1Info.cs      # Assembly info for Grasshopper plugin registration.
│
├── Magnetizing\_FPG/
│   ├── MagnetizingRooms\_ES.cs        # PRIMARY ALGORITHM: Grid-based evolutionary strategy solver.
│   ├── SpringSystem\_ES.cs            # ALTERNATIVE ALGORITHM: Spring system & genetic algorithm solver.
│   │
│   └── RoomProgram/
│       ├── HouseInstance.cs          # GUI Component: Groups rooms, defines boundary and entrance.
│       ├── HouseInstanceAttributes.cs  # Defines custom rendering and interaction for HouseInstance.
│       ├── RoomInstance.cs           # GUI Component: Defines a single room's properties.
│       ├── RoomInstanceAttributes.cs # Defines custom rendering and interaction for RoomInstance.
│       └── HouseInstanceTextInput.cs # Defines interfaces (IHouseInstance, IRoomInstance) and a text-based input component.
│
├── Properties/
│   ├── AssemblyInfo.cs               # Standard assembly metadata.
│   ├── Resources.resx                # Embedded resources (e.g., component icons).
│   └── Resources.Designer.cs         # Auto-generated resource accessor.
│
└── studies/                          # Contains deprecated, experimental, or alternative component versions.

```

## 4. Dependencies

-   **Rhino & Grasshopper**:
    -   `RhinoCommon.dll`: Core Rhino 3D modeling library.
    -   `Grasshopper.dll`: Grasshopper core library for visual programming.
-   **Third-Party**:
    -   `clipper_library.dll`: A high-performance library for 2D polygon clipping and offsetting operations (intersection, union, difference). This is used heavily in the `SpringSystem_ES` component for fitness calculations.

## 5. Build Process

-   The project is built using MSBuild via the Visual Studio solution.
-   The target output is a `.dll` file, which is then renamed to a `.gha` (Grasshopper Assembly) file via a **Post-Build Event**.
-   The post-build script automatically copies the final `.gha` file to the user's Grasshopper Libraries folder (`%AppData%\Grasshopper\Libraries\`) for immediate use.
-   The project includes debug configurations that launch specific versions of Rhino for testing.

## 6. Usage Workflow (Developer Perspective)

1.  **Define Program**: The user defines the architectural program using the GUI components.
    -   Multiple `RoomInstance` components are placed on the canvas. Their properties (name, area) are set via a custom pop-up UI triggered by a double-click.
    -   Adjacency requirements are defined by "wiring" `RoomInstance` components together. This is handled by custom mouse events in `RoomInstanceAttributes.cs` that draw arrows between components.
    -   A `HouseInstance` component is used to group all related `RoomInstance` components. It also takes the building boundary curve and entrance point(s) as input.
2.  **Generate Layout**: The output of the `HouseInstance` component, which encapsulates the entire building program, is connected to a solver component like `MagnetizingRooms_ES`.
3.  **Solve**: The solver component runs its iterative algorithm. The user can adjust parameters like the number of iterations and `MaxAdjDistance` to influence the result.
4.  **Output**: The solver outputs the generated floor plan as a list of Breps (for rooms and corridors), along with metadata like room names and any unfulfilled adjacencies.

---