namespace VisitService.CModels;

public class VisittApiOptions
{
    public const string SectionName = "VisittApi";

    public string GraphQlEndpoint { get; set; }  = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;  
}