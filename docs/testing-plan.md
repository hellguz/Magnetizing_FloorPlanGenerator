# E2E Testing Implementation Plan

## Overview
We're adding end-to-end testing capability to the Magnetizing Floor Plan Generator without requiring Rhino/Grasshopper, while keeping the project simple and maintainable.

## Key Principles
- ✅ **Incremental approach**: Small steps, build and test after each change
- ✅ **Keep it simple**: No major refactoring, just add testing on top
- ✅ **Maintain compatibility**: Grasshopper plugin must continue working perfectly
- ✅ **Deterministic results**: Expose random seed for reproducible testing
- ✅ **Stay graspable**: Project should remain easy to understand

## Implementation Steps

### ✅ Step 1: Documentation
- [x] Create `docs/testing-plan.md` to track progress and agreements
- [x] Document our incremental approach

### 🔄 Step 2: Expose Random Seed (Safest First Change)
- [ ] Add `RandomSeed` property to `MagnetizingRooms_ES` class
- [ ] Replace `Random random = new Random();` with `Random random = new Random(RandomSeed);`
- [ ] Add input parameter for RandomSeed in Grasshopper component
- [ ] Build and verify Grasshopper plugin works identically
- [ ] Test deterministic behavior with same seed

### 📋 Step 3: Minimal Core Interfaces
- [ ] Create `src/Core/Interfaces/IFloorPlanGenerator.cs`
- [ ] Make `MagnetizingRooms_ES` implement interface (no logic changes)
- [ ] Build and verify no functionality broken

### 🖥️ Step 4: Basic Console Test Application
- [ ] Create `src/ConsoleApp/Program.cs`
- [ ] Directly instantiate and call existing algorithm
- [ ] Use simple hardcoded test data initially
- [ ] Build and verify console app can run algorithm

### 🧪 Step 5: Simple Test Project
- [ ] Add test project with MSTest/NUnit
- [ ] Create one basic test: "algorithm runs without crashing"
- [ ] Build and verify test passes

### 📈 Step 6: Gradual Expansion (Future)
- [ ] Add more test scenarios as needed
- [ ] Create sample test data files
- [ ] Add golden file testing for output validation

## Current Status: Step 1 Complete ✅

## Technical Notes

### Random Seed Implementation
The current code has `Random random = new Random();` on line 17 of `MagnetizingRooms_ES.cs`. We'll make this configurable:

```csharp
// Before
Random random = new Random();

// After  
public int RandomSeed { get; set; } = Environment.TickCount; // Default to current behavior
Random random = new Random(RandomSeed);
```

### File Structure (Planned)
```
├── src/
│   ├── Core/
│   │   └── Interfaces/           # Simple interfaces (Step 3)
│   ├── ConsoleApp/              # Standalone test runner (Step 4)
│   └── [existing structure]    # Unchanged
├── tests/                       # Test project (Step 5)
├── docs/
│   └── testing-plan.md         # This file
└── [existing files]            # Unchanged
```

## Success Criteria
- [ ] Algorithm produces deterministic results with same random seed
- [ ] Console application can run algorithm without Rhino/Grasshopper
- [ ] Basic automated tests verify algorithm functionality
- [ ] Grasshopper plugin continues to work exactly as before
- [ ] Project remains simple and easy to understand

---
*Last updated: [Date] - Step 1 Documentation Complete*