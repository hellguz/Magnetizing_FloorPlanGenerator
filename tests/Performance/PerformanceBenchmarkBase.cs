using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Magnetizing_FPG.Tests.Mocks;

namespace Magnetizing_FPG.Tests.Performance
{
    /// <summary>
    /// Base class for performance benchmarks with common infrastructure.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.Net60)]
    [RPlotExporter]
    [CsvExporter]
    public abstract class PerformanceBenchmarkBase
    {
        protected readonly Random Random = new Random(12345); // Fixed seed for reproducible results
        
        /// <summary>
        /// Measures memory usage before and after an operation.
        /// </summary>
        protected MemoryMeasurement MeasureMemory(Action operation)
        {
            // Force garbage collection to get accurate baseline
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBefore = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();

            operation();

            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);

            return new MemoryMeasurement
            {
                MemoryBefore = memoryBefore,
                MemoryAfter = memoryAfter,
                MemoryDelta = memoryAfter - memoryBefore,
                ExecutionTime = stopwatch.Elapsed
            };
        }

        /// <summary>
        /// Creates performance test scenarios of varying complexity.
        /// </summary>
        protected static IEnumerable<PerformanceScenario> CreatePerformanceScenarios()
        {
            yield return new PerformanceScenario
            {
                Name = "Small_Simple",
                Boundary = MockGeometryFactory.StandardBoundaries.SmallOffice,
                Rooms = MockGeometryFactory.StandardRoomPrograms.SimpleOffice,
                ExpectedTimeMs = 100,
                ExpectedMemoryMb = 5
            };

            yield return new PerformanceScenario
            {
                Name = "Medium_Complex",
                Boundary = MockGeometryFactory.StandardBoundaries.MediumOffice,
                Rooms = MockGeometryFactory.StandardRoomPrograms.MediumOffice,
                ExpectedTimeMs = 1000,
                ExpectedMemoryMb = 15
            };

            yield return new PerformanceScenario
            {
                Name = "Large_Stress",
                Boundary = MockGeometryFactory.StandardBoundaries.LargeOffice,
                Rooms = MockGeometryFactory.StandardRoomPrograms.LargeOffice,
                ExpectedTimeMs = 5000,
                ExpectedMemoryMb = 50
            };
        }

        /// <summary>
        /// Validates performance against expected benchmarks.
        /// </summary>
        protected void ValidatePerformance(PerformanceScenario scenario, MemoryMeasurement measurement)
        {
            var actualTimeMs = measurement.ExecutionTime.TotalMilliseconds;
            var actualMemoryMb = measurement.MemoryDelta / (1024.0 * 1024.0);

            // Allow 20% variance from expected performance
            var timeVarianceThreshold = scenario.ExpectedTimeMs * 0.2;
            var memoryVarianceThreshold = scenario.ExpectedMemoryMb * 0.2;

            if (actualTimeMs > scenario.ExpectedTimeMs + timeVarianceThreshold)
            {
                throw new PerformanceException(
                    $"Scenario '{scenario.Name}' exceeded expected execution time. " +
                    $"Expected: {scenario.ExpectedTimeMs}ms, Actual: {actualTimeMs:F2}ms");
            }

            if (actualMemoryMb > scenario.ExpectedMemoryMb + memoryVarianceThreshold)
            {
                throw new PerformanceException(
                    $"Scenario '{scenario.Name}' exceeded expected memory usage. " +
                    $"Expected: {scenario.ExpectedMemoryMb}MB, Actual: {actualMemoryMb:F2}MB");
            }
        }

        /// <summary>
        /// Logs performance metrics in a structured format.
        /// </summary>
        protected void LogPerformanceMetrics(string scenarioName, MemoryMeasurement measurement)
        {
            Console.WriteLine($"=== Performance Metrics: {scenarioName} ===");
            Console.WriteLine($"Execution Time: {measurement.ExecutionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"Memory Before: {measurement.MemoryBefore / (1024.0 * 1024.0):F2}MB");
            Console.WriteLine($"Memory After: {measurement.MemoryAfter / (1024.0 * 1024.0):F2}MB");
            Console.WriteLine($"Memory Delta: {measurement.MemoryDelta / (1024.0 * 1024.0):F2}MB");
            Console.WriteLine("==========================================");
        }
    }

    /// <summary>
    /// Custom configuration for performance benchmarks.
    /// </summary>
    public class PerformanceBenchmarkConfig : ManualConfig
    {
        public PerformanceBenchmarkConfig()
        {
            AddLogger(ConsoleLogger.Default);
            AddExporter(BenchmarkDotNet.Exporters.CsvExporter.Default);
            AddExporter(BenchmarkDotNet.Exporters.HtmlExporter.Default);
            
            // Configure for consistent results
            WithOptions(ConfigOptions.DisableOptimizationsValidator);
        }
    }

    /// <summary>
    /// Represents a performance test scenario.
    /// </summary>
    public class PerformanceScenario
    {
        public string Name { get; set; }
        public object Boundary { get; set; }
        public List<TestRoomData> Rooms { get; set; }
        public double ExpectedTimeMs { get; set; }
        public double ExpectedMemoryMb { get; set; }
    }

    /// <summary>
    /// Memory measurement result.
    /// </summary>
    public class MemoryMeasurement
    {
        public long MemoryBefore { get; set; }
        public long MemoryAfter { get; set; }
        public long MemoryDelta { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    /// <summary>
    /// Exception thrown when performance expectations are not met.
    /// </summary>
    public class PerformanceException : Exception
    {
        public PerformanceException(string message) : base(message) { }
        public PerformanceException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Runner for executing all performance benchmarks.
    /// </summary>
    public static class PerformanceBenchmarkRunner
    {
        public static void RunAllBenchmarks()
        {
            Console.WriteLine("Starting Performance Benchmark Suite...");
            
            // Run BenchmarkDotNet benchmarks
            var summary = BenchmarkRunner.Run<AlgorithmPerformanceBenchmarks>();
            
            Console.WriteLine("Performance benchmarks completed.");
            Console.WriteLine($"Results saved to: {Path.Combine(Environment.CurrentDirectory, "BenchmarkDotNet.Artifacts")}");
        }

        public static void RunQuickPerformanceCheck()
        {
            Console.WriteLine("Running quick performance validation...");
            
            var benchmarks = new AlgorithmPerformanceBenchmarks();
            benchmarks.Setup();
            
            // Run a subset of tests for quick validation
            var scenarios = PerformanceBenchmarkBase.CreatePerformanceScenarios().Take(2);
            
            foreach (var scenario in scenarios)
            {
                try
                {
                    Console.WriteLine($"Testing scenario: {scenario.Name}");
                    // Execute scenario and validate performance
                    // (implementation would depend on the actual algorithm interfaces)
                }
                catch (PerformanceException ex)
                {
                    Console.WriteLine($"Performance issue detected: {ex.Message}");
                }
            }
            
            Console.WriteLine("Quick performance check completed.");
        }
    }

    /// <summary>
    /// Specific benchmark class for algorithm performance testing.
    /// This will be implemented once we have the core algorithm services.
    /// </summary>
    [Config(typeof(PerformanceBenchmarkConfig))]
    public class AlgorithmPerformanceBenchmarks : PerformanceBenchmarkBase
    {
        private List<PerformanceScenario> _scenarios;

        [GlobalSetup]
        public void Setup()
        {
            _scenarios = CreatePerformanceScenarios().ToList();
        }

        [Benchmark]
        [Arguments("Small_Simple")]
        [Arguments("Medium_Complex")]
        [Arguments("Large_Stress")]
        public void RunAlgorithmBenchmark(string scenarioName)
        {
            var scenario = _scenarios.First(s => s.Name == scenarioName);
            
            // TODO: Implement once we have the algorithm services extracted
            // var result = MeasureMemory(() => 
            // {
            //     // Execute magnetizing algorithm with scenario data
            // });
            
            // ValidatePerformance(scenario, result);
            // LogPerformanceMetrics(scenarioName, result);
        }
    }
}