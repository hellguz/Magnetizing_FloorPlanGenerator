using System;
using System.Collections.Generic;

namespace Magnetizing_FPG.Core.Domain
{
    /// <summary>
    /// Configuration parameters for the magnetizing floor plan generation algorithm.
    /// </summary>
    public class AlgorithmConfiguration
    {
        /// <summary>
        /// Maximum number of optimization iterations to perform.
        /// </summary>
        public int MaxIterations { get; set; } = 300;

        /// <summary>
        /// Maximum distance for adjacency satisfaction (in meters).
        /// </summary>
        public double MaxAdjacencyDistance { get; set; } = 2.0;

        /// <summary>
        /// Grid cell size for discretization (in meters).
        /// </summary>
        public double CellSize { get; set; } = 1.0;

        /// <summary>
        /// Boundary expansion offset for room placement (in meters).
        /// </summary>
        public double BoundaryOffset { get; set; } = 2.3;

        /// <summary>
        /// Maximum allowed room aspect ratio (width/height or height/width).
        /// </summary>
        public double MaxRoomAspectRatio { get; set; } = 1.9;

        /// <summary>
        /// Whether to attempt boundary rotation for optimal packing.
        /// </summary>
        public bool TryRotateBoundary { get; set; } = false;

        /// <summary>
        /// Corridor generation mode.
        /// </summary>
        public CorridorMode CorridorMode { get; set; } = CorridorMode.TwoSides;

        /// <summary>
        /// Whether to remove dead-end corridors after generation.
        /// </summary>
        public bool RemoveDeadEnds { get; set; } = true;

        /// <summary>
        /// Whether to remove all corridors and assign space to adjacent rooms.
        /// </summary>
        public bool RemoveAllCorridors { get; set; } = false;

        /// <summary>
        /// Whether corridors add to total area or replace room area.
        /// </summary>
        public bool CorridorsAsAdditionalSpaces { get; set; } = false;

        /// <summary>
        /// Minimum corridor width (in meters).
        /// </summary>
        public double MinCorridorWidth { get; set; } = 1.2;

        /// <summary>
        /// Percentage of total area reserved for circulation.
        /// </summary>
        public double CirculationPercentage { get; set; } = 0.15;

        /// <summary>
        /// Whether to adjust room areas to fit within boundary.
        /// </summary>
        public bool AdjustAreaToFitBoundary { get; set; } = false;

        /// <summary>
        /// Random seed for reproducible results (null for random seed).
        /// </summary>
        public int? RandomSeed { get; set; } = null;

        /// <summary>
        /// Fitness function balance between area maximization and adjacency satisfaction.
        /// 0.0 = only adjacencies, 1.0 = only area, 0.5 = balanced.
        /// </summary>
        public double FitnessFunctionBalance { get; set; } = 0.5;

        /// <summary>
        /// Spring system configuration for physics-based optimization.
        /// </summary>
        public SpringSystemConfiguration SpringSystem { get; set; } = new SpringSystemConfiguration();

        /// <summary>
        /// Custom configuration properties for extensibility.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Validates the algorithm configuration.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (MaxIterations <= 0)
                result.AddError("MaxIterations must be greater than zero");

            if (MaxIterations > 10000)
                result.AddWarning($"MaxIterations ({MaxIterations}) is very high - may cause performance issues");

            if (MaxAdjacencyDistance <= 0)
                result.AddError("MaxAdjacencyDistance must be greater than zero");

            if (CellSize <= 0)
                result.AddError("CellSize must be greater than zero");

            if (CellSize < 0.1)
                result.AddWarning($"CellSize ({CellSize}m) is very small - may cause performance issues");

            if (CellSize > 5.0)
                result.AddWarning($"CellSize ({CellSize}m) is very large - may produce coarse results");

            if (BoundaryOffset < 0)
                result.AddError("BoundaryOffset cannot be negative");

            if (MaxRoomAspectRatio < 1.0)
                result.AddError("MaxRoomAspectRatio must be at least 1.0");

            if (MaxRoomAspectRatio > 10.0)
                result.AddWarning($"MaxRoomAspectRatio ({MaxRoomAspectRatio}) is very high - may produce unusable rooms");

            if (FitnessFunctionBalance < 0.0 || FitnessFunctionBalance > 1.0)
                result.AddError("FitnessFunctionBalance must be between 0.0 and 1.0");

            if (MinCorridorWidth < 0.8)
                result.AddWarning($"MinCorridorWidth ({MinCorridorWidth}m) is below accessibility standards (0.8m minimum)");

            if (MinCorridorWidth > 3.0)
                result.AddWarning($"MinCorridorWidth ({MinCorridorWidth}m) is very wide - may waste space");

            if (CirculationPercentage < 0.0 || CirculationPercentage > 0.5)
                result.AddError("CirculationPercentage must be between 0.0 and 0.5");

            // Validate spring system configuration
            var springValidation = SpringSystem.Validate();
            result.Merge(springValidation, "SpringSystem");

            return result;
        }

        /// <summary>
        /// Creates a copy of the configuration.
        /// </summary>
        public AlgorithmConfiguration Clone()
        {
            return new AlgorithmConfiguration
            {
                MaxIterations = MaxIterations,
                MaxAdjacencyDistance = MaxAdjacencyDistance,
                CellSize = CellSize,
                BoundaryOffset = BoundaryOffset,
                MaxRoomAspectRatio = MaxRoomAspectRatio,
                TryRotateBoundary = TryRotateBoundary,
                CorridorMode = CorridorMode,
                RemoveDeadEnds = RemoveDeadEnds,
                RemoveAllCorridors = RemoveAllCorridors,
                CorridorsAsAdditionalSpaces = CorridorsAsAdditionalSpaces,
                MinCorridorWidth = MinCorridorWidth,
                CirculationPercentage = CirculationPercentage,
                AdjustAreaToFitBoundary = AdjustAreaToFitBoundary,
                RandomSeed = RandomSeed,
                FitnessFunctionBalance = FitnessFunctionBalance,
                SpringSystem = SpringSystem.Clone(),
                CustomProperties = new Dictionary<string, object>(CustomProperties)
            };
        }

        /// <summary>
        /// Creates a configuration optimized for small buildings.
        /// </summary>
        public static AlgorithmConfiguration ForSmallBuilding()
        {
            return new AlgorithmConfiguration
            {
                MaxIterations = 150,
                CellSize = 0.5,
                CorridorMode = CorridorMode.OneSide,
                CirculationPercentage = 0.1
            };
        }

        /// <summary>
        /// Creates a configuration optimized for large buildings.
        /// </summary>
        public static AlgorithmConfiguration ForLargeBuilding()
        {
            return new AlgorithmConfiguration
            {
                MaxIterations = 500,
                CellSize = 1.5,
                CorridorMode = CorridorMode.AllSides,
                CirculationPercentage = 0.2
            };
        }

        /// <summary>
        /// Creates a configuration optimized for performance (fast execution).
        /// </summary>
        public static AlgorithmConfiguration ForPerformance()
        {
            return new AlgorithmConfiguration
            {
                MaxIterations = 50,
                CellSize = 2.0,
                RemoveDeadEnds = false,
                SpringSystem = new SpringSystemConfiguration
                {
                    PopulationSize = 10,
                    MaxGenerations = 20
                }
            };
        }

        /// <summary>
        /// Creates a configuration optimized for quality (best results).
        /// </summary>
        public static AlgorithmConfiguration ForQuality()
        {
            return new AlgorithmConfiguration
            {
                MaxIterations = 1000,
                CellSize = 0.5,
                RemoveDeadEnds = true,
                SpringSystem = new SpringSystemConfiguration
                {
                    PopulationSize = 25,
                    MaxGenerations = 100
                }
            };
        }

        public override string ToString()
        {
            return $"AlgorithmConfig: {MaxIterations} iterations, {CellSize}m cells, {CorridorMode} corridors";
        }
    }

    /// <summary>
    /// Configuration for the spring system physics optimization.
    /// </summary>
    public class SpringSystemConfiguration
    {
        /// <summary>
        /// Number of solution candidates in the evolutionary population.
        /// </summary>
        public int PopulationSize { get; set; } = 15;

        /// <summary>
        /// Maximum number of evolutionary generations.
        /// </summary>
        public int MaxGenerations { get; set; } = 50;

        /// <summary>
        /// Probability of mutation for each gene.
        /// </summary>
        public double MutationProbability { get; set; } = 0.3;

        /// <summary>
        /// Magnitude of mutations as fraction of boundary dimensions.
        /// </summary>
        public double MutationStrength { get; set; } = 0.2;

        /// <summary>
        /// Whether to apply spring collision detection to all genes or just the best.
        /// </summary>
        public bool SpringCollisionAllGenes { get; set; } = true;

        /// <summary>
        /// Maximum room proportion threshold for spring system.
        /// </summary>
        public double ProportionThreshold { get; set; } = 2.0;

        /// <summary>
        /// Validates the spring system configuration.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (PopulationSize < 5)
                result.AddError("PopulationSize must be at least 5");

            if (PopulationSize > 100)
                result.AddWarning($"PopulationSize ({PopulationSize}) is very large - may cause performance issues");

            if (MaxGenerations <= 0)
                result.AddError("MaxGenerations must be greater than zero");

            if (MaxGenerations > 1000)
                result.AddWarning($"MaxGenerations ({MaxGenerations}) is very high - may cause performance issues");

            if (MutationProbability < 0.0 || MutationProbability > 1.0)
                result.AddError("MutationProbability must be between 0.0 and 1.0");

            if (MutationStrength <= 0.0 || MutationStrength > 1.0)
                result.AddError("MutationStrength must be between 0.0 and 1.0");

            if (ProportionThreshold < 1.0)
                result.AddError("ProportionThreshold must be at least 1.0");

            return result;
        }

        /// <summary>
        /// Creates a copy of the spring system configuration.
        /// </summary>
        public SpringSystemConfiguration Clone()
        {
            return new SpringSystemConfiguration
            {
                PopulationSize = PopulationSize,
                MaxGenerations = MaxGenerations,
                MutationProbability = MutationProbability,
                MutationStrength = MutationStrength,
                SpringCollisionAllGenes = SpringCollisionAllGenes,
                ProportionThreshold = ProportionThreshold
            };
        }

        public override string ToString()
        {
            return $"SpringSystemConfig: {PopulationSize} population, {MaxGenerations} generations";
        }
    }

    /// <summary>
    /// Corridor generation modes.
    /// </summary>
    public enum CorridorMode
    {
        /// <summary>
        /// No corridors generated.
        /// </summary>
        None,

        /// <summary>
        /// Corridors generated on one side of rooms.
        /// </summary>
        OneSide,

        /// <summary>
        /// Corridors generated on two sides of rooms.
        /// </summary>
        TwoSides,

        /// <summary>
        /// Corridors generated on all sides of rooms.
        /// </summary>
        AllSides,

        /// <summary>
        /// Custom corridor generation logic.
        /// </summary>
        Custom
    }
}