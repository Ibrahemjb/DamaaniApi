namespace DammaniAPI.Features.Billing;

public class BillingPlan
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";
    public decimal PriceUsd { get; set; }
    public decimal PriceIls { get; set; }
    public int MonthlyCardLimit { get; set; }
    public int MaxUsers { get; set; }
    public bool HasBranches { get; set; }
    public bool HasExport { get; set; }
    public bool HasCustomTemplates { get; set; }
    public bool HasPrintableLabels { get; set; }
    public bool ShowDamaaniBranding { get; set; }
    public bool HasAnalytics { get; set; }
    public int SortOrder { get; set; }
}
