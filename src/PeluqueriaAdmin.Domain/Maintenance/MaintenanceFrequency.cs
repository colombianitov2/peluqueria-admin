namespace PeluqueriaAdmin.Domain.Maintenance;

public enum MaintenanceFrequency
{
    Once = 0,
    Weekly = 1,
    Biweekly = 2,
    Monthly = 3,
    EveryTwoMonths = 4,
    EveryThreeMonths = 5,
    EverySixMonths = 6,
    Yearly = 7,
    Custom = 8,
}

public enum MaintenanceIntervalUnit
{
    Days = 0,
    Weeks = 1,
    Months = 2,
    Years = 3,
}
