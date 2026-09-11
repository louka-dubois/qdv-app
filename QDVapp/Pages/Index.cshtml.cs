using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QDVapp.Models;
using QDVapp.Services;
using System.Security.Claims;
using System.Text.Json;

namespace QDVapp.Pages;

public class IndexModel : PageModel
{
    private readonly ExcelUploadService _uploadService;

    public IndexModel(ExcelUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public int TotalProjets { get; set; }
    public int ProjetsEnRetard { get; set; }
    public double HeuresRestantes { get; set; }
    public double HeuresRestantesEnRetard { get; set; }
    public string? ErrorMessage { get; set; }
    public Services.ExcelUploadService.UploadReport? UploadReport { get; set; }

    public string StatusLabelsJson { get; set; } = "[]";
    public string StatusCountsJson { get; set; } = "[]";
    public string VendeurLabelsJson { get; set; } = "[]";
    public string VendeurHoursJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        UploadReport = ExcelUploadService.DecodeReport(TempData[ExcelUploadService.TempDataReportKey] as string);

        var bytes = await _uploadService.GetBytesAsync(CurrentUserId, CorrectionFields.PageAtelier);
        if (bytes is null)
        {
            ErrorMessage = "Fichier Excel introuvable.";
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet("Liste des projets");
            var range = ws.RangeUsed();
            if (range is null) return;

            var now = DateTime.Today;
            var statusCounts = new Dictionary<string, int>();
            var vendeurHours = new Dictionary<string, double>();

            foreach (var row in range.RowsUsed().Skip(1))
            {
                var cells = row.Cells().ToList();
                if (cells.Count < 2) continue;

                var noCmd = GetString(cells, 1);
                if (string.IsNullOrEmpty(noCmd)) continue;

                TotalProjets++;

                var statut = GetString(cells, 0);
                if (!string.IsNullOrEmpty(statut))
                    statusCounts[statut] = statusCounts.GetValueOrDefault(statut) + 1;

                var vendeur = GetString(cells, 6);
                var restant = GetDouble(cells, 12) ?? 0;

                HeuresRestantes += restant;

                if (!string.IsNullOrEmpty(vendeur))
                    vendeurHours[vendeur] = vendeurHours.GetValueOrDefault(vendeur) + restant;

                var dateRequise = GetDateTime(cells, 7);
                if (dateRequise.HasValue && dateRequise < now && restant > 0)
                {
                    ProjetsEnRetard++;
                    HeuresRestantesEnRetard += restant;
                }
            }

            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            StatusLabelsJson = JsonSerializer.Serialize(statusCounts.Keys.ToList(), opts);
            StatusCountsJson = JsonSerializer.Serialize(statusCounts.Values.ToList(), opts);

            var topVendeurs = vendeurHours.OrderByDescending(kv => kv.Value).Take(10).ToList();
            VendeurLabelsJson = JsonSerializer.Serialize(topVendeurs.Select(kv => kv.Key).ToList(), opts);
            VendeurHoursJson = JsonSerializer.Serialize(topVendeurs.Select(kv => Math.Round(kv.Value, 1)).ToList(), opts);
        }
        catch
        {
            ErrorMessage = "Erreur lors de la lecture du fichier.";
        }
    }

    public async Task<JsonResult> OnPostUploadAtelier(IFormFile file)
    {
        var (success, error, report) = await _uploadService.SaveFileForUserAsync(
            file,
            CurrentUserId,
            CorrectionFields.PageAtelier,
            "Liste des projets");

        if (!success)
            return new JsonResult(new { success = false, error, issues = report?.Issues }) { StatusCode = 400 };

        if (report!.HasIssues)
            TempData[ExcelUploadService.TempDataReportKey] = ExcelUploadService.EncodeReport(report);

        return new JsonResult(new { success = true });
    }

    public async Task<JsonResult> OnPostUploadAvancement(IFormFile file)
    {
        var (success, error, report) = await _uploadService.SaveFileForUserAsync(
            file,
            CurrentUserId,
            CorrectionFields.PageAvancement,
            "Liste des heures à faire");

        if (!success)
            return new JsonResult(new { success = false, error, issues = report?.Issues }) { StatusCode = 400 };

        if (report!.HasIssues)
            TempData[ExcelUploadService.TempDataReportKey] = ExcelUploadService.EncodeReport(report);

        return new JsonResult(new { success = true });
    }

    static string GetString(List<IXLCell> cells, int index)
    {
        return index < cells.Count ? cells[index].GetValue<string>().Trim() : "";
    }

    static DateTime? GetDateTime(List<IXLCell> cells, int index)
    {
        if (index >= cells.Count) return null;
        var val = cells[index].GetValue<string>().Trim();
        if (string.IsNullOrEmpty(val)) return null;
        if (DateTime.TryParse(val, out var dt)) return dt;
        return null;
    }

    static double? GetDouble(List<IXLCell> cells, int index)
    {
        if (index >= cells.Count) return null;
        var val = cells[index].GetValue<string>().Trim();
        if (string.IsNullOrEmpty(val)) return null;
        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num)) return num;
        return null;
    }
}
