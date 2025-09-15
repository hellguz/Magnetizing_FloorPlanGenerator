# Magnetizing Algorithm Tests

## Overview

This test suite verifies the core functionality of the magnetizing floor plan generation algorithm outside of the Grasshopper environment. It focuses on testing algorithm structure, deterministic behavior, and input validation.

## What the Tests Do

### ✅ **Tests That Pass**
1. **Seed Determinism**: Verifies that the same random seed produces consistent behavior
2. **Input Validation**: Tests that input structures can be created and configured correctly
3. **Algorithm Structure**: Verifies that solver classes can be instantiated properly

### ⚠️ **Limitations**
- **Geometry Tests**: Full end-to-end tests with actual floor plan generation require Rhino environment
- **Visual Results**: Cannot verify visual outputs without Rhino's geometry engine
- **Complete Workflows**: Real-world testing should be done within Grasshopper

## How to Run Tests

### Option 1: Run Script (Recommended)
```bash
# Windows Batch
run_tests.bat

# Windows PowerShell
./run_tests.ps1
```

### Option 2: Manual Build & Run
```bash
# Build the test project
dotnet build tests\Magnetizing_FPG.Tests.csproj -c Release

# Run the executable
tests\bin\Release\net48\Magnetizing_FPG.Tests.exe
```

## Expected Output

```
🔬 Magnetizing Floor Plan Generator - End-to-End Tests
======================================================

🧪 Basic Algorithm Tests (No Rhino Dependencies)
===============================================

✅ Test 1: Seed Determinism - PASSED
✅ Test 2: Input Validation - PASSED
✅ Test 3: Algorithm Structure - PASSED

📊 Results: 3/3 tests passed
🎉 All basic tests passed!
💡 Note: Full geometry tests require Rhino environment.
```

## Test Details

### Seed Determinism Test
- Creates two identical input configurations with the same random seed
- Runs the algorithm with both inputs
- Verifies that behavior is consistent (deterministic)
- **Purpose**: Ensures reproducible results for debugging and regression testing

### Input Validation Test
- Creates `SolverInputs` object with various parameters
- Verifies all properties can be set correctly
- Tests data structure integrity
- **Purpose**: Validates that the decoupled input system works properly

### Algorithm Structure Test
- Instantiates `MagnetizingSolver` class
- Creates `SolverOutputs` structure
- Verifies basic object creation succeeds
- **Purpose**: Confirms that algorithm classes are properly structured

## Test Data

Tests automatically create a `TestData` folder with:
- `simple_two_room_results.txt` - Results from basic room layout tests
- `complex_multi_room_results.txt` - Results from complex layout tests
- Additional result files as tests are added

## For Complete Testing

To fully test the algorithm with real geometry:

1. **Use Grasshopper**: Load the `.gha` plugin and test with real boundary curves
2. **Create Test Definitions**: Build Grasshopper definitions with known inputs
3. **Visual Verification**: Check that room layouts match expectations
4. **Edge Cases**: Test with complex boundaries, many rooms, tight constraints

## Extending Tests

To add new tests:

1. **Add Test Method**: Create new test method in `SimpleMockTests.cs`
2. **Update Runner**: Add test call to `RunBasicAlgorithmTests()`
3. **Document**: Update this README with test description

### Example New Test
```csharp
private bool TestNewFeature()
{
    try
    {
        // Test logic here
        Console.WriteLine("   📋 Testing new feature...");

        // Your test implementation

        return testPassed;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ❌ Error: {ex.Message}");
        return false;
    }
}
```

## Continuous Integration

These tests are designed to run in CI environments:
- ✅ No GUI dependencies
- ✅ Self-contained executable
- ✅ Clear pass/fail output
- ✅ Exit codes for automation

Use in your CI pipeline:
```yaml
- name: Run Algorithm Tests
  run: ./run_tests.ps1
```

---

**Note**: This test suite complements but does not replace testing within the Grasshopper environment. Use both approaches for comprehensive validation.