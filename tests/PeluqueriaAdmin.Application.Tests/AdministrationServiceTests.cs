using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class AdministrationServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GenerateScheduledRecords_IsAtomicAndIdempotent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var service = CreateService(repository, settingsRepository);
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        Obligation obligation = Obligation.Create(
            "Internet", ObligationType.Service, new DateOnly(2026, 7, 5),
            Money.FromDecimal(50m), RecurrenceFrequency.Monthly, UtcNow);
        await service.AddAsync(person, cancellationToken);
        await service.AddAsync(obligation, cancellationToken);

        AdministrationData first = await service.GenerateScheduledRecordsAsync(
            new DateOnly(2026, 8, 18), cancellationToken);
        AdministrationData second = await service.GenerateScheduledRecordsAsync(
            new DateOnly(2026, 8, 18), cancellationToken);

        Assert.Single(first.WeeklyRates);
        Assert.Equal(2, first.WeeklyCharges.Count);
        Assert.Equal(2, first.Obligations.Count);
        Assert.Equal(first.WeeklyCharges.Count, second.WeeklyCharges.Count);
        Assert.Equal(first.Obligations.Count, second.Obligations.Count);
        Assert.True(repository.LastSaveWasSingleTransaction);
    }

    [Fact]
    public async Task RegisterPayment_AllowsAdvanceAtZeroDebtAndPersistsAfterServiceRestart()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var service = CreateService(repository, settingsRepository);
        LocalUsePerson person = LocalUsePerson.Create("Luis", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddAsync(person, cancellationToken);
        await service.GenerateScheduledRecordsAsync(new DateOnly(2026, 7, 1), cancellationToken);

        await service.RegisterLocalUsePaymentAsync(
            person.Id,
            new DateOnly(2026, 7, 2),
            Money.FromDecimal(1000m),
            cancellationToken);

        var restartedService = CreateService(repository, settingsRepository);
        AdministrationData reloaded = await restartedService.LoadAsync(cancellationToken);
        Assert.Single(reloaded.LocalUsePayments);
        Assert.Equal(100_000, reloaded.LocalUsePayments.Single().Amount.MinorUnits);
        WorkerAccountBalance balance = WeeklyChargeCalculator.CalculateAccount(
            person,
            reloaded.WeeklyCharges,
            reloaded.LocalUsePayments,
            reloaded.WeeklyRates,
            new DateOnly(2026, 7, 2));
        Assert.Equal(100_000, balance.Credit.MinorUnits);
        Assert.Equal(800, balance.NextRequiredPaymentAmount?.MinorUnits);
    }

    [Fact]
    public async Task RegisterPayment_WhenTransactionFailsDoesNotChangeDebtOrCreateHistory()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var service = CreateService(repository, settingsRepository);
        DateOnly today = new(2026, 7, 20);
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 6, 16), null, UtcNow);
        await service.AddLocalUsePersonAsync(person, today, cancellationToken);
        AdministrationData before = await service.LoadAsync(cancellationToken);
        Assert.Equal(4_800, WeeklyChargeCalculator.CalculateDebt(
            before.WeeklyCharges, before.LocalUsePayments, today).MinorUnits);
        repository.FailNextSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterLocalUsePaymentAsync(
            person.Id, new DateOnly(2026, 7, 19), Money.FromDecimal(12m),
            cancellationToken, completedDraftKey: "pago-prueba", description: "No debe persistir"));

        AdministrationData after = await service.LoadAsync(cancellationToken);
        Assert.Empty(after.LocalUsePayments);
        Assert.Equal(before.ActivityRecords.Count, after.ActivityRecords.Count);
        Assert.Equal(4_800, WeeklyChargeCalculator.CalculateDebt(
            after.WeeklyCharges, after.LocalUsePayments, today).MinorUnits);
    }

    [Fact]
    public async Task AddPerson_GeneratesChargesAndAllowsPaymentInSameSession()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);

        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 18), cancellationToken);
        await service.RegisterLocalUsePaymentAsync(
            person.Id, new DateOnly(2026, 7, 18), Money.FromDecimal(12m), cancellationToken);

        AdministrationData data = await service.LoadAsync(cancellationToken);
        Assert.Equal(2, data.WeeklyCharges.Count);
        Assert.Single(data.LocalUsePayments);
        Assert.Equal(1_200, WeeklyChargeCalculator.CalculateDebt(
            data.WeeklyCharges, data.LocalUsePayments).MinorUnits);
    }

    [Fact]
    public async Task UpdatePerson_RejectsDateChangeThatInvalidatesPaidPeriods()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 18), cancellationToken);
        await service.RegisterLocalUsePaymentAsync(
            person.Id, new DateOnly(2026, 7, 8), Money.FromDecimal(12m), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateLocalUsePersonAsync(
                person.Id, "Ana", new DateOnly(2026, 7, 2), null,
                new DateOnly(2026, 7, 18), cancellationToken));

        Assert.Contains("ya tiene pagos", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddRecurringObligation_GeneratesCurrentOccurrencesWithoutRestart()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Obligation obligation = Obligation.Create(
            "Internet", ObligationType.Service, new DateOnly(2026, 5, 31),
            Money.FromDecimal(50m), RecurrenceFrequency.Monthly, UtcNow);

        await service.AddObligationAsync(obligation, new DateOnly(2026, 7, 31), cancellationToken);
        await service.GenerateScheduledRecordsAsync(new DateOnly(2026, 7, 31), cancellationToken);

        Assert.Equal(
            [new DateOnly(2026, 5, 31), new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 31)],
            (await service.LoadAsync(cancellationToken)).Obligations.Select(item => item.DueDate).Order());
    }

    [Fact]
    public async Task CloseMonth_PreventsDuplicateConfirmedClose()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(
            repository,
            new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Collaborator collaborator = Collaborator.Create("Ana", new DateOnly(2026, 1, 1), null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorProfitShareAsync(
            collaborator.Id, Percentage.FromPercent(20m), cancellationToken);
        Guid collaboratorId = collaborator.Id;
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);

        var first = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [collaboratorId], cancellationToken);

        Assert.Single(first.Participants);
        Assert.Equal(1_000, first.Participants[0].Amount.MinorUnits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [collaboratorId], cancellationToken));
    }

    [Fact]
    public async Task ReopenWithoutPayments_InvalidatesParticipantsAndAllowsCleanNewClose()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Collaborator firstCollaborator = Collaborator.Create("Ana", new DateOnly(2026, 1, 1), null, UtcNow);
        Collaborator secondCollaborator = Collaborator.Create("Beto", new DateOnly(2026, 1, 1), null, UtcNow);
        await service.AddAsync(firstCollaborator, cancellationToken);
        await service.AddAsync(secondCollaborator, cancellationToken);
        await service.UpdateCollaboratorProfitShareAsync(firstCollaborator.Id, Percentage.FromPercent(10m), cancellationToken);
        await service.UpdateCollaboratorProfitShareAsync(secondCollaborator.Id, Percentage.FromPercent(10m), cancellationToken);
        Guid first = firstCollaborator.Id;
        Guid second = secondCollaborator.Id;
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);
        var original = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [first, second], cancellationToken);

        await service.ReopenMonthAsync(original.Close.Id, cancellationToken);
        InvalidOperationException paymentError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterDistributionPaymentAsync(
                original.Participants[0].Id,
                new DateOnly(2026, 7, 31),
                Money.FromDecimal(1m),
                cancellationToken));
        var replacement = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [first, second], cancellationToken);
        AdministrationData active = await service.LoadAsync(cancellationToken);

        Assert.Contains("ya no está disponible", paymentError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, replacement.Participants.Count);
        Assert.Equal(replacement.Close.FundMinorUnits, replacement.Participants.Sum(item => item.Amount.MinorUnits));
        Assert.Equal(2, active.MonthlyCloseParticipants.Count);
        Assert.Single(active.MonthlyCloses, item => item.IsConfirmed);
    }

    [Fact]
    public async Task ReopenWithDistributionPayment_IsBlocked()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Collaborator collaborator = Collaborator.Create("Ana", new DateOnly(2026, 1, 1), null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorProfitShareAsync(collaborator.Id, Percentage.FromPercent(20m), cancellationToken);
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);
        var closed = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [collaborator.Id], cancellationToken);
        await service.RegisterDistributionPaymentAsync(
            closed.Participants[0].Id, new DateOnly(2026, 7, 31), Money.FromDecimal(10m), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReopenMonthAsync(closed.Close.Id, cancellationToken));

        Assert.Contains("pagos de distribución", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(closed.Close.IsConfirmed);
    }

    [Fact]
    public async Task ProductNames_AreUniqueIgnoringCase()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        await service.AddProductAsync(
            Product.Create("Agua", ProductCategory.ProductForSale, "unidad", UtcNow), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddProductAsync(
                Product.Create(" agua ", ProductCategory.ProductForSale, "unidad", UtcNow), cancellationToken));

        Assert.Contains("Ya existe", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteParentWithRelations_IsRejectedWithClearMessage()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Obligation obligation = Obligation.Create(
            "Impuesto", ObligationType.Tax, new DateOnly(2026, 7, 1),
            Money.FromDecimal(20m), RecurrenceFrequency.None, UtcNow);
        await service.AddAsync(obligation, cancellationToken);
        await service.AddAsync(ObligationPayment.Create(
            obligation.Id, new DateOnly(2026, 7, 2), Money.FromDecimal(5m), UtcNow), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(obligation, cancellationToken));

        Assert.Contains("pagos registrados", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(obligation.IsDeleted);
    }

    [Fact]
    public async Task DeleteProtectedParentsAndComputedRecords_IsRejected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));

        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 8), cancellationToken);

        Product product = Product.Create("Agua", ProductCategory.ProductForSale, "unidad", UtcNow);
        await service.AddProductAsync(product, cancellationToken);
        await service.AddInventoryMovementAsync(InventoryMovement.Initial(
            product.Id, new DateOnly(2026, 7, 1), Quantity.Positive(2m), Money.FromDecimal(10m), UtcNow),
            cancellationToken);

        Collaborator collaborator = Collaborator.Create("Luis", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorProfitShareAsync(collaborator.Id, Percentage.FromPercent(20m), cancellationToken);
        var closed = await service.CloseMonthAsync(
            new YearMonth(2026, 7),
            new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0),
            Percentage.FromPercent(20m),
            [collaborator.Id],
            cancellationToken);

        InvalidOperationException personError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(person, cancellationToken));
        InvalidOperationException productError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(product, cancellationToken));
        InvalidOperationException collaboratorError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(collaborator, cancellationToken));
        InvalidOperationException closeError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(closed.Close, cancellationToken));
        InvalidOperationException participantError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(closed.Participants[0], cancellationToken));

        Assert.Contains("cuotas", personError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("movimientos", productError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cierres", collaboratorError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reapertura", closeError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no se eliminan", participantError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InventoryCorrection_RejectsAChronologyThatWouldMakeStockNegative()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Product product = Product.Create("Agua", ProductCategory.ProductForSale, "unidad", UtcNow);
        InventoryMovement initial = InventoryMovement.Initial(
            product.Id, new DateOnly(2026, 7, 1), Quantity.Positive(10m), Money.FromDecimal(50m), UtcNow);
        InventoryMovement sale = InventoryMovement.Sale(
            product.Id, new DateOnly(2026, 7, 2), Quantity.Positive(5m), Money.FromDecimal(5m),
            Money.FromDecimal(5m), 10m, UtcNow.AddMinutes(1));
        await service.AddProductAsync(product, cancellationToken);
        await service.AddInventoryMovementAsync(initial, cancellationToken);
        await service.AddInventoryMovementAsync(sale, cancellationToken);
        initial.Correct(
            new DateOnly(2026, 7, 3), 10m, null, Money.FromDecimal(50m), UtcNow.AddMinutes(2));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateInventoryMovementAsync(initial, cancellationToken));

        Assert.Contains("negativo", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmedCloseSnapshot_SurvivesPercentageChangeAndReopenRestoresDynamicCalculation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        var month = new YearMonth(2026, 7);
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);
        var closed = await service.CloseMonthAsync(
            month, input, Percentage.FromPercent(20m), [Guid.NewGuid()], cancellationToken);
        AdministrationData data = await service.LoadAsync(cancellationToken);

        MonthlySummaryResult snapshot = AdministrationReports.MonthlySummary(
            data, Percentage.FromPercent(50m), month);
        await service.ReopenMonthAsync(closed.Close.Id, cancellationToken);
        MonthlySummaryResult dynamic = AdministrationReports.MonthlySummary(
            await service.LoadAsync(cancellationToken), Percentage.FromPercent(50m), month);

        Assert.Equal(1_000, snapshot.CollaboratorFundMinorUnits);
        Assert.Equal(0, dynamic.CollaboratorFundMinorUnits);
    }

    [Fact]
    public async Task HomeCapacityAndAnnualBreakdown_FollowApprovedRules()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 18), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Servicio vencido", ObligationType.Service, new DateOnly(2026, 6, 1), Money.FromDecimal(10m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Impuesto del mes", ObligationType.Tax, new DateOnly(2026, 7, 25), Money.FromDecimal(20m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Otra", ObligationType.OtherRecurring, new DateOnly(2026, 7, 20), Money.FromDecimal(30m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Servicio futuro", ObligationType.Service, new DateOnly(2026, 8, 1), Money.FromDecimal(40m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        AdministrationData data = await service.LoadAsync(cancellationToken);

        HomeDashboard home = HomeDashboardCalculator.Calculate(
            data, Percentage.FromPercent(20m), new DateOnly(2026, 7, 18));
        ChairCapacity capacity = HomeDashboardCalculator.Capacity(data, 0, new DateOnly(2026, 7, 18));
        AnnualAdministrationReport annual = AdministrationReports.Annual(
            data, Percentage.FromPercent(20m), 2026);

        Assert.Equal(["Servicio vencido", "Impuesto del mes"], home.Obligations.Select(item => item.Name));
        Assert.Equal(1, capacity.Overcapacity);
        Assert.Equal(5_000, annual.Expenses.ServicesMinorUnits);
        Assert.Equal(2_000, annual.Expenses.TaxesMinorUnits);
        Assert.Equal(3_000, annual.Expenses.OtherObligationsMinorUnits);
        Assert.Contains(annual.Indicator, new[] { "Positivo", "Negativo" });
    }

    [Fact]
    public async Task Chairs_EnforceAvailabilityAndOneToOneAssignment()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly today = new(2026, 7, 18);
        Chair firstChair = Chair.Create("Silla 1", today, null, UtcNow);
        LocalUsePerson firstPerson = LocalUsePerson.Create("Ana", today, null, UtcNow);
        LocalUsePerson secondPerson = LocalUsePerson.Create("Luis", today, null, UtcNow);
        await service.AddChairAsync(firstChair, cancellationToken);
        await service.AddLocalUsePersonWithChairAsync(firstPerson, firstChair.Id, today, cancellationToken);

        InvalidOperationException unavailable = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddLocalUsePersonWithChairAsync(secondPerson, firstChair.Id, today, cancellationToken));
        Assert.Equal("No hay sillas disponibles. Debes crear un espacio para una silla adicional.", unavailable.Message);

        Chair secondChair = Chair.Create("Silla 2", today, null, UtcNow);
        await service.AddChairAsync(secondChair, cancellationToken);
        await service.AddLocalUsePersonWithChairAsync(secondPerson, secondChair.Id, today, cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignChairAsync(firstPerson.Id, secondChair.Id, today, cancellationToken));

        AdministrationData data = await service.LoadAsync(cancellationToken);
        Assert.Equal(2, data.Chairs.Count);
        Assert.Equal(2, data.Chairs.Select(x => x.AssignedPersonId).Distinct().Count());
        Assert.DoesNotContain(data.Collaborators, _ => true);
    }

    [Fact]
    public async Task ChairAssignment_SameChairIsNoOp_ChangeAndWithdrawalAreSingleAtomicEvents()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly today = new(2026, 7, 18);
        Chair first = Chair.Create("Silla 1", today, null, UtcNow);
        Chair second = Chair.Create("Silla 2", today, null, UtcNow);
        LocalUsePerson worker = LocalUsePerson.Create("Ana", today, null, UtcNow);
        await service.AddChairAsync(first, cancellationToken);
        await service.AddChairAsync(second, cancellationToken);
        await service.AddLocalUsePersonWithChairAsync(worker, first.Id, today, cancellationToken);
        int initialEvents = repository.Entities
            .OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>()
            .Count();

        await service.AssignChairAsync(worker.Id, first.Id, today, cancellationToken);
        Assert.Equal(initialEvents, repository.Entities
            .OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>()
            .Count());

        await service.AssignChairAsync(worker.Id, second.Id, today, cancellationToken);
        Assert.Null(first.AssignedPersonId);
        Assert.Equal(worker.Id, second.AssignedPersonId);
        Assert.Equal(initialEvents + 3, repository.Entities
            .OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>()
            .Count());

        await service.AssignChairAsync(worker.Id, null, today, cancellationToken);
        Assert.Null(first.AssignedPersonId);
        Assert.Null(second.AssignedPersonId);
        Assert.Equal(initialEvents + 5, repository.Entities
            .OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>()
            .Count());
        Assert.True(repository.LastSaveWasSingleTransaction);
    }

    [Fact]
    public async Task ScheduledRefresh_ReleasesChairAfterHairdresserExit()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Chair chair = Chair.Create("Silla 1", new DateOnly(2026, 7, 1), null, UtcNow);
        LocalUsePerson person = LocalUsePerson.Create(
            "Ana", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 17), UtcNow);
        await service.AddChairAsync(chair, cancellationToken);
        await service.AddLocalUsePersonWithChairAsync(person, chair.Id, new DateOnly(2026, 7, 17), cancellationToken);

        AdministrationData data = await service.GenerateScheduledRecordsAsync(
            new DateOnly(2026, 7, 18), cancellationToken);

        Assert.Null(Assert.Single(data.Chairs).AssignedPersonId);
        Assert.Equal(1, HomeDashboardCalculator.Capacity(data, new DateOnly(2026, 7, 18)).Available);
    }

    [Fact]
    public async Task LogicalWorkerDeletion_PreservesHistoryAndReleasesChairAtomically()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly date = new(2026, 7, 1);
        Chair chair = Chair.Create("Silla 1", date, null, UtcNow);
        LocalUsePerson worker = LocalUsePerson.Create("Ana", date, null, UtcNow);
        await service.AddChairAsync(chair, cancellationToken);
        await service.AddLocalUsePersonWithChairAsync(worker, chair.Id, date.AddDays(7), cancellationToken);
        await service.RegisterLocalUsePaymentAsync(
            worker.Id, date.AddDays(7), Money.FromDecimal(12m), cancellationToken);

        await service.DeleteLocalUsePersonAsync(worker.Id, cancellationToken);

        Assert.True(worker.IsDeleted);
        Assert.Null(chair.AssignedPersonId);
        Assert.Single(repository.Entities.OfType<WeeklyCharge>());
        Assert.Single(repository.Entities.OfType<LocalUsePayment>());
        Assert.Contains(repository.Entities.OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>(),
            item => item.Action == "Eliminación lógica de trabajador");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogicalChairDeletion_WorksForEmptyAndOccupiedChair(bool occupied)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly date = new(2026, 7, 1);
        Chair chair = Chair.Create("Silla", date, null, UtcNow);
        await service.AddChairAsync(chair, cancellationToken);
        LocalUsePerson? worker = null;
        if (occupied)
        {
            worker = LocalUsePerson.Create("Ana", date, null, UtcNow);
            await service.AddLocalUsePersonWithChairAsync(worker, chair.Id, date, cancellationToken);
        }

        await service.DeleteChairAsync(chair.Id, cancellationToken);

        Assert.True(chair.IsDeleted);
        Assert.Null(chair.AssignedPersonId);
        if (worker is not null)
        {
            Assert.False(worker.IsDeleted);
        }
    }

    [Fact]
    public async Task LogicalCollaboratorDeletion_PreservesContributionsAndClosedMonth()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly date = new(2026, 7, 1);
        Collaborator collaborator = Collaborator.Create("Inversionista", date, null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorProfitShareAsync(collaborator.Id, Percentage.FromPercent(20m), cancellationToken);
        await service.AddCollaboratorContributionAsync(
            CollaboratorContribution.Create(collaborator.Id, date, Money.FromDecimal(100m), null, UtcNow),
            cancellationToken);
        await service.CloseMonthAsync(
            new YearMonth(2026, 7),
            new MonthlySummaryInput(10_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            Percentage.FromPercent(20m), [collaborator.Id], cancellationToken);

        await service.DeleteCollaboratorAsync(collaborator.Id, cancellationToken);

        Assert.True(collaborator.IsDeleted);
        Assert.Single(repository.Entities.OfType<CollaboratorContribution>());
        Assert.Single(repository.Entities.OfType<MonthlyCloseParticipant>());
        Assert.Contains(repository.Entities.OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>(),
            item => item.Action == "Eliminación lógica de colaborador");
    }

    [Fact]
    public async Task CompletingRecurringMaintenance_GeneratesOneNextOccurrenceIdempotently()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        MaintenanceRecord first = MaintenanceRecord.Schedule(
            "Silla", "Preventivo", new DateOnly(2026, 1, 31), Money.FromDecimal(10m),
            MaintenanceFrequency.Monthly, null, null, UtcNow);
        await service.ScheduleMaintenanceAsync(first, cancellationToken);

        await service.CompleteMaintenanceAsync(
            first.Id, new DateOnly(2026, 1, 31), Money.FromDecimal(9m), cancellationToken: cancellationToken);
        await service.CompleteMaintenanceAsync(
            first.Id, new DateOnly(2026, 1, 31), Money.FromDecimal(9m), cancellationToken: cancellationToken);

        MaintenanceRecord[] records = repository.Entities.OfType<MaintenanceRecord>().ToArray();
        Assert.Equal(2, records.Length);
        Assert.Equal(new DateOnly(2026, 2, 28), records.Single(item => item.OccurrenceNumber == 1).ScheduledDate);
        Assert.Equal(900, first.ActualCost?.MinorUnits);
    }

    [Fact]
    public async Task StoppingPendingMaintenance_PreservesCompletedOccurrence()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        MaintenanceRecord first = MaintenanceRecord.Schedule(
            "Silla", "Preventivo", new DateOnly(2026, 1, 1), null,
            MaintenanceFrequency.Weekly, null, null, UtcNow);
        await service.ScheduleMaintenanceAsync(first, cancellationToken);
        await service.CompleteMaintenanceAsync(
            first.Id, new DateOnly(2026, 1, 1), Money.FromDecimal(5m), cancellationToken: cancellationToken);
        MaintenanceRecord next = repository.Entities.OfType<MaintenanceRecord>().Single(item => item.OccurrenceNumber == 1);

        await service.StopFutureMaintenanceAsync(next.Id, cancellationToken);

        Assert.False(first.IsDeleted);
        Assert.True(next.IsDeleted);
        Assert.Equal(500, first.GoalAmountFor(new YearMonth(2026, 1)).MinorUnits);
    }

    [Fact]
    public async Task Sale_UsesProductDefaultPriceUpdatesStockAndRejectsOverselling()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly date = new(2026, 7, 18);
        Product product = Product.Create(
            "Cera", ProductCategory.OtherProductForSale, "unidad", UtcNow,
            Money.FromDecimal(3m), "Venta mostrador");
        await service.AddProductWithInitialStockAsync(
            product, date, Quantity.Positive(5m), Money.FromDecimal(1m), null, cancellationToken);

        InventoryMovement sale = await service.RegisterSaleAsync(
            product.Id, date, Quantity.Positive(2m), "Venta confirmada", cancellationToken);
        AdministrationData afterSale = await service.LoadAsync(cancellationToken);

        Assert.Equal(600, sale.CashAmount?.MinorUnits);
        Assert.Equal(3m, InventoryCalculator.CurrentQuantity(
            afterSale.InventoryMovements.Where(x => x.ProductId == product.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterSaleAsync(
            product.Id, date, Quantity.Positive(4m), null, cancellationToken));

        InventoryMovement purchase = await service.RegisterPurchaseAsync(
            product.Id, date, Quantity.Positive(2m), Money.FromDecimal(1.25m), "Reposición", cancellationToken);
        Assert.Equal(250, purchase.CashAmount?.MinorUnits);
    }

    [Fact]
    public async Task SuggestedChairPrice_AddsUnofficialExpensesAndExcludesChairPaymentsFromIncomeOffset()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly today = new(2026, 7, 18);
        Chair chair = Chair.Create("Silla 1", today, null, UtcNow);
        LocalUsePerson person = LocalUsePerson.Create("Ana", today, null, UtcNow);
        await service.AddChairAsync(chair, cancellationToken);
        await service.AddLocalUsePersonWithChairAsync(person, chair.Id, today, cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Arriendo", ObligationType.Service, today, Money.FromDecimal(500m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        await service.AddAsync(FinancialEntry.CreateIncome(today, "Otro ingreso", Money.FromDecimal(50m), UtcNow), cancellationToken);
        await service.AddUnofficialExpenseAsync(UnofficialExpense.Create(
            "Gasto conocido", Money.FromDecimal(100m), today, null, UtcNow), cancellationToken);

        SuggestedChairPrice result = SuggestedChairPriceCalculator.Calculate(
            await service.LoadAsync(cancellationToken), Money.FromDecimal(12m),
            new YearMonth(2026, 7), today);

        Assert.Equal(1, result.OccupiedChairs);
        Assert.Equal(50_000, result.OfficialGoalMinorUnits);
        Assert.Equal(10_000, result.UnofficialExpensesMinorUnits);
        Assert.Equal(5_000, result.ExpectedNonChairIncomeMinorUnits);
        Assert.Equal(55_000, result.SuggestedMonthlyPerChairMinorUnits);
        Assert.Equal(12_692, result.SuggestedWeeklyPerChairMinorUnits);
        Assert.Contains("No resta los pagos", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollaboratorContribution_IsPersistedButExcludedFromOperationalResults()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly date = new(2026, 7, 18);
        Collaborator collaborator = Collaborator.Create("Inversionista", date, null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.AddCollaboratorContributionAsync(
            CollaboratorContribution.Create(
                collaborator.Id, date, Money.FromDecimal(1_000m), "Capital", UtcNow),
            cancellationToken);
        await service.AddAsync(
            FinancialEntry.CreateIncome(date, "Ingreso operativo", Money.FromDecimal(100m), UtcNow),
            cancellationToken);

        AdministrationData data = await service.LoadAsync(cancellationToken);
        MonthlySummaryResult summary = AdministrationReports.MonthlySummary(
            data, Percentage.FromPercent(20m), new YearMonth(2026, 7));

        Assert.Single(data.CollaboratorContributions);
        Assert.Equal(100_000, data.CollaboratorContributions[0].Amount.MinorUnits);
        Assert.Equal(10_000, summary.IncomeMinorUnits);
        Assert.Equal(2_000, summary.CollaboratorFundMinorUnits);
    }

    [Fact]
    public async Task SaveSettings_RecordsNewRateOnlyWhenFeeChanges()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var useCase = new SaveSettingsUseCase(
            settingsRepository,
            repository,
            new FixedTimeProvider(new DateTimeOffset(UtcNow.AddDays(1))));

        await useCase.ExecuteAsync(new SaveSettingsRequest(15m, 20m, 0m, 0, "USD"), cancellationToken);
        await useCase.ExecuteAsync(new SaveSettingsRequest(15m, 25m, 0m, 0, "USD"), cancellationToken);

        Assert.Single(repository.Entities.OfType<WeeklyRate>());
        Assert.Equal(1_500, repository.Entities.OfType<WeeklyRate>().Single().Amount.MinorUnits);
        Assert.Equal(2_500, settingsRepository.Settings.CollaboratorProfit.BasisPoints);
    }

    [Fact]
    public async Task ChairHistory_IsStoredForChairWithWorkerNamesWithoutDuplicates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly today = new(2026, 7, 18);
        Chair first = Chair.Create("Silla Uno", today, null, UtcNow);
        Chair second = Chair.Create("Silla Dos", today, null, UtcNow);
        LocalUsePerson worker = LocalUsePerson.Create("María", today, null, UtcNow);
        await service.AddChairAsync(first, cancellationToken);
        await service.AddChairAsync(second, cancellationToken);
        await service.AddLocalUsePersonWithChairAsync(worker, first.Id, today, cancellationToken);
        await service.AssignChairAsync(worker.Id, second.Id, today, cancellationToken);
        await service.AssignChairAsync(worker.Id, null, today, cancellationToken);

        var chairEvents = repository.Entities
            .OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>()
            .Where(item => item.EntityId == first.Id || item.EntityId == second.Id)
            .ToArray();

        Assert.Equal(4, chairEvents.Length);
        Assert.All(chairEvents, item => Assert.Contains("María", item.Summary, StringComparison.Ordinal));
        Assert.Contains(chairEvents, item => item.Summary.Contains("Silla Uno", StringComparison.Ordinal));
        Assert.Contains(chairEvents, item => item.Summary.Contains("Silla Dos", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sale_UsesEditedUnitPriceAndRejectsQuantityAboveStock()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        DateOnly today = new(2026, 7, 18);
        Product product = Product.Create(
            "Tratamiento", ProductCategory.ProductForSale, "unidad", UtcNow, Money.FromDecimal(30m));
        await service.AddProductWithInitialStockAsync(
            product, today, Quantity.Positive(3m), Money.FromDecimal(10m), cancellationToken: cancellationToken);

        InventoryMovement sale = await service.RegisterSaleAsync(
            product.Id, today, Quantity.Positive(3m), Money.FromDecimal(35m), cancellationToken: cancellationToken);

        Assert.Equal(10_500, sale.CashAmount?.MinorUnits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterSaleAsync(
            product.Id, today, Quantity.Positive(1m), Money.FromDecimal(35m), cancellationToken: cancellationToken));
        Assert.Single(repository.Entities.OfType<InventoryMovement>(), item => item.Type == InventoryMovementType.Sale);
    }

    private static AdministrationService CreateService(
        FakeAdministrationRepository repository,
        FakeSettingsRepository settingsRepository) => new(
            repository,
            settingsRepository,
            new FixedTimeProvider(new DateTimeOffset(UtcNow)));

    private sealed class FakeSettingsRepository(GeneralSettings settings) : ISettingsRepository
    {
        public GeneralSettings Settings { get; } = settings;

        public Task<GeneralSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task SaveAsync(GeneralSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAdministrationRepository : IAdministrationRepository
    {
        public List<AuditableEntity> Entities { get; } = [];

        public bool LastSaveWasSingleTransaction { get; private set; }

        public bool FailNextSave { get; set; }

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdministrationData(
                Entities.OfType<LocalUsePerson>().Where(Active).ToArray(),
                Entities.OfType<WeeklyRate>().Where(Active).ToArray(),
                Entities.OfType<WeeklyCharge>().Where(Active).ToArray(),
                Entities.OfType<LocalUsePayment>().Where(Active).ToArray(),
                Entities.OfType<Product>().Where(Active).ToArray(),
                Entities.OfType<InventoryMovement>().Where(Active).ToArray(),
                Entities.OfType<MonthlyRestockPlan>().Where(Active).ToArray(),
                Entities.OfType<FinancialEntry>().Where(Active).ToArray(),
                Entities.OfType<Obligation>().Where(Active).ToArray(),
                Entities.OfType<ObligationPayment>().Where(Active).ToArray(),
                Entities.OfType<MaintenanceRecord>().Where(Active).ToArray(),
                Entities.OfType<Collaborator>().Where(Active).ToArray(),
                Entities.OfType<MonthlyClose>().Where(Active).ToArray(),
                Entities.OfType<MonthlyCloseParticipant>().Where(Active).ToArray(),
                Entities.OfType<DistributionPayment>().Where(Active).ToArray(),
                Entities.OfType<Chair>().Where(Active).ToArray(),
                Entities.OfType<PeluqueriaAdmin.Domain.Activity.ActivityRecord>().Where(Active).ToArray(),
                Entities.OfType<UnofficialExpense>().Where(Active).ToArray(),
                Entities.OfType<CollaboratorContribution>().Where(Active).ToArray()));

        public Task SaveAsync(
            IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates,
            CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("Falla transaccional simulada.");
            }

            Entities.AddRange(additions);
            LastSaveWasSingleTransaction = true;
            return Task.CompletedTask;
        }

        public Task SaveCompletingDraftAsync(
            IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates,
            string completedDraftKey,
            CancellationToken cancellationToken = default) => SaveAsync(additions, updates, cancellationToken);

        public Task SaveSettingsAndRateAsync(
            GeneralSettings settings,
            WeeklyRate? newRate,
            CancellationToken cancellationToken = default)
        {
            if (newRate is not null)
            {
                Entities.Add(newRate);
            }

            LastSaveWasSingleTransaction = true;
            return Task.CompletedTask;
        }

        public Task SaveSettingsAndRateCompletingDraftAsync(
            GeneralSettings settings,
            WeeklyRate? newRate,
            string completedDraftKey,
            CancellationToken cancellationToken = default) =>
            SaveSettingsAndRateAsync(settings, newRate, cancellationToken);

        private static bool Active(AuditableEntity entity) => !entity.IsDeleted;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
