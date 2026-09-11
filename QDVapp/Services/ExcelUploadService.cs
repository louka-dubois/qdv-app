using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QDVapp.Data;
using QDVapp.Models;

namespace QDVapp.Services;

public class ExcelUploadService
{
    private readonly ApplicationDbContext _db;

    public ExcelUploadService(ApplicationDbContext db)
    {
        _db = db;
    }

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public const string TempDataReportKey = "ExcelUploadReport";

    /// <summary>Serializes a report for storage in TempData.</summary>
    public static string EncodeReport(UploadReport report) => JsonSerializer.Serialize(report, ReportJsonOptions);

    /// <summary>Deserializes a report previously stored in TempData (or null if absent/invalid).</summary>
    public static UploadReport? DecodeReport(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<UploadReport>(json, ReportJsonOptions); }
        catch (JsonException) { return null; }
    }

    /// <summary>Describes a table (worksheet) that is not compliant.</summary>
    public sealed record TableIssue(string Sheet, string Reason);

    /// <summary>Result of inspecting an uploaded workbook for compliance.</summary>
    public sealed class UploadReport
    {
        public bool IsEmpty { get; set; }
        public List<string> Sheets { get; set; } = [];
        public List<TableIssue> Issues { get; set; } = [];
        public bool HasIssues => Issues.Count > 0;
    }

    /// <summary>Stores the uploaded file for the user in the database and returns a compliance report.</summary>
    /// <param name="requiredSheet">The worksheet the app expects for this file type, if any.</param>
    public async Task<(bool Success, string? Error, UploadReport? Report)> SaveFileForUserAsync(
        IFormFile file, string userId, string page, string? requiredSheet)
    {
        var report = new UploadReport();

        if (file is null || file.Length == 0)
            return (false, "Aucun fichier fourni.", report);

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return (false, "Seuls les fichiers .xlsx sont acceptés.", report);

        using (var stream = file.OpenReadStream())
        {
            var header = new byte[4];
            if (stream.Read(header, 0, header.Length) != header.Length ||
                header[0] != 0x50 || header[1] != 0x4B) // "PK" — ZIP magic, .xlsx files are ZIP archives
            {
                return (false, "Le fichier n'est pas un fichier Excel (.xlsx) valide.", report);
            }
            stream.Position = 0;
        }

        // Inspect the workbook (before saving) to build the compliance report.
        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            report.Sheets = workbook.Worksheets.Select(w => w.Name).ToList();

            var requiredFound = false;
            foreach (var ws in workbook.Worksheets)
            {
                var isEmpty = ws.RangeUsed() is null ||
                              ws.RangeUsed()!.RowsUsed().Count(r => !IsRowAllEmpty(r)) <= 1; // header only -> empty

                if (string.Equals(ws.Name, requiredSheet, StringComparison.OrdinalIgnoreCase))
                {
                    requiredFound = true;
                    if (isEmpty)
                        report.Issues.Add(new TableIssue(ws.Name, "L'onglet est vide (aucune donnée)."));
                }
                else if (isEmpty && report.Sheets.Count > 1)
                {
                    report.Issues.Add(new TableIssue(ws.Name, "L'onglet est vide (aucune donnée)."));
                }
            }

            // No data anywhere => empty file.
            report.IsEmpty = !report.Sheets.Any() ||
                             workbook.Worksheets.All(w => w.RangeUsed() is null ||
                                                          w.RangeUsed()!.RowsUsed().Count(r => !IsRowAllEmpty(r)) <= 1);

            if (!report.IsEmpty && !string.IsNullOrWhiteSpace(requiredSheet) && !requiredFound)
                report.Issues.Add(new TableIssue(requiredSheet, "L'onglet attendu est absent du fichier."));
        }
        catch (Exception ex)
        {
            return (false, $"Erreur de lecture du fichier Excel: {ex.Message}", report);
        }

        // Reject empty files (do not overwrite the current good data).
        if (report.IsEmpty)
            return (false, "Le fichier Excel est vide. Il n'a pas été enregistré.", report);

        byte[] data;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            data = ms.ToArray();
        }

        try
        {
            var existing = await _db.UploadedExcels
                .FirstOrDefaultAsync(f => f.UserId == userId && f.Page == page);

            if (existing is null)
            {
                _db.UploadedExcels.Add(new UploadedExcel
                {
                    UserId = userId,
                    Page = page,
                    FileName = file.FileName,
                    Data = data,
                    UploadedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.FileName = file.FileName;
                existing.Data = data;
                existing.UploadedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return (false, $"Erreur lors de la sauvegarde: {ex.Message}", report);
        }

        return (true, null, report);
    }

    /// <summary>Returns a user's stored workbook for a page, or null if none has been uploaded.</summary>
    public async Task<byte[]?> GetBytesAsync(string userId, string page)
        => await _db.UploadedExcels
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.Page == page)
            .Select(f => f.Data)
            .FirstOrDefaultAsync();

    private static bool IsRowAllEmpty(IXLRangeRow row)
    {
        return row.Cells().Where(c => !c.IsEmpty()).Count() == 0;
    }
}