# Known Issues - Magnetizing Floor Plan Generator

## Overview
This document tracks known issues in the current codebase that need to be addressed during or after the refactoring process.

## Critical Issues

### 1. HouseInstance Compatibility Issue

**Issue ID**: ISSUE-001  
**Priority**: High  
**Status**: Documented, Planned for Fix  
**Discovered**: User Report  
**Affects Version**: Current (v1.x)

#### Problem Description
The MagnetizingRooms_ES component currently only works with `HouseInstanceAdvanced` and fails when used with the original `HouseInstance` component. Users get the following error:

**Error Message**: `Der Objektverweis wurde nicht auf eine Objektinstanz festgelegt.` (Object reference not set to an instance of an object)

#### Technical Root Cause
The MagnetizingRooms_ES component expects the input to implement specific methods/properties that are only available in HouseInstanceAdvanced but not in the original HouseInstance implementation. Specifically:

1. **Different Interface Implementation**: HouseInstance and HouseInstanceAdvanced implement the IHouseInstance interface differently
2. **Missing Property Access**: MagnetizingRooms_ES may be trying to access properties that don't exist on HouseInstance
3. **Type Casting Issues**: The component may be performing unsafe type casts to HouseInstanceAdvanced

#### Impact Assessment
- **User Impact**: High - Users cannot use the original HouseInstance component with MagnetizingRooms_ES
- **Workflow Impact**: Forces users to migrate to HouseInstanceAdvanced or use workarounds
- **Functionality**: Core algorithm works, only input compatibility affected

#### Planned Resolution
**Target Phase**: Phase 4.2 - Grasshopper Integration Layer

**Solution Approach**:
1. **Create Adapter Pattern**: Implement `HouseInstanceAdapter` class to normalize both input types
2. **Interface Standardization**: Ensure both HouseInstance types properly implement IHouseInstance
3. **Runtime Type Detection**: Add safe type detection and conversion in MagnetizingRooms_ES
4. **Comprehensive Testing**: Test with both HouseInstance types

**Implementation Steps**:
```csharp
// Create adapter to handle both types
public class HouseInstanceAdapter
{
    public FloorPlanRequest AdaptToRequest(object houseInstanceInput)
    {
        return houseInstanceInput switch
        {
            HouseInstance classic => AdaptClassicHouseInstance(classic),
            HouseInstanceAdvanced advanced => AdaptAdvancedHouseInstance(advanced),
            IHouseInstance generic => AdaptGenericHouseInstance(generic),
            _ => throw new ArgumentException("Unsupported house instance type")
        };
    }
    
    private FloorPlanRequest AdaptClassicHouseInstance(HouseInstance house)
    {
        // Safe conversion logic for original HouseInstance
    }
    
    private FloorPlanRequest AdaptAdvancedHouseInstance(HouseInstanceAdvanced house)
    {
        // Conversion logic for HouseInstanceAdvanced
    }
}
```

#### Workaround (Current)
Users can work around this issue by:
1. Using `HouseInstanceAdvanced` instead of `HouseInstance`
2. Converting their existing definitions to use the advanced component

#### Testing Plan
1. **Compatibility Tests**: Test MagnetizingRooms_ES with both HouseInstance types
2. **Regression Tests**: Ensure existing HouseInstanceAdvanced workflows continue working
3. **Migration Tests**: Test conversion of old definitions to new compatibility layer
4. **Error Handling**: Test graceful handling of unsupported input types

---

## Medium Priority Issues

### 2. Performance Degradation with Fine Grid Resolution

**Issue ID**: ISSUE-002  
**Priority**: Medium  
**Status**: Documented  
**Affects**: Grid generation performance

#### Problem Description
When using cell sizes smaller than 0.5m, the algorithm experiences significant performance degradation due to the quadratic increase in grid cells.

#### Impact
- Grid generation time increases exponentially
- Memory usage becomes prohibitive for large buildings
- UI becomes unresponsive during computation

#### Planned Resolution
- **Target Phase**: Performance optimization (post-refactoring)
- **Solution**: Implement hierarchical grid structures and spatial indexing

### 3. High Memory Usage with Many Rooms

**Issue ID**: ISSUE-003  
**Priority**: Medium  
**Status**: Documented  
**Affects**: Memory management

#### Problem Description
Buildings with more than 50 rooms cause significant memory pressure due to:
- Multiple grid solution copies stored in memory
- Inefficient object allocation patterns
- Lack of memory pooling for frequently used objects

#### Planned Resolution
- **Target Phase**: Phase 2-3 (Service extraction and infrastructure)
- **Solution**: Implement object pooling and more efficient data structures

### 4. Non-Convex Boundary Issues

**Issue ID**: ISSUE-004  
**Priority**: Medium  
**Status**: Documented  
**Affects**: Room placement algorithm

#### Problem Description
Complex, non-convex building boundaries can cause:
- Rooms placed outside the actual boundary
- Inefficient space utilization
- Incorrect adjacency calculations

#### Planned Resolution
- **Target Phase**: Phase 2 (Algorithm extraction)
- **Solution**: Improve boundary validation and containment checking

---

## Low Priority Issues

### 5. UI Responsiveness During Long Operations

**Issue ID**: ISSUE-005  
**Priority**: Low  
**Status**: Documented  
**Affects**: User experience

#### Problem Description
Long-running algorithm operations block the Grasshopper UI, making it appear frozen.

#### Planned Resolution
- **Target Phase**: Phase 4 (Grasshopper integration)
- **Solution**: Implement async operations with progress reporting

### 6. Error Message Localization

**Issue ID**: ISSUE-006  
**Priority**: Low  
**Status**: Documented  
**Affects**: User experience

#### Problem Description
Some error messages are in German (`Der Objektverweis wurde nicht auf eine Objektinstanz festgelegt.`) rather than English.

#### Planned Resolution
- **Target Phase**: Phase 3 (Infrastructure)
- **Solution**: Implement proper error handling with localized messages

### 7. Algorithm Mathematical Documentation

**Issue ID**: ISSUE-007  
**Priority**: Low  
**Status**: Documented  
**Affects**: Maintainability and extensibility

#### Problem Description
The mathematical foundations of the algorithms are not well documented, making it difficult to:
- Understand algorithm parameters
- Modify or extend algorithms
- Debug algorithm behavior

#### Planned Resolution
- **Target Phase**: Phase 6 (Documentation)
- **Solution**: Create comprehensive algorithm documentation with mathematical foundations

---

## Resolved Issues

### Template for Resolved Issues
```markdown
### X. Issue Title
**Issue ID**: ISSUE-XXX  
**Priority**: [High/Medium/Low]  
**Status**: Resolved  
**Resolution Date**: [Date]  
**Resolved By**: [Name/Team]

#### Problem Description
[Description of the problem]

#### Solution Implemented
[Description of how it was fixed]

#### Verification
[How the fix was verified to work]
```

---

## Issue Tracking Process

### Reporting New Issues
1. **Create Issue ID**: Use format ISSUE-XXX with sequential numbering
2. **Assign Priority**: High (blocking), Medium (impactful), Low (minor)
3. **Document Impact**: Describe user and technical impact
4. **Plan Resolution**: Assign to appropriate refactoring phase
5. **Update Status**: Track progress through investigation, planning, implementation, testing, resolved

### Priority Definitions
- **High**: Blocks core functionality or user workflows
- **Medium**: Significantly impacts performance, usability, or maintainability  
- **Low**: Minor issues that don't affect core functionality

### Status Definitions
- **Documented**: Issue identified and documented
- **Investigated**: Root cause analysis completed
- **Planned**: Solution approach defined and assigned to phase
- **In Progress**: Work is being done to resolve the issue
- **Testing**: Solution implemented and being validated
- **Resolved**: Issue completely fixed and verified

---

## Post-Refactoring Issue Review

After the refactoring is complete, review this document to:
1. **Verify Resolution**: Confirm all planned issues were actually resolved
2. **Identify New Issues**: Document any new issues discovered during refactoring
3. **Update Priorities**: Re-assess priority of any remaining issues
4. **Plan Next Steps**: Create roadmap for addressing remaining issues

---

**Last Updated**: [Date]  
**Next Review**: After Phase 4.2 (HouseInstance compatibility fix)  
**Maintained By**: Development Team