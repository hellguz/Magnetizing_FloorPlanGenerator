# Magnetizing Floor Plan Generator

A Grasshopper plugin for automatically generating floor plans using a novel "magnetizing" algorithm that intelligently arranges rooms based on adjacency requirements and spatial constraints.

## ✨ What it does

This tool helps architects, developers, and urban planners quickly generate efficient floor plan layouts for public buildings. Instead of manually arranging rooms, the algorithm uses a quasi-evolutionary approach to optimize room placement, creating diverse layout options that serve as excellent starting points for further design development.

**Key Features:**
- 🏗️ Automatic room arrangement based on your building program
- 🔗 Respects room adjacency requirements (which rooms should be connected)
- 📐 Works within custom building boundaries
- 🎯 Generates multiple layout variations for comparison
- ⚡ Fast iteration for exploring design alternatives

## 📥 Installation (Easy!)

### Option 1: Download Pre-built Plugin *(Recommended)*
1. **Get Rhino 7+** - Make sure you have Rhino 7 or later with Grasshopper
2. **Download** the latest `.gha` file from [Food4Rhino](https://www.food4rhino.com/en/app/magnetizing-floor-plan-generator)
3. **Copy** the `Magnetizing_FPG.gha` file to your Grasshopper Libraries folder:
   - Windows: `%AppData%\Grasshopper\Libraries\`
   - Mac: `~/Library/Application Support/McNeel/Rhinoceros/MacPlugIns/Grasshopper/Libraries/`
4. **Restart** Rhino and Grasshopper
5. **Find** the components under the "Magnetizing_FPG" tab in Grasshopper

That's it! No additional downloads needed - the plugin works with the .NET Framework that's already on your system.

### Option 2: Build from Source (For Developers)
**Prerequisites:** Visual Studio Code + .NET Framework 4.8 SDK

```bash
git clone https://github.com/yourusername/Magnetizing_FloorPlanGenerator.git
cd Magnetizing_FloorPlanGenerator
dotnet build -c Release
copy build\net48\Magnetizing_FPG.gha "%AppData%\Grasshopper\Libraries\"
```

## 🚀 How to Use

1. **Create your building boundary** - Draw a closed curve in Rhino
2. **Add HouseInstance component** - Connect your boundary curve
3. **Define your room program** - Use RoomInstance components to specify:
   - Room names (e.g., "Office", "Meeting Room", "Lobby")
   - Room areas (in square meters)
   - Which rooms should be adjacent to each other
4. **Add MagnetizingRooms_ES component** - This runs the algorithm
5. **Adjust parameters** - Fine-tune cell size, iterations, and other settings
6. **Get results** - The algorithm outputs optimized floor plan layouts as curves

The algorithm intelligently places rooms to satisfy your adjacency requirements while efficiently using the available space.

## 🏗️ How It Works

The "magnetizing" algorithm treats rooms like magnetic objects that attract or repel each other based on your adjacency requirements. The process:

1. **Initialization** - Rooms are randomly placed within the building boundary
2. **Magnetizing Forces** - Adjacent rooms attract each other, non-adjacent rooms repel
3. **Evolutionary Optimization** - Multiple generations of layouts are tested and improved
4. **Constraint Satisfaction** - Solutions respect room sizes, building boundaries, and adjacency rules
5. **Output Generation** - The best layouts are converted to architectural drawings

## 🔧 Technical Details

- **Platform:** Grasshopper for Rhino 7+
- **Framework:** .NET Framework 4.8 *(chosen for maximum compatibility - no additional runtime downloads required)*
- **Dependencies:** Self-contained (all libraries embedded in the plugin)
- **Performance:** Optimized for real-time feedback with reasonable building sizes

## 📁 Repository Structure

```
├── src/                    # Source code
│   ├── MagnetizingRooms_ES.cs     # Main algorithm implementation
│   ├── RoomProgram/               # Room and house instance classes
│   └── Properties/                # Assembly info and resources
├── libs/                  # Rhino/Grasshopper dependencies  
├── build/                 # Build output (.dll and .gha files)
└── README.md             # This file
```

## 👥 Authors & Contributors

**Main Author:** Egor Gavrilov

**Co-Authors:**
- Sven Schneider  
- Martin Dennemark
- Reinhard Koenig

## 🙏 Acknowledgments

This project was developed at **Bauhaus-University Weimar**. We thank all contributors and testers who helped improve this tool, and the broader computational design community for their valuable feedback.

## 📄 Citation

If you use this tool in your research or projects, please cite:

```
Gavrilov, E., Schneider, S., Dennemark, M., Koenig, R. (2020). 
Computer-aided approach to public buildings floor plan generation. 
Magnetizing Floor Plan Generator. In Proceedings of the 1st International 
Conference on Optimization-Driven Architectural Design.
```

## 📚 References

- [Research Paper](https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/11249532/25a1c0c0-c0ef-4931-8df6-98bf77f7b074/Computer-aided_approach_to_public_buildings_floor.pdf) - Original research publication
- [Food4Rhino Page](https://www.food4rhino.com/en/app/magnetizing-floor-plan-generator) - Official plugin page

## 🧪 Testing & Development

### Running Algorithm Tests

The project includes a standalone test runner that allows you to test the core algorithm without Grasshopper:

```bash
# Navigate to project directory
cd Magnetizing_FloorPlanGenerator

# Run the test suite
dotnet run --project tests/ConsoleTestRunner.csproj
```

This will run comprehensive tests including:
- ✅ Core algorithm instantiation and configuration
- ✅ RandomSeed deterministic behavior validation
- ✅ Standalone algorithm execution (without Grasshopper dependencies)
- ✅ Original Grasshopper component testing
- ✅ Test case management and regression testing

### Building the Project

```bash
# Build the main Grasshopper plugin
dotnet build src/Magnetizing_FPG.csproj

# Build the test runner
dotnet build tests/ConsoleTestRunner.csproj

# Build everything
dotnet build Magnetizing_FPG.sln
```

### Core Algorithm Usage (Standalone)

The extracted core algorithm can be used independently for testing and development:

```csharp
using Magnetizing_FPG.CoreAlgorithm;

// Create standalone algorithm
var algorithm = new CoreMagnetizingAlgorithm(12345); // Fixed seed for deterministic results

// Create test input
var input = new AlgorithmInput {
    House = new SimpleHouse {
        Boundary = SimpleBoundary.CreateRectangle(0, 0, 10, 8),
        Rooms = {
            new SimpleRoom("Living Room", 20, false, true), // Last param = isEntrance
            new SimpleRoom("Kitchen", 15),
            new SimpleRoom("Bedroom", 18)
        },
        AdjacencyStrings = { "1-2", "1-3" } // Room connections
    },
    Iterations = 50,
    MaxAdjDistance = 2.0,
    CellSize = 1.0
};

// Run algorithm
var result = algorithm.GenerateFloorPlan(input);

// Check results
Console.WriteLine($"Success: {result.Success}");
Console.WriteLine($"Rooms placed: {result.PlacedRoomsCount}/{result.TotalRoomsCount}");
```

## 🐛 Support

Found a bug or have a feature request? Please report issues on our [GitHub Issues page](https://github.com/yourusername/Magnetizing_FloorPlanGenerator/issues).

---

*Built with ❤️ for the architectural design community*