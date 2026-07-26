using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class InventorySalesRegressionTests
{
    [Fact]
    public async Task QuesoRancio_ForSaleAppearsWithStockAndDefaultPrice_SellsOnceAndPersists()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            paths.EnsureDirectories();
            var factory = new TestFactory(paths.DatabaseFilePath);
            var clock = new FixedClock(new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero));
            var backup = new DatabaseBackupService(factory, paths, clock);
            await new DatabaseInitializer(factory, paths, clock, backup).InitializeAsync(cancellationToken);
            var service = new AdministrationService(
                new EfAdministrationRepository(factory), new EfSettingsRepository(factory), clock);
            DateOnly date = new(2026, 7, 19);
            Product product = Product.Create(
                "Queso rancio",
                ProductCategory.FoodOrDrinkForSale,
                "unidad",
                clock.GetUtcNow().UtcDateTime,
                Money.FromDecimal(4.50m));

            await service.AddProductWithInitialStockAsync(
                product,
                date,
                Quantity.Positive(3m),
                Money.FromDecimal(2m),
                cancellationToken: cancellationToken);

            AdministrationData afterCreate = await service.LoadAsync(cancellationToken);
            Product selected = Assert.Single(afterCreate.Products, item => item.Name == "Queso rancio");
            Assert.True(selected.IsForSale);
            Assert.Equal(450, selected.DefaultSalePrice?.MinorUnits);
            Assert.Equal(3m, InventoryCalculator.CurrentQuantity(
                afterCreate.InventoryMovements.Where(item => item.ProductId == selected.Id)));

            InventoryMovement sale = await service.RegisterSaleAsync(
                selected.Id, date, Quantity.Positive(1m), "Venta de regresión", cancellationToken);
            Assert.Equal(450, sale.CashAmount?.MinorUnits);

            AdministrationData afterSale = await service.LoadAsync(cancellationToken);
            Assert.Equal(2m, InventoryCalculator.CurrentQuantity(
                afterSale.InventoryMovements.Where(item => item.ProductId == selected.Id)));
            Assert.Single(afterSale.InventoryMovements, item => item.ProductId == selected.Id
                && item.Type == InventoryMovementType.Sale);

            SqliteConnection.ClearAllPools();
            var reopened = new AdministrationService(
                new EfAdministrationRepository(new TestFactory(paths.DatabaseFilePath)),
                new EfSettingsRepository(new TestFactory(paths.DatabaseFilePath)),
                clock);
            AdministrationData persisted = await reopened.LoadAsync(cancellationToken);
            Product persistedProduct = Assert.Single(persisted.Products, item => item.Name == "Queso rancio");
            Assert.True(persistedProduct.IsForSale);
            Assert.Equal(2m, InventoryCalculator.CurrentQuantity(
                persisted.InventoryMovements.Where(item => item.ProductId == persistedProduct.Id)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestFactory(string path) : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = Create(path);
        public PeluqueriaDbContext CreateDbContext() => new(options);
        public Task<PeluqueriaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
        private static DbContextOptions<PeluqueriaDbContext> Create(string path)
        {
            var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(builder, path);
            return builder.Options;
        }
    }
}
