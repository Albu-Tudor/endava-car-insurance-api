using CarInsurance.Api.Data;
using CarInsurance.Api.Models;
using CarInsurance.Api.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace CarInsurance.Api.Tests
{
    public class PolicyExpirationHostedServiceTests
    {
        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private static Mock<ILogger<ScopedProcessingService>> CreateMockLogger()
        {
            return new Mock<ILogger<ScopedProcessingService>>();
        }

        [Fact]
        public async Task ScopedProcessingService_DoWork_WithExistingState_UpdatesState()
        {
            // Arrange
            using var db = CreateDbContext();
            var logger = CreateMockLogger();
            var service = new ScopedProcessingService(db, logger.Object);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var initialDate = new DateOnly(2024, 1, 1);
            db.ProcessingStates.Add(new ProcessingState 
            { 
                Key = "PolicyExpirationChecker.LastRunUtc", 
                Value = initialDate 
            });
            await db.SaveChangesAsync();

            // Act
            var task = service.DoWork(cancellationTokenSource.Token);
            await Task.Delay(200); // Let it run and complete one iteration
            cancellationTokenSource.Cancel();

            // Assert
            var state = await db.ProcessingStates.FirstOrDefaultAsync(s => s.Key == "PolicyExpirationChecker.LastRunUtc");
            Assert.NotNull(state);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.ToLocalTime().Date), state.Value);
        }

        [Fact]
        public async Task ScopedProcessingService_DoWork_WithExpiredPolicies_LogsExpiredPolicies()
        {
            // Arrange
            using var db = CreateDbContext();
            var logger = CreateMockLogger();
            var service = new ScopedProcessingService(db, logger.Object);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var owner = new Owner { Name = "Test Owner", Email = "test@example.com" };
            db.Owners.Add(owner);
            await db.SaveChangesAsync();

            var car = new Car { Vin = "VINTEST", YearOfManufacture = 2020, OwnerId = owner.Id };
            db.Cars.Add(car);
            await db.SaveChangesAsync();

            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var twoDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));

            db.Policies.AddRange(
                new InsurancePolicy 
                { 
                    CarId = car.Id, 
                    Provider = "Allianz", 
                    StartDate = twoDaysAgo.AddDays(-30), 
                    EndDate = yesterday 
                },
                new InsurancePolicy 
                { 
                    CarId = car.Id, 
                    Provider = "AXA", 
                    StartDate = twoDaysAgo.AddDays(-30), 
                    EndDate = twoDaysAgo 
                }
            );

            var threeDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
            db.ProcessingStates.Add(new ProcessingState 
            { 
                Key = "PolicyExpirationChecker.LastRunUtc", 
                Value = threeDaysAgo 
            });
            await db.SaveChangesAsync();

            // Act
            var task = service.DoWork(cancellationTokenSource.Token);
            await Task.Delay(200);
            cancellationTokenSource.Cancel();

            // Assert
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Policy") && v.ToString()!.Contains("expired")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeast(2));
        }

        [Fact]
        public async Task ScopedProcessingService_DoWork_WithNoExpiredPolicies_DoesNotLog()
        {
            // Arrange
            using var db = CreateDbContext();
            var logger = CreateMockLogger();
            var service = new ScopedProcessingService(db, logger.Object);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var owner = new Owner { Name = "Test Owner", Email = "test@example.com" };
            db.Owners.Add(owner);
            await db.SaveChangesAsync();

            var car = new Car { Vin = "VINTEST", YearOfManufacture = 2020, OwnerId = owner.Id };
            db.Cars.Add(car);
            await db.SaveChangesAsync();

            var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
            var nextWeek = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

            db.Policies.AddRange(
                new InsurancePolicy 
                { 
                    CarId = car.Id, 
                    Provider = "Allianz", 
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow), 
                    EndDate = tomorrow 
                },
                new InsurancePolicy 
                { 
                    CarId = car.Id, 
                    Provider = "AXA", 
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow), 
                    EndDate = nextWeek 
                }
            );

            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            db.ProcessingStates.Add(new ProcessingState 
            { 
                Key = "PolicyExpirationChecker.LastRunUtc", 
                Value = yesterday 
            });
            await db.SaveChangesAsync();

            // Act
            var task = service.DoWork(cancellationTokenSource.Token);
            await Task.Delay(200); // Let it run and complete one iteration
            cancellationTokenSource.Cancel();

            // Assert
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Policy") && v.ToString()!.Contains("expired")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
    }
}