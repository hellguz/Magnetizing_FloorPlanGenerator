# Test Data Directory

This directory contains standardized test data for the Magnetizing Floor Plan Generator tests.

## Directory Structure

### Boundaries/
Contains boundary curve definitions for different building shapes:
- `rectangular_small.json` - 20x15m rectangular boundary
- `rectangular_medium.json` - 40x30m rectangular boundary  
- `rectangular_large.json` - 60x45m rectangular boundary
- `l_shape.json` - L-shaped boundary for complex geometry testing
- `narrow_building.json` - 50x8m narrow building
- `square_building.json` - 25x25m square building
- `complex_boundary.json` - Complex multi-segment boundary

### RoomPrograms/
Contains room program definitions with different complexity levels:
- `simple_office.json` - 3 rooms (entrance + 2 offices)
- `medium_office.json` - 6 rooms with various adjacencies
- `large_office.json` - 12 rooms for stress testing
- `residential.json` - Residential program (living room, bedrooms, kitchen, etc.)
- `retail.json` - Retail space program
- `medical_clinic.json` - Medical office program

### ExpectedResults/
Contains expected outputs for regression testing:
- `golden_master_results.json` - Known good outputs for various test cases
- `performance_benchmarks.json` - Expected performance characteristics
- `boundary_rectangular_small_rooms_simple.json` - Expected result for specific combination

### Adjacencies/
Contains adjacency patterns for testing:
- `linear_adjacency.json` - Simple linear connections
- `hub_pattern.json` - Central hub connected to all rooms
- `complex_network.json` - Complex interconnected pattern
- `minimal_adjacency.json` - Minimal connections for testing

## JSON Format Examples

### Boundary Format
```json
{
  "name": "rectangular_small",
  "description": "Small rectangular office building",
  "points": [
    {"x": 0, "y": 0, "z": 0},
    {"x": 20, "y": 0, "z": 0},
    {"x": 20, "y": 15, "z": 0},
    {"x": 0, "y": 15, "z": 0}
  ],
  "is_closed": true,
  "area": 300
}
```

### Room Program Format
```json
{
  "name": "simple_office",
  "description": "Simple 3-room office layout",
  "entrance_point": {"x": 10, "y": 0, "z": 0},
  "rooms": [
    {
      "id": 1,
      "name": "Entrance",
      "area": 15,
      "is_hall": true
    },
    {
      "id": 2,
      "name": "Office 1",
      "area": 25,
      "is_hall": false
    },
    {
      "id": 3,
      "name": "Office 2", 
      "area": 20,
      "is_hall": false
    }
  ],
  "adjacencies": ["1-2", "1-3"]
}
```

### Expected Result Format
```json
{
  "test_case": "rectangular_small_simple_office",
  "boundary": "rectangular_small",
  "room_program": "simple_office",
  "algorithm_config": {
    "iterations": 300,
    "max_adj_distance": 2.0,
    "cell_size": 1.0
  },
  "expected_output": {
    "rooms_placed": 3,
    "total_area_utilized": 285.5,
    "adjacencies_satisfied": 2,
    "corridors_generated": true,
    "execution_time_ms": 1200,
    "memory_usage_mb": 45
  }
}
```

## Usage in Tests

### Loading Test Data
```csharp
// Load boundary data
var boundary = TestDataLoader.LoadBoundary("rectangular_small");

// Load room program
var roomProgram = TestDataLoader.LoadRoomProgram("simple_office");

// Load expected results
var expectedResult = TestDataLoader.LoadExpectedResult("rectangular_small_simple_office");
```

### Creating Test Combinations
```csharp
// Generate test cases for all boundary/room program combinations
var testCases = TestDataGenerator.CreateAllCombinations();

// Generate specific test cases
var specificCases = TestDataGenerator.CreateCases(
    boundaries: new[] {"rectangular_small", "l_shape"},
    roomPrograms: new[] {"simple_office", "medium_office"}
);
```

## Adding New Test Data

1. **Create new boundary**: Add JSON file to `Boundaries/` directory
2. **Create new room program**: Add JSON file to `RoomPrograms/` directory
3. **Generate expected results**: Run the current system to capture outputs
4. **Update test cases**: Add new combinations to test suite

## Validation

All test data files are validated for:
- JSON format correctness
- Geometric validity (e.g., closed boundaries)
- Room program consistency (e.g., total area vs boundary area)
- Adjacency relationship validity

## Performance Test Data

For performance testing, we maintain:
- **Small datasets**: Quick validation (< 1 second)
- **Medium datasets**: Typical usage (1-10 seconds)  
- **Large datasets**: Stress testing (10+ seconds)
- **Edge cases**: Boundary conditions and error scenarios