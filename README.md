# Magnetizing Floor Plan Generator

## Overview

The Magnetizing Floor Plan Generator is an innovative tool for automatically generating floor plans for public buildings.  It uses a novel "magnetizing" approach to arrange rooms and create efficient layouts while considering adjacency requirements and spatial constraints.  This Grasshopper plugin provides an algorithmic solution for the complex task of floor plan generation, which is typically time-consuming and challenging for architects, developers, and urban planners.  The generator aims to produce diverse, flexible, and rapid results that can serve as a starting point for further design refinement. 

## Key Features

- Automatic generation of floor plans based on a room program and adjacency requirements 
- Flexible input of room areas, connections, and building boundary 
- Iterative optimization using a quasi-evolutionary strategy 
- Corridor and circulation space generation 
- Adjustable parameters for fine-tuning results 
- Visual output of generated floor plans in Rhino/Grasshopper 

## How It Works

The Magnetizing Floor Plan Generator uses the following key steps: 

1.  **Grid Initialization**: The building boundary is converted into a grid of cells. Cells outside the boundary are marked as unusable. 
2.  **Iterative Placement**: The algorithm iteratively places rooms onto the grid. It prioritizes rooms with more connections to already-placed rooms.  An evolutionary strategy is used to explore different placement solutions, keeping a collection of the best ones found so far. 
3.  **Optimization**: To find better solutions, the algorithm periodically removes a few rooms from an existing good solution and tries to place them again differently.  This helps escape local optima and improves the layout.
4.  **Corridor Generation**: Corridors are automatically generated to connect the rooms and provide circulation space. 
5.  **Finalization**: After the iterations are complete, the best solution is selected.  Dead-end corridors can be optionally removed to create a more efficient circulation network. 

## Installation

1.  Ensure you have Rhino 7 or later and Grasshopper installed.
2.  Download the latest release from the [Food4Rhino page](https://www.food4rhino.com/en/app/magnetizing-floor-plan-generator). 
3.  Unzip the downloaded file. 
4.  Copy the `Magnetizing_FPG.gha` file to your Grasshopper Libraries folder (typically find this by typing `GrasshopperFolders` in Rhino and selecting the "Libraries" folder). 
5.  Restart Rhino and Grasshopper. 
6.  The Magnetizing Floor Plan Generator components should now be available in the Grasshopper canvas under the "Magnetizing_FPG" tab. 

## Building from Source

To build this project from source, you will need:
* Microsoft Visual Studio 2022.
* .NET Framework 4.8 Targeting Pack.
* A copy of Rhino 7 or 8 installed.

The project is configured to find the necessary Rhino and Grasshopper DLLs from your Rhino installation. Simply open the `.sln` file in Visual Studio and build the solution. The `PostBuildEvent` will automatically copy the resulting `.gha` file to the Grasshopper Libraries folder.

## Usage

1.  Create a new Grasshopper definition. 
2.  Add the `HouseInstance` component to your canvas. 
3.  Connect your building boundary curve to the "Boundary" input. 
4.  Use `RoomInstance` components to define your room program (names, areas, connections). 
5.  Connect the `RoomInstance` components to the `HouseInstance`. 
6.  Add the `MagnetizingRooms_ES` component and connect the `HouseInstance` to it. 
7.  Adjust parameters as needed (cell size, iterations, etc.). 
8.  The generated floor plan will be output as curves. 

For more detailed usage instructions and examples, please refer to the [official documentation](https://www.food4rhino.com/en/app/magnetizing-floor-plan-generator). 

## Contributing

Contributions to improve the Magnetizing Floor Plan Generator are welcome.  Please feel free to submit issues or pull requests through GitHub. 

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Authors

-   Egor Gavrilov

## Co-Authors

-   Sven Schneider
-   Martin Dennemark
-   Reinhard Koenig

## Acknowledgments

This project was developed at the Bauhaus-University Weimar.  We thank all contributors and testers who have helped improve this tool. 

## Citation

If you use this tool in your research or projects, please cite: 

Gavrilov, E., Schneider, S., Dennemark, M., Koenig, R. (2020). Computer-aided approach to public buildings floor plan generation. Magnetizing Floor Plan Generator. In Proceedings of the 1st International Conference on Optimization-Driven Architectural Design. 


## Contact

For questions, bug reports, or support, please [open an issue](https://github.com/EgorGavrilov/Magnetizing-Floor-Plan-Generator/issues) on the GitHub repository.

