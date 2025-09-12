# Refactoring Plan - Magnetizing Floor Plan Generator

## Executive Summary

Transform the monolithic Grasshopper plugin into a modern, modular, testable architecture following SOLID principles. This refactoring will preserve all existing functionality while dramatically improving maintainability, testability, and extensibility.

## Goals & Objectives

### Primary Goals
1. **Modular Architecture**: Separate concerns into focused, single-responsibility modules
2. **Testability**: Enable comprehensive unit and integration testing
3. **Maintainability**: Make the codebase easier to understand and modify
4. **Performance**: Optimize algorithm performance and memory usage
5. **Extensibility**: Create extension points for new algorithms and features

### Success Criteria
- [ ] 90%+ code coverage with automated tests
- [ ] Zero breaking changes to existing Grasshopper components
- [ ] Performance maintained or improved (≤10% degradation acceptable)
- [ ] Comprehensive documentation for all public APIs
- [ ] Standalone library usable without Grasshopper

## Current State Analysis

### Problems to Address
1. **Monolithic components**: 1,552-line MagnetizingRooms_ES class with multiple responsibilities
2. **Untestable code**: Heavy coupling with Grasshopper runtime
3. **Mixed concerns**: UI, algorithms, and data management intertwined
4. **Compatibility issue**: MagnetizingRooms_ES only works with HouseInstanceAdvanced
5. **Performance bottlenecks**: Inefficient memory allocation and computation patterns
6. **Limited extensibility**: Hard to add new algorithms or optimization strategies

### Technical Debt Assessment
- **Complexity**: Very High (monolithic classes, complex algorithms)
- **Testability**: Very Low (no unit tests, Grasshopper dependencies)
- **Documentation**: Low (minimal algorithm documentation)
- **Performance**: Medium (good algorithms, but inefficient implementation)

## Refactoring Strategy

### Approach: Incremental Strangler Fig Pattern
1. **Extract services gradually** while keeping original components working
2. **Maintain backwards compatibility** throughout refactoring process
3. **Test continuously** to ensure functionality preservation
4. **Document extensively** during extraction process

### Phase-by-Phase Execution
Each phase builds on previous phases and maintains full system functionality.

## Phase 1: Foundation & Documentation (Week 1)

### 1.1 Project Structure Setup
Create new project structure to accommodate modular architecture:

```
src/
├── Core/
│   ├── Domain/               # Business models
│   ├── Interfaces/           # Core abstractions  
│   ├── Services/             # Algorithm implementations
│   └── Exceptions/           # Custom exceptions
├── Infrastructure/           # Cross-cutting concerns
├── Grasshopper/             # GH-specific wrappers
├── Tests/
│   ├── Unit/                # Unit tests
│   ├── Integration/         # Integration tests
│   └── TestData/           # Test data files
└── Examples/               # Usage examples
```

### 1.2 Core Domain Models
Extract pure domain models without external dependencies:

**Domain/Models/Room.cs**
```csharp
public class Room
{
    public int Id { get; set; }
    public string Name { get; set; }
    public double Area { get; set; }
    public bool IsHall { get; set; }
    public Point2d Position { get; set; }
    public Size2d Dimensions { get; set; }
    public List<int> AdjacentRoomIds { get; set; }
}
```

**Domain/Models/FloorPlan.cs**
```csharp
public class FloorPlan
{
    public Boundary Boundary { get; set; }
    public List<Room> Rooms { get; set; }
    public List<Corridor> Corridors { get; set; }
    public Point2d EntrancePoint { get; set; }
    public double CellSize { get; set; }
    public FloorPlanMetrics Metrics { get; set; }
}
```

### 1.3 Test Infrastructure Setup
- **xUnit test project** with Grasshopper test fixtures
- **Test data management** with sample boundaries and room programs
- **Mock objects** for Grasshopper geometry types
- **Performance benchmarking** framework

### 1.4 Documentation Creation
- [x] Technical analysis document
- [x] API reference documentation  
- [x] Refactoring plan (this document)
- [ ] Architecture decision records (ADRs)
- [ ] Algorithm mathematical documentation

**Deliverables:**
- New project structure
- Core domain models
- Test infrastructure
- Comprehensive documentation

## Phase 2: Algorithm Service Extraction (Week 2-3)

### 2.1 Extract Magnetizing Algorithm Core

Create pure algorithm service independent of Grasshopper:

**Interfaces/IMagnetizingAlgorithm.cs**
```csharp
public interface IMagnetizingAlgorithm
{
    Task<FloorPlanResult> GenerateFloorPlan(
        FloorPlanRequest request,
        CancellationToken cancellationToken = default);
    
    AlgorithmConfiguration Configuration { get; set; }
}
```

**Services/MagnetizingAlgorithmService.cs**
```csharp
public class MagnetizingAlgorithmService : IMagnetizingAlgorithm
{
    private readonly IGridGenerator _gridGenerator;
    private readonly IRoomPlacer _roomPlacer;
    private readonly ICorridorGenerator _corridorGenerator;
    private readonly ILogger<MagnetizingAlgorithmService> _logger;
    
    // Pure algorithm implementation extracted from MagnetizingRooms_ES
}
```

### 2.2 Extract Grid Management

**Interfaces/IGridGenerator.cs**
```csharp
public interface IGridGenerator
{
    Grid CreateGrid(Boundary boundary, double cellSize);
    bool IsValidPlacement(Grid grid, Room room, Point2d position);
    void PlaceRoom(Grid grid, Room room, Point2d position);
}
```

### 2.3 Extract Room Placement Logic

**Interfaces/IRoomPlacer.cs**
```csharp
public interface IRoomPlacer  
{
    List<RoomPlacement> FindValidPlacements(
        Grid grid, 
        Room room, 
        List<AdjacencyConstraint> constraints);
    
    RoomPlacement SelectOptimalPlacement(
        List<RoomPlacement> candidates,
        PlacementCriteria criteria);
}
```

### 2.4 Spring System Service Extraction

**Interfaces/ISpringSystem.cs**
```csharp
public interface ISpringSystem
{
    Task<List<Room>> OptimizeLayout(
        List<Room> rooms,
        Boundary boundary,
        SpringSystemConfiguration config);
}
```

**Deliverables:**
- Core algorithm services
- Grid management system
- Room placement engine
- Spring system service
- Comprehensive unit tests for all services

## Phase 3: Infrastructure & Configuration (Week 4)

### 3.1 Dependency Injection Setup

**Infrastructure/ServiceContainer.cs**
```csharp
public static class ServiceContainer
{
    public static IServiceProvider BuildServiceProvider(
        IConfiguration configuration = null)
    {
        var services = new ServiceCollection();
        
        // Register core services
        services.AddScoped<IMagnetizingAlgorithm, MagnetizingAlgorithmService>();
        services.AddScoped<IGridGenerator, GridGenerator>();
        services.AddScoped<IRoomPlacer, RoomPlacer>();
        services.AddScoped<ICorridorGenerator, CorridorGenerator>();
        services.AddScoped<ISpringSystem, SpringSystemService>();
        
        // Register infrastructure
        services.AddLogging();
        services.AddScoped<IGeometryConverter, GeometryConverter>();
        services.AddScoped<IValidationService, ValidationService>();
        
        return services.BuildServiceProvider();
    }
}
```

### 3.2 Configuration Management

**Infrastructure/AlgorithmConfiguration.cs**
```csharp
public class AlgorithmConfiguration
{
    public int MaxIterations { get; set; } = 300;
    public double MaxAdjacencyDistance { get; set; } = 2.0;
    public double CellSize { get; set; } = 1.0;
    public double BoundaryOffset { get; set; } = 2.3;
    public CorridorMode CorridorMode { get; set; } = CorridorMode.TwoSides;
    public bool RemoveDeadEnds { get; set; } = true;
    public bool TryRotateBoundary { get; set; } = false;
}
```

### 3.3 Validation & Error Handling

**Infrastructure/ValidationService.cs**
```csharp
public class ValidationService : IValidationService
{
    public ValidationResult ValidateFloorPlanRequest(FloorPlanRequest request);
    public ValidationResult ValidateBoundary(Boundary boundary);
    public ValidationResult ValidateRoomProgram(List<Room> rooms);
}
```

### 3.4 Geometry Conversion Layer

**Infrastructure/GeometryConverter.cs**
```csharp
public class GeometryConverter : IGeometryConverter  
{
    public Boundary FromRhinoCurve(Curve rhinoCurve);
    public Curve ToRhinoCurve(Boundary boundary);
    public Room FromRhinoBrep(Brep rhinoBrep);
    public Brep ToRhinoBrep(Room room);
}
```

**Deliverables:**
- Dependency injection infrastructure
- Configuration management system
- Validation framework
- Geometry conversion utilities
- Error handling infrastructure

## Phase 4: Grasshopper Integration Layer (Week 5)

### 4.1 Updated Component Architecture

Transform existing components into thin wrappers around core services:

**Grasshopper/Components/MagnetizingRoomsComponent.cs**
```csharp
public class MagnetizingRoomsComponent : GH_Component
{
    private readonly IMagnetizingAlgorithm _algorithm;
    private readonly IGeometryConverter _converter;
    private readonly IValidationService _validator;
    
    // Delegate all algorithm work to injected services
    protected override void SolveInstance(IGH_DataAccess DA)
    {
        // Convert Grasshopper inputs to domain models
        var request = BuildFloorPlanRequest(DA);
        
        // Validate inputs
        var validation = _validator.ValidateFloorPlanRequest(request);
        if (!validation.IsValid)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, validation.ErrorMessage);
            return;
        }
        
        // Execute algorithm
        var result = await _algorithm.GenerateFloorPlan(request);
        
        // Convert results back to Grasshopper types
        SetOutputs(DA, result);
    }
}
```

### 4.2 Fix HouseInstance Compatibility

Create adapter to make original HouseInstance work with the algorithm:

**Grasshopper/Adapters/HouseInstanceAdapter.cs**
```csharp
public class HouseInstanceAdapter
{
    public FloorPlanRequest AdaptHouseInstance(HouseInstance houseInstance);
    public FloorPlanRequest AdaptHouseInstanceAdvanced(HouseInstanceAdvanced advanced);
}
```

### 4.3 Backwards Compatibility Layer

Ensure all existing Grasshopper definitions continue working:

**Grasshopper/Legacy/LegacyComponentWrapper.cs**
```csharp
public class LegacyComponentWrapper  
{
    // Provides compatibility shim for existing definitions
    // Maps old parameter names and types to new architecture
}
```

**Deliverables:**
- Updated Grasshopper components
- HouseInstance compatibility fix
- Backwards compatibility layer
- Component integration tests

## Phase 5: Testing & Quality Assurance (Week 6-7)

### 5.1 Comprehensive Test Suite

**Unit Tests:**
- Algorithm service tests with mock dependencies
- Domain model validation tests  
- Grid generation and manipulation tests
- Room placement logic tests
- Spring system physics tests

**Integration Tests:**
- End-to-end floor plan generation tests
- Grasshopper component integration tests
- Performance regression tests
- Cross-platform compatibility tests

**Test Data:**
- Standard test boundaries (rectangular, L-shaped, complex)
- Varied room programs (small, medium, large buildings)
- Edge cases (overlapping rooms, invalid adjacencies)
- Performance benchmarks (timing, memory usage)

### 5.2 Golden Master Testing

Capture outputs from current system to ensure refactored version produces identical results:

**Tests/GoldenMaster/GoldenMasterTests.cs**
```csharp
[Theory]
[MemberData(nameof(TestCases))]
public async Task RefactoredAlgorithm_ProducesSameResults_AsOriginal(TestCase testCase)
{
    // Run test case through both original and refactored systems
    // Compare outputs for equivalence
}
```

### 5.3 Performance Testing

**Tests/Performance/PerformanceTests.cs**
```csharp
[Fact]
public async Task MagnetizingAlgorithm_PerformsWithinBounds()
{
    // Ensure refactored version maintains performance characteristics
    // Memory usage, execution time, scalability tests
}
```

### 5.4 Code Quality Tools
- **Static analysis** with SonarAnalyzer
- **Code coverage** with Coverlet
- **Performance profiling** with dotMemory/PerfView
- **Documentation** with DocFX

**Deliverables:**
- Comprehensive test suite (90%+ coverage)
- Golden master test validation
- Performance benchmarks
- Code quality reports

## Phase 6: Documentation & Examples (Week 8)

### 6.1 Technical Documentation

**docs/architecture/ARCHITECTURE.md**
- System architecture overview
- Component interaction diagrams
- Data flow documentation
- Extension points and patterns

**docs/algorithms/ALGORITHMS.md**
- Mathematical foundations of magnetizing algorithm
- Spring system physics explanation
- Evolutionary strategy details
- Performance characteristics

### 6.2 Developer Documentation

**docs/developers/DEVELOPMENT.md**
- Development environment setup
- Building and testing procedures
- Debugging guidelines
- Contributing guidelines

**docs/developers/EXTENDING.md**  
- Adding new algorithms
- Creating custom constraints
- Plugin extension points
- API extension examples

### 6.3 Usage Examples

**examples/BasicUsage/BasicFloorPlan.cs**
```csharp
// Standalone library usage example
var algorithm = new MagnetizingAlgorithmService(/*dependencies*/);
var result = await algorithm.GenerateFloorPlan(request);
```

**examples/Grasshopper/SampleDefinitions/**
- Basic floor plan generation
- Custom constraints
- Multi-story buildings  
- Performance optimization

### 6.4 Migration Guide

**docs/MIGRATION_GUIDE.md**
- Upgrading from v1.x to v2.x
- Breaking changes documentation
- Migration tools and scripts
- Common issues and solutions

**Deliverables:**
- Complete technical documentation
- Developer guides and examples
- Migration documentation
- Sample projects and definitions

## Implementation Guidelines

### Code Quality Standards

**Coding Conventions:**
- Follow C# coding guidelines (Microsoft standards)
- Use meaningful names for all identifiers
- Maximum method length: 50 lines
- Maximum class length: 300 lines
- Comprehensive XML documentation for all public APIs

**Architecture Principles:**
- **SOLID principles**: Single responsibility, open/closed, etc.
- **Dependency injection**: Constructor injection preferred
- **Interface segregation**: Small, focused interfaces
- **Composition over inheritance**: Favor composition patterns

**Testing Requirements:**
- **Unit tests**: Every public method must have tests
- **Integration tests**: Test component interactions
- **Performance tests**: Benchmark critical paths
- **Documentation tests**: Ensure examples work

### Performance Considerations

**Memory Management:**
- Use object pooling for frequently allocated objects
- Implement proper disposal patterns for large objects
- Minimize allocations in algorithm hot paths
- Profile memory usage regularly

**Computational Optimization:**
- Parallel processing where beneficial (PLINQ, Parallel.ForEach)
- Caching of expensive computations  
- Efficient data structures (avoid O(n²) operations)
- Algorithm complexity analysis and optimization

**Scalability:**
- Design for buildings up to 100 rooms
- Memory usage should scale linearly with problem size
- Execution time should be predictable and configurable

### Risk Mitigation

**Technical Risks:**
- **Performance degradation**: Continuous performance monitoring
- **Breaking changes**: Comprehensive backwards compatibility testing
- **Data loss**: Robust serialization and versioning
- **Integration issues**: Extensive integration testing

**Mitigation Strategies:**
- **Feature flags**: Allow gradual rollout of new features
- **A/B testing**: Compare old vs new implementations
- **Rollback plan**: Maintain ability to revert changes
- **Monitoring**: Add logging and metrics throughout system

## Success Metrics

### Quantitative Metrics
- [ ] **Code coverage**: ≥90% line coverage
- [ ] **Performance**: ≤10% performance degradation
- [ ] **Memory usage**: ≤20% memory increase acceptable
- [ ] **Bug rate**: <5 bugs per 1000 lines of code
- [ ] **Documentation**: 100% public API documented

### Qualitative Metrics  
- [ ] **Maintainability**: New features can be added without modifying existing code
- [ ] **Testability**: New functionality can be unit tested in isolation
- [ ] **Understandability**: New developers can understand architecture in <1 week
- [ ] **Extensibility**: New algorithms can be added via plugins

## Timeline & Milestones

### Week 1: Foundation
- [x] Project structure setup
- [x] Core domain models
- [x] Documentation framework
- [x] Test infrastructure

### Week 2-3: Core Services
- [ ] Algorithm service extraction
- [ ] Grid management system
- [ ] Room placement engine  
- [ ] Spring system service

### Week 4: Infrastructure
- [ ] Dependency injection setup
- [ ] Configuration management
- [ ] Validation framework
- [ ] Geometry conversion

### Week 5: Integration  
- [ ] Grasshopper component updates
- [ ] HouseInstance compatibility fix
- [ ] Backwards compatibility
- [ ] Integration testing

### Week 6-7: Testing
- [ ] Unit test suite completion
- [ ] Golden master testing
- [ ] Performance validation
- [ ] Quality assurance

### Week 8: Documentation
- [ ] Technical documentation
- [ ] Developer guides
- [ ] Examples and samples
- [ ] Migration documentation

## Post-Refactoring Roadmap

### Immediate Next Steps (Month 2)
1. **Performance optimization**: Profile and optimize identified bottlenecks
2. **Advanced features**: Multi-story support, custom constraints
3. **UI improvements**: Better progress reporting, cancellation support
4. **Documentation refinement**: Based on early user feedback

### Future Enhancements (Month 3-6)
1. **Alternative algorithms**: Packing algorithms, machine learning approaches
2. **Export formats**: DXF, IFC, other CAD formats
3. **Analysis tools**: Space efficiency metrics, circulation analysis
4. **Cloud integration**: Web-based algorithm execution

### Long-term Vision (Year 1+)
1. **Standalone application**: Desktop app independent of Grasshopper
2. **Web interface**: Browser-based floor plan generation
3. **API service**: RESTful API for integration with other tools
4. **Machine learning**: AI-powered room placement optimization

## Conclusion

This refactoring plan transforms the Magnetizing Floor Plan Generator from a monolithic Grasshopper plugin into a modern, modular, testable architecture. The incremental approach ensures functionality preservation while dramatically improving code quality, maintainability, and extensibility.

The end result will be:
- **Maintainable codebase** with clear separation of concerns
- **Comprehensive testing** ensuring reliability and preventing regressions  
- **Improved performance** through optimized algorithms and memory management
- **Enhanced extensibility** enabling future algorithmic innovations
- **Better documentation** supporting open-source collaboration
- **Standalone library** usable beyond Grasshopper

This foundation will enable rapid development of new features and support the plugin's evolution into a leading floor plan generation tool.