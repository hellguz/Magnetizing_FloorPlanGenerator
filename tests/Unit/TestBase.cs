using System;
using System.Collections.Generic;
using AutoFixture;
using AutoFixture.Xunit2;
using Magnetizing_FPG.Tests.Mocks;
using Moq;
using Xunit.Abstractions;

namespace Magnetizing_FPG.Tests.Unit
{
    /// <summary>
    /// Base class for all unit tests providing common testing infrastructure.
    /// </summary>
    public abstract class TestBase
    {
        protected readonly ITestOutputHelper Output;
        protected readonly Fixture Fixture;
        protected readonly MockRepository MockRepository;

        protected TestBase(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            
            // Configure AutoFixture for consistent test data generation
            Fixture = new Fixture();
            ConfigureFixture(Fixture);
            
            // Configure Moq for strict mocking by default
            MockRepository = new MockRepository(MockBehavior.Strict);
        }

        /// <summary>
        /// Configures AutoFixture with custom settings for our domain.
        /// </summary>
        private void ConfigureFixture(Fixture fixture)
        {
            // Configure reasonable defaults for our domain objects
            fixture.Customize<TestRoomData>(composer => composer
                .With(r => r.Area, () => fixture.Create<double>() % 100 + 10) // 10-110 sq meters
                .With(r => r.IsHall, () => fixture.Create<bool>())
                .With(r => r.Name, () => $"Room {fixture.Create<int>() % 1000}")
                .With(r => r.Id, () => Math.Abs(fixture.Create<int>()) % 100 + 1)); // 1-100

            // Add other domain-specific customizations as needed
        }

        /// <summary>
        /// Creates a mock with the specified behavior and setup.
        /// </summary>
        protected Mock<T> CreateMock<T>(MockBehavior behavior = MockBehavior.Strict) where T : class
        {
            return new Mock<T>(behavior);
        }

        /// <summary>
        /// Creates test data for a simple room program.
        /// </summary>
        protected List<TestRoomData> CreateSimpleRoomProgram()
        {
            return MockGeometryFactory.StandardRoomPrograms.SimpleOffice;
        }

        /// <summary>
        /// Creates test data for a medium complexity room program.
        /// </summary>
        protected List<TestRoomData> CreateMediumRoomProgram()
        {
            return MockGeometryFactory.StandardRoomPrograms.MediumOffice;
        }

        /// <summary>
        /// Logs test information to the test output.
        /// </summary>
        protected void LogTestInfo(string message)
        {
            Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// Logs test performance metrics.
        /// </summary>
        protected void LogPerformanceMetric(string operation, TimeSpan duration, string additionalInfo = null)
        {
            var info = string.IsNullOrEmpty(additionalInfo) ? "" : $" ({additionalInfo})";
            Output.WriteLine($"[PERF] {operation}: {duration.TotalMilliseconds:F2}ms{info}");
        }

        /// <summary>
        /// Measures execution time of an operation.
        /// </summary>
        protected T MeasureTime<T>(string operationName, Func<T> operation)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var result = operation();
                var duration = DateTime.UtcNow - startTime;
                LogPerformanceMetric(operationName, duration);
                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                LogPerformanceMetric($"{operationName} (FAILED)", duration, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Measures execution time of a void operation.
        /// </summary>
        protected void MeasureTime(string operationName, Action operation)
        {
            MeasureTime(operationName, () =>
            {
                operation();
                return (object)null;
            });
        }
    }

    /// <summary>
    /// Custom AutoData attribute for consistent test data generation.
    /// </summary>
    public class AutoMoqDataAttribute : AutoDataAttribute
    {
        public AutoMoqDataAttribute() : base(() =>
        {
            var fixture = new Fixture();
            
            // Add AutoMoq to automatically create mocks for interfaces
            // fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true });
            
            return fixture;
        })
        {
        }
    }

    /// <summary>
    /// Inline auto data for parameterized tests.
    /// </summary>
    public class InlineAutoMoqDataAttribute : InlineAutoDataAttribute
    {
        public InlineAutoMoqDataAttribute(params object[] values) : base(new AutoMoqDataAttribute(), values)
        {
        }
    }
}