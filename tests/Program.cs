using System;

namespace Magnetizing_FPG.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🔬 Magnetizing Floor Plan Generator - End-to-End Tests");
            Console.WriteLine("======================================================");
            Console.WriteLine();

            try
            {
                Console.WriteLine("Running basic algorithm structure tests...\n");

                // Run simple tests that don't require Rhino environment
                var basicTests = new SimpleMockTests();
                basicTests.RunBasicAlgorithmTests();

                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine("💡 Note: Full E2E tests require Rhino environment to run.");
                Console.WriteLine("   For complete testing, use the algorithm within Grasshopper.");
                Console.WriteLine("   These basic tests verify core algorithm structure and determinism.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Fatal error running tests: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }

            Console.WriteLine("\nPress any key to exit...");
            try
            {
                Console.ReadKey();
            }
            catch
            {
                // Ignore if running in environment without console input
            }
        }
    }
}