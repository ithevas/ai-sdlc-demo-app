namespace InvoiceApp.Domain;

public static class ApprovalLimits
{
    public const decimal ManagerMaxAmount = 50_000m;
    public const decimal DirectorMaxAmount = 500_000m;
}

public static class Roles
{
    public const string Manager = "Manager";
    public const string Director = "Director";
}
