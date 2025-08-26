# Magnetizing Floor Plan Generator

## Overview

The Magnetizing Floor Plan Generator is an innovative tool for automatically generating floor plans for public buildings. It uses a novel "magnetizing" approach to arrange rooms and create efficient layouts while considering adjacency requirements and spatial constraints.

This Grasshopper plugin provides an algorithmic solution for the complex task of floor plan generation, which is typically time-consuming and challenging for architects, developers, and urban planners. The generator aims to produce diverse, flexible, and rapid results that can serve as a starting point for further design refinement.

## Key Features

- Automatic generation of floor plans based on room program and adjacency requirements
- Flexible input of room areas, connections, and building boundary
- Iterative optimization using a quasi-evolutionary strategy 
- Corridor and circulation space generation
- Adjustable parameters for fine-tuning results
- Visual output of generated floor plans in Rhino/Grasshopper

## Installation

### Option 1: Pre-built Release
1. Ensure you have Rhino 7 or later and Grasshopper installed
2. Download the latest release from the [Food4Rhino page](https://www.food4rhino.com/en/app/magnetizing-floor-plan-generator)
3. Unzip the downloaded file
4. Copy the `Magnetizing_FPG.gha` file to your Grasshopper Libraries folder (typically `%AppData%\Grasshopper\Libraries\`)
5. Restart Rhino and Grasshopper
6. The Magnetizing Floor Plan Generator components should now be available in the Grasshopper canvas under the "Magnetizing_FPG" tab

### Option 2: Build from Source (VS Code)
1. Prerequisites:
   - .NET Framework 4.8 SDK
   - VS Code with C# extension
   - Git
   
2. Clone and build:
   ```bash
   git clone https://github.com/yourusername/Magnetizing_FloorPlanGenerator.git
   cd Magnetizing_FloorPlanGenerator
   dotnet build src/Magnetizing_FPG.csproj -c Release
   ```

3. Install the plugin:
   ```bash
   # Copy the built .gha file to Grasshopper Libraries folder
   copy build\net48\Magnetizing_FPG.gha "%AppData%\Grasshopper\Libraries\"
   ```

4. Restart Rhino and Grasshopper

## Usage

1. Create a new Grasshopper definition
2. Add the `HouseInstance` component to your canvas
3. Connect your building boundary curve to the "Boundary" input
4. Use `RoomInstance` components to define your room program (names, areas, connections)
5. Connect the `RoomInstance` components to the `HouseInstance`
6. Add the `MagnetizingRooms_ES` component and connect the `HouseInstance` to it
7. Adjust parameters as needed (cell size, iterations, etc.)
8. The generated floor plan will be output as curves

For more detailed usage instructions and examples, please refer to the [official documentation](https://www.food4rhino.com/en/app/magnetizing-floor-plan-generator).

## How It Works

The Magnetizing Floor Plan Generator uses the following key steps:

1. Initialization of rooms based on input program
2. Iterative placement of rooms considering adjacencies
3. Corridor generation to connect spaces
4. Optimization using a quasi-evolutionary strategy
5. Fine-tuning of room positions and proportions
6. Optional post-processing (e.g., dead-end removal)

The algorithm aims to balance various factors such as room adjacencies, proportions, and overall layout efficiency.

## Development

### Repository Structure
```
├── src/                    # Source code
│   ├── Magnetizing_FPG/   # Main algorithm implementations
│   └── Properties/        # Assembly info and resources
├── libs/                  # Rhino/Grasshopper dependencies
├── assets/               # Icons and resources
├── docs/                 # Documentation and examples
├── build/                # Build output
└── legacy/               # Original Visual Studio project files
```

### Building in VS Code
1. Open the repository in VS Code
2. Use `Ctrl+Shift+P` → "Tasks: Run Task" → "build-gha" to build the release version
3. Or use the terminal: `dotnet build src/Magnetizing_FPG.csproj -c Release`
4. The built `.gha` file will be in `build/net48/`

### Dependencies
The plugin is **self-contained** - all 3rd party dependencies are embedded directly in the `.gha` file using Costura.Fody.

Development dependencies in `libs/` folder:
- `RhinoCommon.dll` - Core Rhino API (available in Rhino installation)
- `Grasshopper.dll` - Grasshopper API (available in Rhino installation)
- `GH_IO.dll` - Grasshopper I/O operations (available in Rhino installation)
- `clipper_library.dll` - 2D polygon operations (embedded in final .gha)

### Testing
1. Copy the built `.gha` file to your Grasshopper Libraries folder
2. Launch Rhino and test the components in Grasshopper
3. Use VS Code's debugger by pressing F5 (requires Rhino 7 installed)

## Contributing

Contributions to improve the Magnetizing Floor Plan Generator are welcome. Please feel free to submit issues or pull requests through GitHub.

## License


## Authors

- Egor Gavrilov

## Co-Authors

- Sven Schneider
- Martin Dennemark
- Reinhard Koenig

## Acknowledgments

This project was developed at the Bauhaus-University Weimar. We thank all contributors and testers who have helped improve this tool.

## Citation

If you use this tool in your research or projects, please cite:

```
Gavrilov, E., Schneider, S., Dennemark, M., Koenig, R. (2020). Computer-aided approach to public buildings floor plan generation. Magnetizing Floor Plan Generator. In Proceedings of the 1st International Conference on Optimization-Driven Architectural Design.
```

## Contact

For questions or support, please contact [contact information].

Citations:
[1] https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/11249532/25a1c0c0-c0ef-4931-8df6-98bf77f7b074/Computer-aided_approach_to_public_buildings_floor.pdf
[2] https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/11249532/7a06ca7e-a198-4bcf-86ae-607b84a82103/concatenated_code.txt
[3] https://www.food4rhino.com/en/app/magnetizing-floor-plan-generator