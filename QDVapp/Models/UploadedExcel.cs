namespace QDVapp.Models;

public class UploadedExcel
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Page { get; set; } = "";
    public string FileName { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}