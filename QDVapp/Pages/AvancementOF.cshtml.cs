using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QDVapp.Data;
using QDVapp.Models;
using QDVapp.Services;
using System.Security.Claims;

namespace QDVapp.Pages;

public class AvancementOFModel : PageModel
{
    private readonly ExcelUploadService _uploadService;
    private readonly CorrectionService _correctionService;
    private readonly ApplicationDbContext _db;
    private readonly CorrectedExcelExportService _excelExport;

    public AvancementOFModel(ExcelUploadService uploadService, CorrectionService correctionService, ApplicationDbContext db, CorrectedExcelExportService excelExport)
    {
        _uploadService = uploadService;
        _correctionService = correctionService;
        _db = db;
        _excelExport = excelExport;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public List<AvancementOF> Ordres { get; set; } = [];
    public List<SkippedRow> SkippedRows { get; set; } = [];
    public int TotalCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ExpandRef { get; set; }
    public Services.ExcelUploadService.UploadReport? UploadReport { get; set; }

    [BindProperty]
    public NewOFInput Input { get; set; } = new();

    public async Task OnGet([Microsoft.AspNetCore.Mvc.FromQuery(Name = "ref")] string? refe)
    {
        SuccessMessage = TempData["OFSuccess"] as string;
        ErrorMessage = TempData["OFError"] as string;
        UploadReport = ExcelUploadService.DecodeReport(TempData[ExcelUploadService.TempDataReportKey] as string);

        var userId = CurrentUserId;
        var bytes = await _uploadService.GetBytesAsync(userId, CorrectionFields.PageAvancement);
        if (bytes is null)
        {
            ErrorMessage = "Fichier Excel introuvable.";
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet(1);
            var range = ws.RangeUsed();
            if (range is null)
            {
                ErrorMessage = "La plage de données est vide.";
                return;
            }
            var rows = range.RowsUsed();

            var list = new List<AvancementOF>();
            var skipped = new List<SkippedRow>();

            foreach (var row in rows.Skip(1))
            {
                var exRow = row.RowNumber();
                var cells = row.Cells().ToList();
                if (cells.Count < 2)
                {
                    skipped.Add(new SkippedRow(exRow, "La ligne contient trop peu de colonnes pour être interprétée."));
                    continue;
                }

                var nbre = cells[0].GetValue<string>();
                if (nbre.StartsWith("Nbre", StringComparison.OrdinalIgnoreCase))
                    break;

                var projet = GetString(cells, 0);
                if (string.IsNullOrEmpty(projet))
                {
                    skipped.Add(new SkippedRow(exRow, "La valeur « Projet » est manquante ou vide."));
                    continue;
                }

                var ordre = new AvancementOF { BadValues = new Dictionary<string, string>() };
                ordre.Projet = GetString(cells, 0);
                ordre.RefOF = GetString(cells, 1);
                ordre.StatutOF = GetString(cells, 2);
                ordre.DescriptionArticle = GetString(cells, 3);
                ordre.Priorite = GetInt(cells, 4, "Priorite", ordre.BadValues);
                ordre.RequisFab = GetDateTime(cells, 5, "RequisFab", ordre.BadValues);
                ordre.RequisFinal = GetDateTime(cells, 6, "RequisFinal", ordre.BadValues);
                ordre.Scie = GetString(cells, 7);
                ordre.RobotPlasma = GetString(cells, 8);
                ordre.LaserTube = GetString(cells, 9);
                ordre.TablePlasma = GetString(cells, 10);
                ordre.TableLaser = GetString(cells, 11);
                ordre.Pliage = GetString(cells, 12);
                ordre.Roulage = GetString(cells, 13);
                ordre.Machinage = GetString(cells, 14);
                ordre.STMachine = GetString(cells, 15);
                ordre.Montage = GetString(cells, 16);
                ordre.TuyauterieMontage = GetString(cells, 17);
                ordre.Soudage = GetString(cells, 18);
                ordre.TuyauterieSoudage = GetString(cells, 19);
                ordre.SoudageRobot = GetString(cells, 20);
                ordre.STMontageSoudage = GetString(cells, 21);
                ordre.Inspection = GetString(cells, 22);
                ordre.STInspection = GetString(cells, 23);
                ordre.Reparation = GetString(cells, 24);
                ordre.Peinture = GetString(cells, 25);
                ordre.STPeinture = GetString(cells, 26);
                ordre.Emballage = GetString(cells, 27);
                ordre.CommentaireInspection = GetString(cells, 28);
                ordre.CommentaireOF = GetString(cells, 29);
                list.Add(ordre);
            }

            var corrections = await _correctionService.GetAllAsync(userId, CorrectionFields.PageAvancement);
            _correctionService.ApplyAvancement(list, corrections);

            Ordres = list;
            SkippedRows = skipped;
            TotalCount = list.Count;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de lecture du fichier: {ex.Message}";
        }

        var manual = _db.ManuelOFs.Where(m => m.UserId == userId).OrderBy(m => m.Projet).ThenBy(m => m.RefOF).ToList();
        foreach (var m in manual)
        {
            Ordres.Add(ToAvancement(m));
        }
        TotalCount += manual.Count;

        if (!string.IsNullOrEmpty(refe) && string.IsNullOrEmpty(ErrorMessage))
        {
            var match = Ordres.FirstOrDefault(o => o.RefOF == refe) ?? Ordres.FirstOrDefault(o => o.Projet == refe);
            if (match is null)
                ErrorMessage = $"Aucun ordre de fabrication trouvé pour la référence \"{refe}\".";
            else
                ExpandRef = match.RefOF ?? (string.IsNullOrEmpty(match.Id) ? match.Projet : match.Id);
        }
    }

    public async Task<IActionResult> OnGetExportCorrected()
    {
        var userId = CurrentUserId;
        var bytes = await _uploadService.GetBytesAsync(userId, CorrectionFields.PageAvancement);
        var output = await _excelExport.ExportOfsCorrectedAsync(bytes, userId);
        if (output is null)
            return NotFound("Fichier source des OF introuvable.");
        return File(output, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Avancement des OF - Heures à faire (corrigé).xlsx");
    }

    public IActionResult OnPostCreate()
    {
        if (string.IsNullOrWhiteSpace(Input.RefOF) && string.IsNullOrWhiteSpace(Input.Projet))
        {
            TempData["OFError"] = "Le projet ou la référence OF est obligatoire.";
            return RedirectToPage();
        }

        var existing = _db.ManuelOFs.FirstOrDefault(m => m.Id == Input.Id && m.UserId == CurrentUserId);
        var target = existing;
        var isEdit = existing is not null;
        if (target is null && !string.IsNullOrWhiteSpace(Input.Id))
        {
            TempData["OFError"] = "OF introuvable ou déjà supprimé.";
            return RedirectToPage();
        }
        target ??= new ManuelOF { Id = Guid.NewGuid().ToString("N"), UserId = CurrentUserId, Manuel = true };

        ApplyInput(target);

        if (!isEdit) _db.ManuelOFs.Add(target);
        _db.SaveChanges();

        TempData["OFSuccess"] = $"OF « {(string.IsNullOrWhiteSpace(target.RefOF) ? target.Projet : target.RefOF)} » {(isEdit ? "modifié" : "ajouté")}.";
        return RedirectToPage();
    }

    public IActionResult OnPostDelete([FromForm] string id)
    {
        var of = _db.ManuelOFs.FirstOrDefault(m => m.Id == id && m.UserId == CurrentUserId);
        if (of is not null)
        {
            _db.ManuelOFs.Remove(of);
            _db.SaveChanges();
            TempData["OFSuccess"] = "OF manuel supprimé.";
        }
        else
        {
            TempData["OFError"] = "OF manuel introuvable.";
        }
        return RedirectToPage();
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

    public async Task<JsonResult> OnPostFixValue([Microsoft.AspNetCore.Mvc.FromBody] AvancementCorrectionInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.Projet) || string.IsNullOrWhiteSpace(input?.Field))
            return new JsonResult(new { success = false, error = "Référence de projet manquante. Rechargez la page." }) { StatusCode = 400 };

        var error = _correctionService.Validate(CorrectionFields.PageAvancement, input.Field, input.Value ?? "", out var normalized);
        if (error is not null)
            return new JsonResult(new { success = false, error }) { StatusCode = 400 };

        await _correctionService.UpsertAsync(CurrentUserId, CorrectionFields.PageAvancement, input.Projet.Trim(), input.Field, normalized);
        return new JsonResult(new { success = true, field = input.Field, value = normalized });
    }

    public async Task<JsonResult> OnPostCorrect([Microsoft.AspNetCore.Mvc.FromBody] AvancementCorrectionBatch input)
    {
        if (string.IsNullOrWhiteSpace(input?.ProjectKey))
            return new JsonResult(new { success = false, error = "Référence d'OF manquante." }) { StatusCode = 400 };

        var userId = CurrentUserId;

        foreach (var fv in input.Fields ?? [])
        {
            if (string.IsNullOrWhiteSpace(fv?.Field)) continue;

            // Empty value => revert to the Excel/original value (remove the correction).
            if (string.IsNullOrWhiteSpace(fv.Value))
            {
                await _correctionService.RemoveAsync(userId, CorrectionFields.PageAvancement, input.ProjectKey.Trim(), fv.Field);
                continue;
            }

            var error = _correctionService.Validate(CorrectionFields.PageAvancement, fv.Field, fv.Value, out var normalized);
            if (error is not null)
                return new JsonResult(new { success = false, error = $"Champ « {fv.Field} » : {error}" }) { StatusCode = 400 };

            await _correctionService.UpsertAsync(userId, CorrectionFields.PageAvancement, input.ProjectKey.Trim(), fv.Field, normalized);
        }

        return new JsonResult(new { success = true });
    }

    public async Task<JsonResult> OnPostResetCorrection([Microsoft.AspNetCore.Mvc.FromBody] AvancementCorrectionInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.Projet) || string.IsNullOrWhiteSpace(input?.Field))
            return new JsonResult(new { success = false, error = "Référence manquante." }) { StatusCode = 400 };

        await _correctionService.RemoveAsync(CurrentUserId, CorrectionFields.PageAvancement, input.Projet.Trim(), input.Field);
        return new JsonResult(new { success = true });
    }

    private static string? GetString(List<IXLCell> cells, int index)
    {
        if (index >= cells.Count) return null;
        var val = cells[index].GetValue<string>();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    private static int? GetInt(List<IXLCell> cells, int index, string key, IDictionary<string, string> bad)
    {
        if (index >= cells.Count) return null;
        var cell = cells[index];
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number)
            return (int)cell.GetDouble();
        if (int.TryParse(cell.GetString(), out var result))
            return result;
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

    private void ApplyInput(ManuelOF m)
    {
        m.Projet = NullIfEmpty(Input.Projet);
        m.RefOF = NullIfEmpty(Input.RefOF);
        m.StatutOF = NullIfEmpty(Input.StatutOF);
        m.DescriptionArticle = NullIfEmpty(Input.DescriptionArticle);
        m.Priorite = Input.Priorite;
        m.RequisFab = Input.RequisFab;
        m.RequisFinal = Input.RequisFinal;
        m.Scie = NullIfEmpty(Input.Scie);
        m.RobotPlasma = NullIfEmpty(Input.RobotPlasma);
        m.LaserTube = NullIfEmpty(Input.LaserTube);
        m.TablePlasma = NullIfEmpty(Input.TablePlasma);
        m.TableLaser = NullIfEmpty(Input.TableLaser);
        m.Pliage = NullIfEmpty(Input.Pliage);
        m.Roulage = NullIfEmpty(Input.Roulage);
        m.Machinage = NullIfEmpty(Input.Machinage);
        m.STMachine = NullIfEmpty(Input.STMachine);
        m.Montage = NullIfEmpty(Input.Montage);
        m.TuyauterieMontage = NullIfEmpty(Input.TuyauterieMontage);
        m.Soudage = NullIfEmpty(Input.Soudage);
        m.TuyauterieSoudage = NullIfEmpty(Input.TuyauterieSoudage);
        m.SoudageRobot = NullIfEmpty(Input.SoudageRobot);
        m.STMontageSoudage = NullIfEmpty(Input.STMontageSoudage);
        m.Inspection = NullIfEmpty(Input.Inspection);
        m.STInspection = NullIfEmpty(Input.STInspection);
        m.Reparation = NullIfEmpty(Input.Reparation);
        m.Peinture = NullIfEmpty(Input.Peinture);
        m.STPeinture = NullIfEmpty(Input.STPeinture);
        m.Emballage = NullIfEmpty(Input.Emballage);
        m.CommentaireInspection = NullIfEmpty(Input.CommentaireInspection);
        m.CommentaireOF = NullIfEmpty(Input.CommentaireOF);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AvancementOF ToAvancement(ManuelOF m)
    {
        return new AvancementOF
        {
            Id = m.Id,
            Manuel = m.Manuel,
            Projet = m.Projet,
            RefOF = m.RefOF,
            StatutOF = m.StatutOF,
            DescriptionArticle = m.DescriptionArticle,
            Priorite = m.Priorite,
            RequisFab = m.RequisFab,
            RequisFinal = m.RequisFinal,
            Scie = m.Scie,
            RobotPlasma = m.RobotPlasma,
            LaserTube = m.LaserTube,
            TablePlasma = m.TablePlasma,
            TableLaser = m.TableLaser,
            Pliage = m.Pliage,
            Roulage = m.Roulage,
            Machinage = m.Machinage,
            STMachine = m.STMachine,
            Montage = m.Montage,
            TuyauterieMontage = m.TuyauterieMontage,
            Soudage = m.Soudage,
            TuyauterieSoudage = m.TuyauterieSoudage,
            SoudageRobot = m.SoudageRobot,
            STMontageSoudage = m.STMontageSoudage,
            Inspection = m.Inspection,
            STInspection = m.STInspection,
            Reparation = m.Reparation,
            Peinture = m.Peinture,
            STPeinture = m.STPeinture,
            Emballage = m.Emballage,
            CommentaireInspection = m.CommentaireInspection,
            CommentaireOF = m.CommentaireOF,
            BadValues = m.BadValues,
            CorrectedFields = m.CorrectedFields,
        };
    }

    public class NewOFInput
    {
        public string? Id { get; set; }
        public string? Projet { get; set; }
        public string? RefOF { get; set; }
        public string? StatutOF { get; set; }
        public string? DescriptionArticle { get; set; }
        public int? Priorite { get; set; }
        public DateTime? RequisFab { get; set; }
        public DateTime? RequisFinal { get; set; }
        public string? Scie { get; set; }
        public string? RobotPlasma { get; set; }
        public string? LaserTube { get; set; }
        public string? TablePlasma { get; set; }
        public string? TableLaser { get; set; }
        public string? Pliage { get; set; }
        public string? Roulage { get; set; }
        public string? Machinage { get; set; }
        public string? STMachine { get; set; }
        public string? Montage { get; set; }
        public string? TuyauterieMontage { get; set; }
        public string? Soudage { get; set; }
        public string? TuyauterieSoudage { get; set; }
        public string? SoudageRobot { get; set; }
        public string? STMontageSoudage { get; set; }
        public string? Inspection { get; set; }
        public string? STInspection { get; set; }
        public string? Reparation { get; set; }
        public string? Peinture { get; set; }
        public string? STPeinture { get; set; }
        public string? Emballage { get; set; }
        public string? CommentaireInspection { get; set; }
        public string? CommentaireOF { get; set; }
    }

    public sealed record AvancementCorrectionInput
    {
        public string? Projet { get; init; }
        public string? Field { get; init; }
        public string? Value { get; init; }
    }

    public sealed record AvancementCorrectionBatch
    {
        public string? ProjectKey { get; set; }
        public List<OFCorrectionFieldValue>? Fields { get; set; }
    }

    public sealed record OFCorrectionFieldValue
    {
        public string? Field { get; set; }
        public string? Value { get; set; }
    }
}
