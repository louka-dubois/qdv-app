using ClosedXML.Excel;
using QDVapp.Data;
using QDVapp.Models;

namespace QDVapp.Services;

public class CorrectedExcelExportService
{
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;
    private readonly CorrectionService _correctionService;

    public CorrectedExcelExportService(IWebHostEnvironment env, ApplicationDbContext db, CorrectionService correctionService)
    {
        _env = env;
        _db = db;
        _correctionService = correctionService;
    }

    public byte[]? ExportProjectsCorrected()
    {
        var filePath = Path.Combine(_env.WebRootPath, "files", "AtelierProjetsUsine", "Atelier - Liste des projets dans usine (1).xlsx");
        if (!System.IO.File.Exists(filePath)) return null;

        var all = new List<ProjetUsine>();
        using (var workbook = new XLWorkbook(filePath))
        {
            var ws = workbook.Worksheet("Liste des projets");
            var range = ws.RangeUsed();
            if (range is not null)
            {
                foreach (var row in range.RowsUsed().Skip(1))
                {
                    var cells = row.Cells().ToList();
                    if (cells.Count < 2) continue;
                    var noCmd = GetString(cells, 1);
                    if (string.IsNullOrEmpty(noCmd)) continue;
                    all.Add(ParseProject(cells));
                }
            }
        }

        var corrections = _correctionService.GetAllAsync(CorrectionFields.PageAtelier).GetAwaiter().GetResult();
        _correctionService.ApplyAtelier(all, corrections);
        all.AddRange(_db.Projets);

        return WriteProjectFile(filePath, all);
    }

    public byte[]? ExportOfsCorrected()
    {
        var filePath = Path.Combine(_env.WebRootPath, "files", "AvancementOF", "Avancement des OF - Heures à faire.xlsx");
        if (!System.IO.File.Exists(filePath)) return null;

        var all = new List<AvancementOF>();
        using (var workbook = new XLWorkbook(filePath))
        {
            var ws = workbook.Worksheet("Liste des heures à faire");
            var range = ws.RangeUsed();
            if (range is not null)
            {
                foreach (var row in range.RowsUsed().Skip(1))
                {
                    var cells = row.Cells().ToList();
                    if (cells.Count < 2) continue;
                    if (cells[0].GetValue<string>().StartsWith("Nbre", StringComparison.OrdinalIgnoreCase)) break;
                    var projet = GetString(cells, 0);
                    if (string.IsNullOrEmpty(projet)) continue;
                    all.Add(ParseOf(cells));
                }
            }
        }

        var corrections = _correctionService.GetAllAsync(CorrectionFields.PageAvancement).GetAwaiter().GetResult();
        _correctionService.ApplyAvancement(all, corrections);
        all.AddRange(_db.ManuelOFs.Select(ToAvancement));

        return WriteOfFile(filePath, all);
    }

    private byte[]? WriteProjectFile(string filePath, List<ProjetUsine> projects)
    {
        byte[] bytes;
        using (var workbook = new XLWorkbook(filePath))
        {
            var ws = workbook.Worksheet("Liste des projets");
            ws.RangeUsed()?.Clear(XLClearOptions.Contents);

            var headers = ProjectHeaders();
            WriteHeaders(ws, headers);

            var r = 2;
            foreach (var p in projects)
            {
                WriteProjectRow(ws, r, p);
                r++;
            }

            ws.RangeUsed()?.SetAutoFilter();
            ws.Columns(1, headers.Count).AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            bytes = ms.ToArray();
        }
        return bytes;
    }

    private byte[]? WriteOfFile(string filePath, List<AvancementOF> ordres)
    {
        byte[] bytes;
        using (var workbook = new XLWorkbook(filePath))
        {
            var ws = workbook.Worksheet("Liste des heures à faire");
            ws.RangeUsed()?.Clear(XLClearOptions.Contents);

            var headers = OfHeaders();
            WriteHeaders(ws, headers);

            var r = 2;
            foreach (var o in ordres)
            {
                WriteOfRow(ws, r, o);
                r++;
            }

            ws.RangeUsed()?.SetAutoFilter();
            ws.Columns(1, headers.Count).AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            bytes = ms.ToArray();
        }
        return bytes;
    }

    private static List<string> ProjectHeaders()
        => new()
        {
            "Statut", "NoCmd", "RéfOF", "CodeArticle", "Description", "Commentaire OF", "Vendeur",
            "Date requise", "Jrs ST planifié", "Planifié (estimé original)", "Fait (réel)", "Décl. av",
            "Restant à faire selon Avcmt", "Temps Total fin estimé", "Diff", "Pct Diff", "NoteFab"
        };

    private static List<string> OfHeaders()
    {
        var headers = new List<string>
        {
            "Projet", "RéfOF", "StatutOF", "Description Article", "Priorité", "Requis Fab", "Requis final"
        };
        foreach (var (label, _) in StationColumns.Stations) headers.Add(label);
        headers.Add("Commentaire Inspection");
        headers.Add("CommentaireOF");
        return headers;
    }

    private static void WriteHeaders(IXLWorksheet ws, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#343A40");
        }
        ws.SheetView.FreezeRows(1);
    }

    private static ProjetUsine ParseProject(List<IXLCell> cells)
    {
        var proj = new ProjetUsine { NoCmd = GetString(cells, 1), BadValues = new Dictionary<string, string>() };
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
        return proj;
    }

    private static AvancementOF ParseOf(List<IXLCell> cells)
    {
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
        return ordre;
    }

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

    private static void WriteProjectRow(IXLWorksheet ws, int r, ProjetUsine p)
    {
        var c = 1;
        ws.Cell(r, c++).Value = p.Statut ?? "";
        ws.Cell(r, c++).Value = p.NoCmd ?? "";
        ws.Cell(r, c++).Value = p.RefOF ?? "";
        ws.Cell(r, c++).Value = p.CodeArticle ?? "";
        ws.Cell(r, c++).Value = p.Description ?? "";
        ws.Cell(r, c++).Value = p.CommentaireOF ?? "";
        ws.Cell(r, c++).Value = p.Vendeur ?? "";
        if (p.DateRequise.HasValue) ws.Cell(r, c++).Value = p.DateRequise.Value; else c++;
        if (p.JrsSTPlanifie.HasValue) ws.Cell(r, c++).Value = p.JrsSTPlanifie.Value; else c++;
        if (p.Planifie.HasValue) ws.Cell(r, c++).Value = p.Planifie.Value; else c++;
        if (p.Fait.HasValue) ws.Cell(r, c++).Value = p.Fait.Value; else c++;
        if (p.DeclAv.HasValue) ws.Cell(r, c++).Value = p.DeclAv.Value; else c++;
        if (p.Restant.HasValue) ws.Cell(r, c++).Value = p.Restant.Value; else c++;
        if (p.TempsTotalFinEstime.HasValue) ws.Cell(r, c++).Value = p.TempsTotalFinEstime.Value; else c++;
        ws.Cell(r, c++).Value = StringOrNumber(p.Diff);
        ws.Cell(r, c++).Value = StringOrNumber(p.PctDiff);
        ws.Cell(r, c).Value = p.NoteFab ?? "";
    }

    private static void WriteOfRow(IXLWorksheet ws, int r, AvancementOF o)
    {
        var c = 1;
        ws.Cell(r, c++).Value = o.Projet ?? "";
        ws.Cell(r, c++).Value = o.RefOF ?? "";
        ws.Cell(r, c++).Value = o.StatutOF ?? "";
        ws.Cell(r, c++).Value = o.DescriptionArticle ?? "";
        if (o.Priorite.HasValue) ws.Cell(r, c++).Value = o.Priorite.Value; else c++;
        if (o.RequisFab.HasValue) ws.Cell(r, c++).Value = o.RequisFab.Value; else c++;
        if (o.RequisFinal.HasValue) ws.Cell(r, c++).Value = o.RequisFinal.Value; else c++;
        foreach (var (_, prop) in StationColumns.Stations)
            ws.Cell(r, c++).Value = StringOrNumber(GetStationValue(o, prop));
        ws.Cell(r, c++).Value = o.CommentaireInspection ?? "";
        ws.Cell(r, c).Value = o.CommentaireOF ?? "";
    }

    private static string? GetStationValue(AvancementOF of, string propertyName)
    {
        var prop = typeof(AvancementOF).GetProperty(propertyName);
        return prop?.GetValue(of)?.ToString();
    }

    private static XLCellValue StringOrNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return XLCellValue.FromObject("");
        if (double.TryParse(value.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return XLCellValue.FromObject(d);
        return XLCellValue.FromObject(value);
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
        if (cell.DataType == XLDataType.Number) return (int)cell.GetDouble();
        if (int.TryParse(cell.GetString(), out var result)) return result;
        bad[key] = cell.GetString();
        return null;
    }

    private static DateTime? GetDateTime(List<IXLCell> cells, int index, string key, IDictionary<string, string> bad)
    {
        if (index >= cells.Count) return null;
        var cell = cells[index];
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();
        if (cell.DataType == XLDataType.Number) return DateTime.FromOADate(cell.GetDouble());
        if (DateTime.TryParse(cell.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt)) return dt;
        bad[key] = cell.GetString();
        return null;
    }

    private static double? GetDouble(List<IXLCell> cells, int index, string key, IDictionary<string, string> bad)
    {
        if (index >= cells.Count) return null;
        var cell = cells[index];
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number) return cell.GetDouble();
        if (double.TryParse(cell.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num)) return num;
        bad[key] = cell.GetString();
        return null;
    }
}