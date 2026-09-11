namespace QDVapp.Models;

public class ProjectCorrection
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Page { get; set; } = "";
    public string ProjectKey { get; set; } = "";
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
}
