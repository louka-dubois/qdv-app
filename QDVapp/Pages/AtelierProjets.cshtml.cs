using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QDVapp.Data;
using QDVapp.Models;
using QDVapp.Services;

namespace QDVapp.Pages;

public class AtelierProjetsModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;
    private readonly ExcelUploadService _uploadService;
    private readonly CorrectionService _correctionService;
    private readonly CorrectedExcelExportService _excelExport;

    public AtelierProjetsModel(IWebHostEnvironment env, ApplicationDbContext db, ExcelUploadService uploadService, CorrectionService correctionService, CorrectedExcelExportService excelExport)
    {
        _env = env;
        _db = db;
        _uploadService = uploadService;
        _correctionService = correctionService;
        _excelExport = excelExport;
    }

    public List<ProjetUsine> Projets { get; set; } = [];
    public List<SkippedRow> SkippedRows { get; set; } = [];
    public int TotalCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ExpandRef { get; set; }
    public Services.ExcelUploadService.UploadReport? UploadReport { get; set; }

    [BindProperty]
    public NewProjectInput Input { get; set; } = new();

    public async Task OnGet([FromQuery(Name = "ref")] string? refe)
    {
        SuccessMessage = TempData["ProjectSuccess"] as string;
        ErrorMessage = TempData["ProjectError"] as string;
        UploadReport = ExcelUploadService.DecodeReport(TempData[ExcelUploadService.TempDataReportKey] as string);

        var filePath = Path.Combine(_env.WebRootPath, "files", "AtelierProjetsUsine", "Atelier - Liste des projets dans usine (1).xlsx");

        if (!System.IO.File.Exists(filePath))
        {
            ErrorMessage = "Fichier Excel introuvable.";
            return;
        }

        try
        {
            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheet("Liste des projets");
            var range = ws.RangeUsed();
            if (range is null)
            {
                ErrorMessage = "La plage de données est vide.";
                return;
            }

            var list = new List<ProjetUsine>();
            var skipped = new List<SkippedRow>();
            foreach (var row in range.RowsUsed().Skip(1))
            {
                var exRow = row.RowNumber();
                var cells = row.Cells().ToList();
                if (cells.Count < 2)
                {
                    skipped.Add(new SkippedRow(exRow, "La ligne contient trop peu de colonnes pour être interprétée."));
                    continue;
                }

                var noCmd = GetString(cells, 1);
                if (string.IsNullOrEmpty(noCmd))
                {
                    skipped.Add(new SkippedRow(exRow, "La valeur « No Projet » est manquante ou vide."));
                    continue;
                }

                var proj = new ProjetUsine { NoCmd = noCmd, BadValues = new Dictionary<string, string>() };
                proj.Statut = GetString(cells, 0);
                proj.RefOF = GetString(cells, 2);
                proj.CodeArticle = GetString(cells, 3);
                proj.Description = GetString(cells, 4);
                proj.CommentaireOF = GetString(cells, 5);
                proj.Vendeur = GetString(cells, 6);
                proj.DateRequise = GetDateTime(cells, 7, "DateRequise", proj.BadValues);
                proj.JrsSTPlanifie = GetDouble(cells, 8, "JrsSTPlanifie", proj.BadValues);
                proj.Planifie = GetDouble(cells, 9, "Planifie", proj.BadValues);
                proj.Fait = GetDouble(cells, 10, "Fait", proj.BadValues);
                proj.DeclAv = GetDouble(cells, 11, "DeclAv", proj.BadValues);
                proj.Restant = GetDouble(cells, 12, "Restant", proj.BadValues);
                proj.TempsTotalFinEstime = GetDouble(cells, 13, "TempsTotalFinEstime", proj.BadValues);
                proj.Diff = GetString(cells, 14);
                proj.PctDiff = GetString(cells, 15);
                proj.NoteFab = GetString(cells, 16);
                list.Add(proj);
            }

            var corrections = await _correctionService.GetAllAsync(CorrectionFields.PageAtelier);
            _correctionService.ApplyAtelier(list, corrections);

            Projets = list;
            SkippedRows = skipped;
            TotalCount = list.Count;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de lecture du fichier: {ex.Message}";
        }

        var manual = _db.Projets.OrderBy(p => p.NoCmd).ToList();
        Projets.AddRange(manual);
        TotalCount += manual.Count;

        if (!string.IsNullOrEmpty(refe) && string.IsNullOrEmpty(ErrorMessage))
        {
            var match = Projets.FirstOrDefault(p => p.RefOF == refe) ?? Projets.FirstOrDefault(p => p.NoCmd == refe);
            if (match is null)
                ErrorMessage = $"Aucun projet trouvé pour la référence \"{refe}\".";
            else
                ExpandRef = match.RefOF ?? (string.IsNullOrEmpty(match.Id) ? match.NoCmd : match.Id);
        }
    }

    public IActionResult OnGetExportCorrected()
    {
        var bytes = _excelExport.ExportProjectsCorrected();
        if (bytes is null)
            return NotFound("Fichier source des projets introuvable.");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Atelier - Liste des projets (corrigé).xlsx");
    }

    public IActionResult OnPostCreate()
    {
        if (string.IsNullOrWhiteSpace(Input.NoCmd))
        {
            TempData["ProjectError"] = "Le numéro de projet est obligatoire.";
            return RedirectToPage();
        }

        var existing = _db.Projets.FirstOrDefault(p => p.Id == Input.Id);
        var isEdit = existing is not null;
        var target = existing ?? new ProjetUsine { Id = Guid.NewGuid().ToString("N"), Manuel = true };

        ApplyInput(target);

        if (!isEdit) _db.Projets.Add(target);
        _db.SaveChanges();

        TempData["ProjectSuccess"] = $"Projet « {target.NoCmd} » {(isEdit ? "modifié" : "ajouté")}.";
        return RedirectToPage();
    }

    public async Task<JsonResult> OnPostCorrect([Microsoft.AspNetCore.Mvc.FromBody] ProjectCorrectionBatch input)
    {
        if (string.IsNullOrWhiteSpace(input?.ProjectKey))
            return new JsonResult(new { success = false, error = "Référence de projet manquante." }) { StatusCode = 400 };

        foreach (var fv in input.Fields ?? [])
        {
            if (string.IsNullOrWhiteSpace(fv?.Field)) continue;

            // Empty value => revert to the Excel/original value (remove the correction).
            if (string.IsNullOrWhiteSpace(fv.Value))
            {
                await _correctionService.RemoveAsync(CorrectionFields.PageAtelier, input.ProjectKey.Trim(), fv.Field);
                continue;
            }

            var error = _correctionService.Validate(CorrectionFields.PageAtelier, fv.Field, fv.Value, out var normalized);
            if (error is not null)
                return new JsonResult(new { success = false, error = $"Champ « {fv.Field} » : {error}" }) { StatusCode = 400 };

            await _correctionService.UpsertAsync(CorrectionFields.PageAtelier, input.ProjectKey.Trim(), fv.Field, normalized);
        }

        return new JsonResult(new { success = true });
    }

    private void ApplyInput(ProjetUsine p)
    {
        p.Statut = NullIfEmpty(Input.Statut);
        p.NoCmd = (Input.NoCmd ?? string.Empty).Trim();
        p.RefOF = NullIfEmpty(Input.RefOF);
        p.CodeArticle = NullIfEmpty(Input.CodeArticle);
        p.Description = NullIfEmpty(Input.Description);
        p.Vendeur = NullIfEmpty(Input.Vendeur);
        p.DateRequise = Input.DateRequise;
        p.Planifie = Input.Planifie;
        p.Fait = Input.Fait;
        p.Restant = Input.Restant;
        p.JrsSTPlanifie = Input.JrsSTPlanifie;
        p.NoteFab = NullIfEmpty(Input.NoteFab);
        p.CommentaireOF = NullIfEmpty(Input.CommentaireOF);

        var plan = Input.Planifie;
        var rest = Input.Restant;
        double? ttf = Input.Fait.HasValue || rest.HasValue ? (Input.Fait ?? 0) + (rest ?? 0) : null;
        double? diff = plan.HasValue && ttf.HasValue ? plan - ttf : null;

        p.TempsTotalFinEstime = ttf;
        p.Diff = diff?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        p.PctDiff = GetPctDiffString(diff, plan);
        p.DeclAv = plan is > 0 && rest.HasValue ? (plan - rest) / plan : plan == 0 ? 0 : null;
    }

    private static string? GetPctDiffString(double? diff, double? plan)
    {
        if (diff.HasValue && plan is > 0)
            return (diff / plan)?.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        if (plan == 0)
            return diff == 0 ? "0" : "Erreur";
        return null;
    }

    public IActionResult OnPostDelete([FromForm] string id)
    {
        var project = _db.Projets.FirstOrDefault(p => p.Id == id);
        if (project is not null)
        {
            _db.Projets.Remove(project);
            _db.SaveChanges();
            TempData["ProjectSuccess"] = "Projet manuel supprimé.";
        }
        else
        {
            TempData["ProjectError"] = "Projet manuel introuvable.";
        }
        return RedirectToPage();
    }

    public JsonResult OnPostUploadAtelier(IFormFile file)
    {
        var (success, error, report) = _uploadService.SaveFileWithReport(
            file,
            "AtelierProjetsUsine",
            "Atelier - Liste des projets dans usine (1).xlsx",
            "Liste des projets");

        if (!success)
            return new JsonResult(new { success = false, error, issues = report?.Issues }) { StatusCode = 400 };

        if (report!.HasIssues)
            TempData[ExcelUploadService.TempDataReportKey] = ExcelUploadService.EncodeReport(report);

        return new JsonResult(new { success = true });
    }

    public async Task<JsonResult> OnPostFixValue([Microsoft.AspNetCore.Mvc.FromBody] ProjectCorrectionInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.NoCmd) || string.IsNullOrWhiteSpace(input?.Field))
            return new JsonResult(new { success = false, error = "Référence de projet manquante. Rechargez la page." }) { StatusCode = 400 };

        var error = _correctionService.Validate(CorrectionFields.PageAtelier, input.Field, input.Value ?? "", out var normalized);
        if (error is not null)
            return new JsonResult(new { success = false, error }) { StatusCode = 400 };

        await _correctionService.UpsertAsync(CorrectionFields.PageAtelier, input.NoCmd.Trim(), input.Field, normalized);
        return new JsonResult(new { success = true, field = input.Field, value = normalized });
    }

    public async Task<JsonResult> OnPostResetCorrection([Microsoft.AspNetCore.Mvc.FromBody] ProjectCorrectionInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.NoCmd) || string.IsNullOrWhiteSpace(input?.Field))
            return new JsonResult(new { success = false, error = "Référence manquante." }) { StatusCode = 400 };

        await _correctionService.RemoveAsync(CorrectionFields.PageAtelier, input.NoCmd.Trim(), input.Field);
        return new JsonResult(new { success = true });
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

public class NewProjectInput
{
    public string? Id { get; set; }
    public string? Statut { get; set; }
    public string? NoCmd { get; set; }
    public string? RefOF { get; set; }
    public string? CodeArticle { get; set; }
    public string? Description { get; set; }
    public string? Vendeur { get; set; }
    public DateTime? DateRequise { get; set; }
    public double? Planifie { get; set; }
    public double? Fait { get; set; }
    public double? Restant { get; set; }
    public double? JrsSTPlanifie { get; set; }
    public string? NoteFab { get; set; }
    public string? CommentaireOF { get; set; }
}

public sealed class ProjectCorrectionBatch
{
    public string? ProjectKey { get; set; }
    public List<CorrectionFieldValue>? Fields { get; set; }
}

public sealed class CorrectionFieldValue
{
    public string? Field { get; set; }
    public string? Value { get; set; }
}

    private static string? GetString(List<IXLCell> cells, int index)
    {
        if (index >= cells.Count) return null;
        var val = cells[index].GetValue<string>();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    private static double? GetDouble(List<IXLCell> cells, int index, string key, IDictionary<string, string> bad)
    {
        if (index >= cells.Count) return null;
        var cell = cells[index];
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number)
            return (double)cell.GetDouble();
        if (double.TryParse(cell.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
            return num;
        bad[key] = cell.GetString();
        return null;
    }

    private static DateTime? GetDateTime(List<IXLCell> cells, int index, string key, IDictionary<string, string> bad)
    {
        if (index >= cells.Count) return null;
        var cell = cells[index];
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime();
        if (cell.DataType == XLDataType.Number)
            return DateTime.FromOADate(cell.GetDouble());
        if (DateTime.TryParse(cell.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        bad[key] = cell.GetString();
        return null;
    }

    public sealed record SkippedRow(int Row, string Reason);

    public sealed record ProjectCorrectionInput
    {
        public string? NoCmd { get; init; }
        public string? Field { get; init; }
        public string? Value { get; init; }
    }
}
