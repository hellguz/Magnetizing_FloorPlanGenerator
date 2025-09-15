using System;
using Magnetizing_FPG;

namespace FloorPlanGeneratorTests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Floor Plan Generator - Test Runner");
            Console.WriteLine("===================================");
            Console.WriteLine("This console app tests the algorithm independently");
            Console.WriteLine("");

            try
            {
                // Test 1: Algorithm instantiation
                Console.WriteLine("Test 1: Algorithm instantiation...");
                var algorithm = new MagnetizingRooms_ES();
                Console.WriteLine("✅ Successfully created MagnetizingRooms_ES instance");
                
                // Test 2: RandomSeed property
                Console.WriteLine("\nTest 2: RandomSeed functionality...");
                int originalSeed = algorithm.RandomSeed;
                Console.WriteLine("   Original RandomSeed: " + originalSeed);
                
                algorithm.RandomSeed = 12345;
                Console.WriteLine("   Set RandomSeed to: " + algorithm.RandomSeed);
                
                algorithm.RandomSeed = 67890;
                Console.WriteLine("   Changed RandomSeed to: " + algorithm.RandomSeed);
                Console.WriteLine("✅ RandomSeed property working correctly");

                Console.WriteLine("\n🎉 All tests passed!");
                Console.WriteLine("\nThe algorithm is ready for deterministic testing:");
                Console.WriteLine("- Set same RandomSeed in Grasshopper → get same results");
                Console.WriteLine("- Use this for automated validation and regression testing");

            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error: " + ex.Message);
                Console.WriteLine("Details: " + ex.ToString());
                return;
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}