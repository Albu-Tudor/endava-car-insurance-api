using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using CarInsurance.Api.Services;

using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Tests
{
    public class CarServiceTests
    {
        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new AppDbContext(options);

            // Seed
            var owner = new Owner { Name = "Test", Email = "test@example.com" };
            db.Owners.Add(owner);
            db.SaveChanges();

            Car car = new Car { Vin = "VINTEST", YearOfManufacture = 2020, OwnerId = owner.Id };
            db.Cars.Add(car);
            db.SaveChanges();

            db.Policies.AddRange(
                new InsurancePolicy { CarId = car.Id, Provider = "Allianz", StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 12, 31) }
            );
            db.SaveChanges();

            return db;
        }

        [Fact]
        public async Task ListCarsAsync_ReturnsProjectedDtoWithOwner()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);

            var cars = await svc.ListCarsAsync();

            Assert.Single(cars);
            var c = cars[0];
            var entity = db.Cars.Include(x => x.Owner).First();

            Assert.Equal(entity.Id, c.Id);
            Assert.Equal(entity.Vin, c.Vin);
            Assert.Equal(entity.YearOfManufacture, c.Year);
            Assert.Equal(entity.OwnerId, c.OwnerId);
            Assert.Equal(entity.Owner.Name, c.OwnerName);
            Assert.Equal(entity.Owner.Email, c.OwnerEmail);
        }

        [Fact]
        public async Task IsInsuranceValid_InclusiveOnStartAndEnd()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);
            var start = new DateOnly(2025, 1, 1);
            var end = new DateOnly(2025, 12, 31);

            Assert.True(await svc.IsInsuranceValidAsync(db.Cars.First().Id, start));
            Assert.True(await svc.IsInsuranceValidAsync(db.Cars.First().Id, end));
            Assert.False(await svc.IsInsuranceValidAsync(db.Cars.First().Id, new DateOnly(2024, 12, 31)));
            Assert.False(await svc.IsInsuranceValidAsync(db.Cars.First().Id, new DateOnly(2026, 1, 1)));
        }

        [Fact]
        public async Task IsInsuranceValid_ThrowsForMissingCar()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.IsInsuranceValidAsync(9999, new DateOnly(2025, 6, 1)));
        }

        [Fact]
        public async Task CreateClaim_ThrowsForMissingCar()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.CreateClaimAsync(9999, new CreateClaimRequest("2025-01-29", "description", 5000)));
        }

        [Fact]
        public async Task CreateClaim_InvalidDateString_Throws()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);
            var carId = db.Cars.First().Id;
            var badDate = new CreateClaimRequest("2025-02-30", "description", 1000);

            await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateClaimAsync(carId, badDate));
        }

        [Fact]
        public async Task CreateClaim_Succeeds_AndPersists()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);
            var carId = db.Cars.First().Id;

            var req = new CreateClaimRequest("2025-06-15", "Accident damage repair", 2500);
            var dto = await svc.CreateClaimAsync(carId, req);

            Assert.NotNull(dto);
            Assert.True(dto.Id > 0);
            Assert.Equal(carId, dto.CarId);
            Assert.Equal("2025-06-15", dto.ClaimDate);
            Assert.Equal("Accident damage repair", dto.Description);
            Assert.Equal(2500, dto.Amount);

            var saved = db.Claims.Single(c => c.Id == dto.Id);
            Assert.Equal(carId, saved.CarId);
            Assert.Equal(new DateOnly(2025, 6, 15), saved.ClaimDate);
            Assert.Equal("Accident damage repair", saved.Description);
            Assert.Equal(2500, saved.Amount);
        }

        [Fact]
        public async Task History_ThrowsForMissingCar()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetCarHistoryAsync(9999));
        }

        [Fact]
        public async Task History_ReturnsPoliciesAndClaims_Sorted()
        {
            using var db = CreateDbContext();
            var svc = new CarService(db);
            var carId = db.Cars.First().Id;

            db.Claims.Add(new InsuranceClaim { CarId = carId, ClaimDate = new DateOnly(2025, 6, 15), Description = "description", Amount = 2000 });
            db.SaveChanges();

            var history = await svc.GetCarHistoryAsync(carId);
            Assert.True(history.Items.Count >= 2);
            var sorted = history.Items.OrderBy(i => i.StartDate).ToList();
            Assert.Equal(sorted.Select(i => i.StartDate), history.Items.Select(i => i.StartDate));
        }
    }
}
