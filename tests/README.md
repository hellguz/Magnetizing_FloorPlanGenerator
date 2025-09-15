# Tests Directory

This directory will contain automated tests for the Magnetizing Floor Plan Generator.

## Test Strategy

Since the algorithm uses RhinoCommon which requires Rhino to be running, our testing approach focuses on:

### 1. Deterministic Testing via RandomSeed
- ✅ **Implemented**: RandomSeed property allows same input → same output
- 🎯 **Usage**: Set fixed seed in Grasshopper for reproducible results
- 📊 **Validation**: Compare outputs across multiple runs with same seed

### 2. Console App Testing Framework  
- ✅ **Created**: `src/ConsoleApp/` provides testing infrastructure
- 🔧 **Purpose**: Foundation for non-Rhino algorithm components
- 📈 **Future**: Can test mathematical calculations, data structures, etc.

### 3. Grasshopper Automation (Future)
- 📋 **Planned**: Automated Grasshopper scripts for algorithm testing
- 🎯 **Goal**: Run algorithm with test data, capture outputs
- ✅ **Ready**: RandomSeed makes this feasible

## Current Test Status

### ✅ Working
- RandomSeed property for deterministic results
- Console app builds and runs (.NET 4.8 compatible)
- Basic testing infrastructure established

### 🎯 Next Steps
- Create test data files (boundaries, room programs)
- Add unit tests for algorithm components
- Implement golden file testing for output validation
- Create automated test scenarios

## Running Tests

Currently manual testing via Grasshopper with fixed RandomSeed values.

Future: `dotnet test` for automated test suite.