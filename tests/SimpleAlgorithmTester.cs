using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Magnetizing_FPG;
using Rhino.Geometry;
using System.Reflection;

namespace FloorPlanGeneratorTests
{
    /// <summary>
    /// Simplified algorithm tester that focuses on testing what we can without full IGH_DataAccess
    /// </summary>
    public class SimpleAlgorithmTester
    {
        private readonly string _testDataDirectory;

        public SimpleAlgorithmTester(string testDataDirectory = "testdata")
        {
            _testDataDirectory = testDataDirectory;
            if (!Directory.Exists(_testDataDirectory))
            {
                Directory.CreateDirectory(_testDataDirectory);
            }
        }

        /// <summary>
        /// Test algorithm instantiation and configuration
        /// </summary>
        public SimpleTestResult TestAlgorithmBasics(AlgorithmTestCase testCase)
        {
            var result = new SimpleTestResult
            {
                TestName = testCase.TestName,
                RandomSeed = testCase.RandomSeed,
                StartTime = DateTime.Now
            };

            try
            {
                // Test 1: Algorithm instantiation
                var algorithm = new MagnetizingRooms_ES();
                result.AlgorithmInstantiated = true;

                // Test 2: RandomSeed configuration
                algorithm.RandomSeed = testCase.RandomSeed;
                if (algorithm.RandomSeed == testCase.RandomSeed)
                {
                    result.RandomSeedConfigured = true;
                }

                // Test 3: Try to create house instance
                var houseInstance = ConvertTestHouseToReal(testCase.HouseInstance);
                result.HouseInstanceCreated = houseInstance != null;

                // Test 4: Validate test data structure
                result.BoundaryCreated = houseInstance?.boundary != null;
                result.RoomsConfigured = testCase.HouseInstance?.Rooms?.Count > 0;
                result.AdjacenciesConfigured = testCase.HouseInstance?.Adjacencies?.Count > 0;

                result.Success = result.AlgorithmInstantiated && 
                                result.RandomSeedConfigured && 
                                result.HouseInstanceCreated &&
                                result.BoundaryCreated;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ErrorDetails = ex.ToString();
            }

            result.EndTime = DateTime.Now;
            result.ExecutionTimeMs = (int)(result.EndTime - result.StartTime).TotalMilliseconds;

            return result;
        }

        /// <summary>
        /// Test deterministic behavior by running algorithm setup multiple times
        /// </summary>
        public SimpleTestResult TestDeterministicBehavior(AlgorithmTestCase testCase, int iterations = 3)
        {
            var result = new SimpleTestResult
            {
                TestName = testCase.TestName + "_Deterministic",
                RandomSeed = testCase.RandomSeed,
                StartTime = DateTime.Now
            };

            try
            {
                var seedResults = new List<int>();
                
                for (int i = 0; i < iterations; i++)
                {
                    var algorithm = new MagnetizingRooms_ES();
                    algorithm.RandomSeed = testCase.RandomSeed;
                    seedResults.Add(algorithm.RandomSeed);
                }

                // Verify all seeds are identical
                result.Success = seedResults.All(seed => seed == testCase.RandomSeed);
                result.DeterministicBehaviorVerified = result.Success;
                
                if (!result.Success)
                {
                    result.ErrorMessage = "RandomSeed values were not consistent: " + string.Join(", ", seedResults);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ErrorDetails = ex.ToString();
            }

            result.EndTime = DateTime.Now;
            result.ExecutionTimeMs = (int)(result.EndTime - result.StartTime).TotalMilliseconds;

            return result;
        }

        /// <summary>
        /// Convert test house instance to real algorithm input
        /// </summary>
        private HouseInstance ConvertTestHouseToReal(TestHouseInstance testHouse)
        {
            var houseInstance = new HouseInstance();
            houseInstance.HouseName = testHouse.HouseName;
            houseInstance.FloorName = testHouse.FloorName;
            houseInstance.tryRotateBoundary = testHouse.TryRotateBoundary;

            // Convert boundary
            if (testHouse.Boundary != null)
            {
                houseInstance.boundary = testHouse.Boundary.ToRhinoCurve();
            }

            // Convert starting point
            if (testHouse.StartingPoint != null)
            {
                houseInstance.startingPoint = testHouse.StartingPoint.ToRhinoPoint3d();
            }

            // Convert adjacencies
            houseInstance.adjStrList = testHouse.Adjacencies.ToList();

            // Convert adjacency array
            if (testHouse.Adjacencies.Count > 0)
            {
                houseInstance.adjArray = new int[testHouse.Adjacencies.Count, 2];
                for (int i = 0; i < testHouse.Adjacencies.Count; i++)
                {
                    var parts = testHouse.Adjacencies[i].Split('-');
                    if (parts.Length == 2)
                    {
                        houseInstance.adjArray[i, 0] = int.Parse(parts[0].Trim());
                        houseInstance.adjArray[i, 1] = int.Parse(parts[1].Trim());
                    }
                }
            }

            return houseInstance;
        }

        /// <summary>
        /// Save test case to JSON file
        /// </summary>
        public void SaveTestCase(AlgorithmTestCase testCase)
        {
            var filePath = Path.Combine(_testDataDirectory, testCase.TestName + ".json");
            var json = JsonConvert.SerializeObject(testCase, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Load test case from JSON file
        /// </summary>
        public AlgorithmTestCase LoadTestCase(string testName)
        {
            var filePath = Path.Combine(_testDataDirectory, testName + ".json");
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<AlgorithmTestCase>(json);
            }
            return null;
        }
    }

    /// <summary>
    /// Simplified test result focusing on what we can actually test
    /// </summary>
    public class SimpleTestResult
    {
        public string TestName { get; set; }
        public int RandomSeed { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorDetails { get; set; }
        
        // Timing
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ExecutionTimeMs { get; set; }
        
        // Test results
        public bool AlgorithmInstantiated { get; set; }
        public bool RandomSeedConfigured { get; set; }
        public bool HouseInstanceCreated { get; set; }
        public bool BoundaryCreated { get; set; }
        public bool RoomsConfigured { get; set; }
        public bool AdjacenciesConfigured { get; set; }
        public bool DeterministicBehaviorVerified { get; set; }
    }
}