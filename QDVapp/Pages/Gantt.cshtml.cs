using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QDVapp.Models;

namespace QDVapp.Pages;

public class GanttModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public GanttModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    public List<GanttItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int VisibleWeeks { get; set; }
    public DateTime Today { get; set; }
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet(
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate,
        [FromQuery(Name = "weeks")] int weeks = 12)
    {
        Today = DateTime.Today;
        RangeStart = Today.AddDays(-14);

        if (fromDate.HasValue && toDate.HasValue && toDate > fromDate)
        {
            RangeStart = fromDate.Value;
            RangeEnd = toDate.Value;
            VisibleWeeks = 0;
        }
        else if (weeks > 0)
        {
            VisibleWeeks = weeks;
            RangeEnd = Today.AddDays(weeks * 7);
        }
        else
        {
            VisibleWeeks = 0;
        }

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
                ErrorMessage = "Données vides.";
                return;
            }

            var raw = new List<(string noCmd, string refOF, string? statut, string? desc, string? vendeur, DateTime? dateReq, double? restant, double? planifie, double? fait)>();

            foreach (var row in range.RowsUsed().Skip(1))
            {
                var cells = row.Cells().ToList();
                if (cells.Count < 2) continue;

                var noCmd = GetString(cells, 1);
                if (string.IsNullOrEmpty(noCmd)) continue;

                raw.Add((noCmd, GetString(cells, 2) ?? "", GetString(cells, 0), GetString(cells, 4), GetString(cells, 6), GetDateTime(cells, 7), GetDouble(cells, 12), GetDouble(cells, 9), GetDouble(cells, 10)));
            }

            var items = raw.Where(r => r.dateReq.HasValue)
                           .Select(r => new GanttItem
                           {
                               NoCmd = r.noCmd,
                               RefOF = r.refOF,
                               Statut = r.statut ?? "",
                               Description = r.desc ?? "",
                               Vendeur = r.vendeur ?? "",
                               DateRequise = r.dateReq!.Value,
                               Restant = r.restant,
                               Planifie = r.planifie,
                               Fait = r.fait
                           })
                           .OrderBy(i => i.DateRequise)
                           .ToList();

            Items = items;
            TotalCount = items.Count;

            if (VisibleWeeks == 0 && RangeEnd == default(DateTime))
            {
                var maxDate = items.Count > 0 ? items.Max(i => i.DateRequise) : Today;
                RangeEnd = maxDate.AddDays(14);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de lecture: {ex.Message}";
        }
    }

    static string? GetString(List<IXLCell> cells, int index)
    {
        if (index >= cells.Count) return null;
        var val = cells[index].GetValue<string>();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    static double? GetDouble(List<IXLCell> cells, int index)
    {
        if (index >= cells.Count) return null;
        if (cells[index].DataType == XLDataType.Number)
            return (double)cells[index].GetDouble();
        return null;
    }

    static DateTime? GetDateTime(List<IXLCell> cells, int index)
    {
        if (index >= cells.Count) return null;
        if (cells[index].DataType == XLDataType.DateTime)
            return cells[index].GetDateTime();
        return null;
    }
}

public class GanttItem
{
    public string NoCmd { get; set; } = "";
    public string RefOF { get; set; } = "";
    public string Statut { get; set; } = "";
    public string Description { get; set; } = "";
    public string Vendeur { get; set; } = "";
    public DateTime DateRequise { get; set; }
    public double? Restant { get; set; }
    public double? Planifie { get; set; }
    public double? Fait { get; set; }
}
