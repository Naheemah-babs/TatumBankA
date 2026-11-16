namespace TatumWelcome2.Domain.Models;

public static class DataPlans
{
    public static readonly string[] Networks = { "MTN", "Airtel", "Glo", "9mobile" };

    public static readonly List<DataPlan> DataPlanItems = new List<DataPlan>
    {
        new DataPlan { Name = "1GB (1 day)", Price = 350m },
        new DataPlan { Name = "2GB (7 days)", Price = 700m },
        new DataPlan { Name = "5GB (30 days)", Price = 1500m },
        new DataPlan { Name = "10GB (30 days)", Price = 3000m }
    };
}