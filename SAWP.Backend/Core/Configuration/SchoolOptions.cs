namespace Core.Configuration;

public class SchoolOptions
{
    public const string SectionName = "School";
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";
    public string KitchenFinalizeTime { get; set; } = "10:30";
}
