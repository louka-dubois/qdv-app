using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QDVapp.Data;
using QDVapp.Models;
using QDVapp.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace QDVapp.Pages;

public class RapportsModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;
    private readonly CorrectionService _correctionService;

    public RapportsModel(IWebHostEnvironment env, ApplicationDbContext db, CorrectionService correctionService)
    {
        _env = env;
        _db = db;
        _correctionService = correctionService;
    }

    public string Period { get; set; } = "week";
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public string PeriodLabel { get; set; } = "";

    public string PeriodLabelDate(DateTime d) => d.ToString("yyyy-MM-dd");
    public string? Vendeur { get; set; }
    public List<string> Vendeurs { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public List<ProjetUsine> Projets { get; set; } = [];
    public List<ProjetUsine> AllProjets { get; set; } = [];
    public List<AvancementOF> Ordres { get; set; } = [];

    public int TotalProjets { get; set; }
    public int ProjetsEnRetard { get; set; }
    public double HeuresRestantes { get; set; }
    public double HeuresRestantesEnRetard { get; set; }
    public int TotalOfs { get; set; }
    public double TotalHeuresStations { get; set; }

    public string StatusLabelsJson { get; set; } = "[]";
    public string StatusCountsJson { get; set; } = "[]";
    public string VendeurLabelsJson { get; set; } = "[]";
    public string VendeurHoursJson { get; set; } = "[]";
    public string StationLabelsJson { get; set; } = "[]";
    public string StationHoursJson { get; set; } = "[]";
    public string OfStatusLabelsJson { get; set; } = "[]";
    public string OfStatusCountsJson { get; set; } = "[]";

    public async Task OnGet([FromQuery(Name = "period")] string? period,
        [FromQuery(Name = "start")] string? start,
        [FromQuery(Name = "end")] string? end,
        [FromQuery(Name = "vendeur")] string? vendeur)
    {
        ApplyFilters(period, start, end, vendeur);

        await LoadProjetsAsync();
        await LoadOfsAsync();

        ComputeTotals();

        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        StatusLabelsJson = JsonSerializer.Serialize(
            Projets.Where(p => !string.IsNullOrWhiteSpace(p.Statut))
                   .Select(p => p.Statut!)
                   .GroupBy(s => s)
                   .Select(g => g.Key).ToList(), opts);
        StatusCountsJson = JsonSerializer.Serialize(
            Projets.Where(p => !string.IsNullOrWhiteSpace(p.Statut))
                   .Select(p => p.Statut!)
                   .GroupBy(s => s)
                   .Select(g => g.Count()).ToList(), opts);

        var vendeurHours = Projets.Where(p => !string.IsNullOrWhiteSpace(p.Vendeur))
                                  .GroupBy(p => p.Vendeur!)
                                  .Select(g => (Key: g.Key, Hours: g.Sum(p => p.Restant ?? 0)))
                                  .OrderByDescending(x => x.Hours)
                                  .Take(10).ToList();
        VendeurLabelsJson = JsonSerializer.Serialize(vendeurHours.Select(x => x.Key).ToList(), opts);
        VendeurHoursJson = JsonSerializer.Serialize(vendeurHours.Select(x => Math.Round(x.Hours, 1)).ToList(), opts);

        var stationHours = new List<(string Label, double Hours)>();
        foreach (var (label, prop) in StationColumns.Stations)
        {
            var total = Ordres.Sum(o => ParseHour(o.BadValues.Count == 0 ? GetStationValue(o, prop) : null));
            stationHours.Add((label, total));
        }
        StationLabelsJson = JsonSerializer.Serialize(stationHours.Select(x => x.Label).ToList(), opts);
        StationHoursJson = JsonSerializer.Serialize(stationHours.Select(x => Math.Round(x.Hours, 1)).ToList(), opts);

        OfStatusLabelsJson = JsonSerializer.Serialize(
            Ordres.Where(o => !string.IsNullOrWhiteSpace(o.StatutOF))
                  .Select(o => o.StatutOF!)
                  .GroupBy(s => s)
                  .Select(g => g.Key).ToList(), opts);
        OfStatusCountsJson = JsonSerializer.Serialize(
            Ordres.Where(o => !string.IsNullOrWhiteSpace(o.StatutOF))
                  .Select(o => o.StatutOF!)
                  .GroupBy(s => s)
                  .Select(g => g.Count()).ToList(), opts);
    }

    public async Task<IActionResult> OnGetExport([FromQuery(Name = "period")] string? period,
        [FromQuery(Name = "start")] string? start,
        [FromQuery(Name = "end")] string? end,
        [FromQuery(Name = "vendeur")] string? vendeur)
    {
        ApplyFilters(period, start, end, vendeur);

        await LoadProjetsAsync();
        await LoadOfsAsync();
        ComputeTotals();

        var fileName = $"Rapport_{RangeStart:yyyyMMdd}_au_{RangeEnd:yyyyMMdd}{(!string.IsNullOrEmpty(Vendeur) ? $"_{SanitizeFileName(Vendeur!)}" : "")}.xlsx";

        byte[] bytes;
        using (var workbook = new XLWorkbook())
        {
            WriteSynthèseSheet(workbook);
            WriteProjetsSheet(workbook);
            WriteOfsSheet(workbook);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            bytes = ms.ToArray();
        }

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> OnGetExportPdf([FromQuery(Name = "period")] string? period,
        [FromQuery(Name = "start")] string? start,
        [FromQuery(Name = "end")] string? end,
        [FromQuery(Name = "vendeur")] string? vendeur)
    {
        ApplyFilters(period, start, end, vendeur);

        await LoadProjetsAsync();
        await LoadOfsAsync();
        ComputeTotals();

        var fileName = $"Rapport_{RangeStart:yyyyMMdd}_au_{RangeEnd:yyyyMMdd}{(!string.IsNullOrEmpty(Vendeur) ? $"_{SanitizeFileName(Vendeur!)}" : "")}.pdf";
        var pdf = BuildPdf();
        return File(pdf, "application/pdf", fileName);
    }

    private byte[] BuildPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var statusDist = Projets.Where(p => !string.IsNullOrWhiteSpace(p.Statut))
                                .GroupBy(p => p.Statut!)
                                .Select(g => (Key: g.Key, Value: (object)g.Count()))
                                .OrderByDescending(x => (int)x.Value)
                                .ToList();
        var vendeurHours = Projets.Where(p => !string.IsNullOrWhiteSpace(p.Vendeur))
                                  .GroupBy(p => p.Vendeur!)
                                  .Select(g => (Key: g.Key, Value: (object)Math.Round(g.Sum(p => p.Restant ?? 0), 2)))
                                  .OrderByDescending(x => (double)x.Value)
                                  .ToList();
        var stationHours = new List<(string Key, object Value)>();
        foreach (var (label, prop) in StationColumns.Stations)
        {
            var total = Ordres.Sum(o => ParseHour(o.BadValues.Count == 0 ? GetStationValue(o, prop) : null));
            stationHours.Add((label, Math.Round(total, 2)));
        }
        stationHours = stationHours.OrderByDescending(x => (double)x.Value).ToList();
        var ofStatusDist = Ordres.Where(o => !string.IsNullOrWhiteSpace(o.StatutOF))
                                 .GroupBy(o => o.StatutOF!)
                                 .Select(g => (Key: g.Key, Value: (object)g.Count()))
                                 .OrderByDescending(x => (int)x.Value)
                                 .ToList();

        var document = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().PaddingBottom(8).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Rapport QDVapp").FontSize(18).SemiBold();
                        row.RelativeItem().AlignRight().Text($"Généré le {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                    col.Item().Text($"Période : {PeriodLabel}").FontSize(11);
                    col.Item().Text(string.IsNullOrEmpty(Vendeur) ? "Vendeur : Tous" : $"Vendeur : {Vendeur}").FontSize(10);
                    col.Item().Text($"{Projets.Count} projet(s) · {Ordres.Count} OF").FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Padding(2).Element(KpiBox).Column(inner =>
                        {
                            inner.Item().Text(TotalProjets.ToString()).FontSize(15).SemiBold();
                            inner.Item().Text("Projets période").FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                        row.RelativeItem().Padding(2).Element(KpiBox).Column(inner =>
                        {
                            inner.Item().Text(ProjetsEnRetard.ToString()).FontSize(15).SemiBold();
                            inner.Item().Text("En retard").FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                        row.RelativeItem().Padding(2).Element(KpiBox).Column(inner =>
                        {
                            inner.Item().Text(Math.Round(HeuresRestantes, 0).ToString("0") + " h").FontSize(15).SemiBold();
                            inner.Item().Text("Heures restantes").FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                        row.RelativeItem().Padding(2).Element(KpiBox).Column(inner =>
                        {
                            inner.Item().Text(Math.Round(HeuresRestantesEnRetard, 0).ToString("0") + " h").FontSize(15).SemiBold();
                            inner.Item().Text("Heures en retard").FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                        row.RelativeItem().Padding(2).Element(KpiBox).Column(inner =>
                        {
                            inner.Item().Text(TotalOfs.ToString()).FontSize(15).SemiBold();
                            inner.Item().Text("Total OF").FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                        row.RelativeItem().Padding(2).Element(KpiBox).Column(inner =>
                        {
                            inner.Item().Text(Math.Round(TotalHeuresStations, 0).ToString("0") + " h").FontSize(15).SemiBold();
                            inner.Item().Text("Heures postes").FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    col.Item().PaddingTop(8).Text("Répartition par statut (projets)").FontSize(12).SemiBold();
                    col.Item().Table(t => WriteBreakdownTable(t, statusDist, "Statut", "Nb projets"));

                    col.Item().PaddingTop(8).Text("Heures restantes par vendeur").FontSize(12).SemiBold();
                    col.Item().Table(t => WriteBreakdownTable(t, vendeurHours, "Vendeur", "Heures restantes"));

                    col.Item().PaddingTop(8).Text("Heures par poste (OF)").FontSize(12).SemiBold();
                    col.Item().Table(t => WriteBreakdownTable(t, stationHours, "Poste", "Heures"));

                    col.Item().PaddingTop(8).Text("Répartition OF par statut").FontSize(12).SemiBold();
                    col.Item().Table(t => WriteBreakdownTable(t, ofStatusDist, "Statut", "Nb OF"));

                    col.Item().PaddingTop(12).Text($"Projets de la période ({Projets.Count})").FontSize(12).SemiBold();
                    col.Item().Table(WriteProjetsTable);

                    col.Item().PaddingTop(12).Text($"OF de la période ({Ordres.Count})").FontSize(12).SemiBold();
                    col.Item().Table(WriteOfsTable);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Grey.Darken2));
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IContainer KpiBox(IContainer container)
    {
        return container
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten3)
            .Background(Colors.Grey.Lighten5)
            .PaddingVertical(5)
            .PaddingHorizontal(6)
            .AlignCenter();
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Background(Colors.Grey.Darken2)
            .PaddingVertical(3)
            .PaddingHorizontal(4);
    }

    private static IContainer DataCell(IContainer container)
    {
        return container
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(2)
            .PaddingHorizontal(4);
    }

    private static void WriteTableHeader(QuestPDF.Fluent.TableDescriptor table, params string[] headers)
    {
        table.Header(header =>
        {
            foreach (var h in headers)
                header.Cell().Element(HeaderCell).Text(h).FontColor(Colors.White).SemiBold();
        });
    }

    private static void WriteBreakdownTable(QuestPDF.Fluent.TableDescriptor table, IEnumerable<(string Key, object Value)> rows, string col1, string col2)
    {
        table.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(3);
            cd.RelativeColumn(1);
        });
        WriteTableHeader(table, col1, col2);
        foreach (var (key, value) in rows)
        {
            table.Cell().Element(DataCell).Text(key);
            table.Cell().Element(DataCell).Text(value.ToString());
        }
    }

    private void WriteProjetsTable(QuestPDF.Fluent.TableDescriptor table)
    {
        table.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(1);
            cd.RelativeColumn(2);
            cd.RelativeColumn(1.5f);
            cd.RelativeColumn(1.5f);
            cd.RelativeColumn(2);
            cd.RelativeColumn(1.5f);
            cd.RelativeColumn(1);
        });
        WriteTableHeader(table, "Statut", "No Projet", "Réf OF", "Vendeur", "Code Article", "Date requise", "Restant (h)");
        foreach (var p in Projets)
        {
            table.Cell().Element(DataCell).Text(p.Statut ?? "-");
            table.Cell().Element(DataCell).Text(p.NoCmd ?? "-");
            table.Cell().Element(DataCell).Text(p.RefOF ?? "-");
            table.Cell().Element(DataCell).Text(p.Vendeur ?? "-");
            table.Cell().Element(DataCell).Text(p.CodeArticle ?? "-");
            table.Cell().Element(DataCell).Text(p.DateRequise?.ToString("dd/MM/yyyy") ?? "-");
            table.Cell().Element(DataCell).Text(p.Restant?.ToString("0.##") ?? "-");
        }
    }

    private void WriteOfsTable(QuestPDF.Fluent.TableDescriptor table)
    {
        table.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(2);
            cd.RelativeColumn(1.5f);
            cd.RelativeColumn(1.5f);
            cd.RelativeColumn(1.5f);
            cd.RelativeColumn(1);
        });
        WriteTableHeader(table, "Projet", "Réf OF", "Statut", "Requis final", "Heures postes");
        foreach (var o in Ordres)
        {
            table.Cell().Element(DataCell).Text(o.Projet ?? "-");
            table.Cell().Element(DataCell).Text(o.RefOF ?? "-");
            table.Cell().Element(DataCell).Text(o.StatutOF ?? "-");
            table.Cell().Element(DataCell).Text(o.RequisFinal?.ToString("dd/MM/yyyy") ?? "-");
            table.Cell().Element(DataCell).Text(SumStationHours(o).ToString("0.##"));
        }
    }

    private static double SumStationHours(AvancementOF of)
    {
        double total = 0;
        foreach (var (_, prop) in StationColumns.Stations)
        {
            var val = of.GetType().GetProperty(prop)?.GetValue(of)?.ToString();
            if (double.TryParse((val ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                total += num;
        }
        return total;
    }

    private void ApplyFilters(string? period, string? start, string? end, string? vendeur)
    {
        Vendeur = string.IsNullOrWhiteSpace(vendeur) ? null : vendeur.Trim();
        Period = period switch
        {
            "day" => "day",
            "month" => "month",
            "custom" => "custom",
            _ => "week"
        };

        var today = DateTime.Today;
        if (Period == "custom" && DateTime.TryParse(start, out var s) && DateTime.TryParse(end, out var e))
        {
            RangeStart = s.Date;
            RangeEnd = e.Date >= s.Date ? e.Date : s.Date;
            PeriodLabel = $"Du {RangeStart:dd/MM/yyyy} au {RangeEnd:dd/MM/yyyy}";
        }
        else
        {
            (RangeStart, RangeEnd, PeriodLabel) = ComputeRange(Period, today);
        }
    }

    private void WriteSynthèseSheet(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("Synthèse");
        ws.Cell(1, 1).Value = "Rapport QDVapp";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Période : {PeriodLabel}";
        ws.Cell(3, 1).Value = string.IsNullOrEmpty(Vendeur) ? "Vendeur : Tous" : $"Vendeur : {Vendeur}";

        var kpis = new (string Label, string Value)[]
        {
            ("Projets de la période", TotalProjets.ToString()),
            ("Projets en retard", ProjetsEnRetard.ToString()),
            ("Heures restantes (h)", Math.Round(HeuresRestantes, 2).ToString("0.##")),
            ("Heures en retard (h)", Math.Round(HeuresRestantesEnRetard, 2).ToString("0.##")),
            ("Total OF", TotalOfs.ToString()),
            ("Total heures postes (h)", Math.Round(TotalHeuresStations, 2).ToString("0.##")),
        };

        var r = 5;
        foreach (var (label, value) in kpis)
        {
            ws.Cell(r, 1).Value = label;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = value;
            r++;
        }

        r += 1;

        var statusDist = Projets.Where(p => !string.IsNullOrWhiteSpace(p.Statut))
                                .GroupBy(p => p.Statut!)
                                .Select(g => (Key: g.Key, Value: (object)g.Count()))
                                .OrderByDescending(x => (int)x.Value)
                                .ToList();
        WriteBreakdown(ws, r, "Répartition par statut (projets)", "Statut", "Nb projets", statusDist);
        r += statusDist.Count + 3;

        var vendeurHours = Projets.Where(p => !string.IsNullOrWhiteSpace(p.Vendeur))
                                  .GroupBy(p => p.Vendeur!)
                                  .Select(g => (Key: g.Key, Value: (object)Math.Round(g.Sum(p => p.Restant ?? 0), 2)))
                                  .OrderByDescending(x => (double)x.Value)
                                  .ToList();
        WriteBreakdown(ws, r, "Heures restantes par vendeur", "Vendeur", "Heures restantes", vendeurHours);
        r += vendeurHours.Count + 3;

        var stationHours = new List<(string Key, object Value)>();
        foreach (var (label, prop) in StationColumns.Stations)
        {
            var total = Ordres.Sum(o => ParseHour(o.BadValues.Count == 0 ? GetStationValue(o, prop) : null));
            stationHours.Add((label, Math.Round(total, 2)));
        }
        stationHours = stationHours.OrderByDescending(x => (double)x.Value).ToList();
        WriteBreakdown(ws, r, "Heures par poste (OF)", "Poste", "Heures", stationHours);
        r += stationHours.Count + 3;

        var ofStatusDist = Ordres.Where(o => !string.IsNullOrWhiteSpace(o.StatutOF))
                                 .GroupBy(o => o.StatutOF!)
                                 .Select(g => (Key: g.Key, Value: (object)g.Count()))
                                 .OrderByDescending(x => (int)x.Value)
                                 .ToList();
        WriteBreakdown(ws, r, "Répartition OF par statut", "Statut", "Nb OF", ofStatusDist);

        ws.Columns().AdjustToContents(1, 4, 40);
    }

    private void WriteProjetsSheet(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("Projets");
        var headers = new[] { "Statut", "No Projet", "Réf OF", "Code Article", "Description", "Vendeur", "Date requise", "Planifié (h)", "Fait (h)", "Restant (h)", "Diff", "Pct Diff", "Note Fab" };
        WriteHeaders(ws, headers);

        var r = 2;
        foreach (var p in Projets)
        {
            var c = 1;
            ws.Cell(r, c++).Value = p.Statut ?? "";
            ws.Cell(r, c++).Value = p.NoCmd ?? "";
            ws.Cell(r, c++).Value = p.RefOF ?? "";
            ws.Cell(r, c++).Value = p.CodeArticle ?? "";
            ws.Cell(r, c++).Value = p.Description ?? "";
            ws.Cell(r, c++).Value = p.Vendeur ?? "";
            if (p.DateRequise.HasValue) ws.Cell(r, c++).Value = p.DateRequise.Value; else c++;
            if (p.Planifie.HasValue) ws.Cell(r, c++).Value = p.Planifie.Value; else c++;
            if (p.Fait.HasValue) ws.Cell(r, c++).Value = p.Fait.Value; else c++;
            if (p.Restant.HasValue) ws.Cell(r, c++).Value = p.Restant.Value; else c++;
            ws.Cell(r, c++).Value = p.Diff ?? "";
            ws.Cell(r, c++).Value = p.PctDiff ?? "";
            ws.Cell(r, c).Value = p.NoteFab ?? "";
            r++;
        }
        FinalizeSheet(ws);
    }

    private void WriteOfsSheet(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.Add("OF");
        var headers = new List<string> { "Projet", "Réf OF", "Statut", "Description", "Priorité", "Requis fab", "Requis final" };
        foreach (var (label, _) in StationColumns.Stations) headers.Add(label);
        headers.Add("Total heures postes");
        WriteHeaders(ws, headers);

        var r = 2;
        foreach (var o in Ordres)
        {
            var c = 1;
            ws.Cell(r, c++).Value = o.Projet ?? "";
            ws.Cell(r, c++).Value = o.RefOF ?? "";
            ws.Cell(r, c++).Value = o.StatutOF ?? "";
            ws.Cell(r, c++).Value = o.DescriptionArticle ?? "";
            if (o.Priorite.HasValue) ws.Cell(r, c++).Value = o.Priorite.Value; else c++;
            if (o.RequisFab.HasValue) ws.Cell(r, c++).Value = o.RequisFab.Value; else c++;
            if (o.RequisFinal.HasValue) ws.Cell(r, c++).Value = o.RequisFinal.Value; else c++;

            double total = 0;
            foreach (var (_, prop) in StationColumns.Stations)
            {
                var val = GetStationValue(o, prop);
                ws.Cell(r, c++).Value = val ?? "";
                total += ParseHour(val);
            }
            ws.Cell(r, c).Value = Math.Round(total, 2);
            r++;
        }
        FinalizeSheet(ws);
    }

    private void WriteHeaders(IXLWorksheet ws, IReadOnlyList<string> headers)
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

    private void FinalizeSheet(IXLWorksheet ws)
    {
        ws.RangeUsed()?.SetAutoFilter();
    }

    private void WriteBreakdown(IXLWorksheet ws, int startRow, string title, string col1, string col2, List<(string Key, object Value)> rows)
    {
        ws.Cell(startRow, 2).Value = title;
        ws.Cell(startRow, 2).Style.Font.Bold = true;
        ws.Cell(startRow + 1, 2).Value = col1;
        ws.Cell(startRow + 1, 3).Value = col2;
        ws.Cell(startRow + 1, 2).Style.Font.Bold = true;
        ws.Cell(startRow + 1, 3).Style.Font.Bold = true;
        var r = startRow + 2;
        foreach (var (key, value) in rows)
        {
            ws.Cell(r, 2).Value = key;
            ws.Cell(r, 3).Value = XLCellValue.FromObject(value);
            r++;
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return name;
    }

    private (DateTime start, DateTime end, string label) ComputeRange(string period, DateTime today)
    {
        return period switch
        {
            "day" => (today, today, $"Jour du {today:dd/MM/yyyy}"),
            "month" => (new DateTime(today.Year, today.Month, 1),
                        new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
                        $"{today:MMMM yyyy}"),
            _ => (today.AddDays(-((int)today.DayOfWeek)).AddDays(1),
                  today.AddDays(7 - (int)today.DayOfWeek),
                  $"Semaine du {today.AddDays(-((int)today.DayOfWeek)).AddDays(1):dd/MM/yyyy} au {today.AddDays(7 - (int)today.DayOfWeek):dd/MM/yyyy}")
        };
    }

    private async Task LoadProjetsAsync()
    {
        var filePath = Path.Combine(_env.WebRootPath, "files", "AtelierProjetsUsine", "Atelier - Liste des projets dans usine (1).xlsx");
        if (!System.IO.File.Exists(filePath)) return;

        try
        {
            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheet("Liste des projets");
            var range = ws.RangeUsed();
            if (range is null) return;

            var all = new List<ProjetUsine>();
            foreach (var row in range.RowsUsed().Skip(1))
            {
                var cells = row.Cells().ToList();
                if (cells.Count < 2) continue;
                var noCmd = GetString(cells, 1);
                if (string.IsNullOrEmpty(noCmd)) continue;

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
                all.Add(proj);
            }

            var corrections = await _correctionService.GetAllAsync(CorrectionFields.PageAtelier);
            _correctionService.ApplyAtelier(all, corrections);

            var manual = _db.Projets.ToList();
            all.AddRange(manual);

            AllProjets = all.Where(p => p.DateRequise.HasValue).ToList();
            Vendeurs = AllProjets.Where(p => !string.IsNullOrWhiteSpace(p.Vendeur))
                                 .Select(p => p.Vendeur!)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .OrderBy(x => x)
                                 .ToList();

            Projets = AllProjets.Where(p =>
                (string.IsNullOrEmpty(Vendeur) || string.Equals(p.Vendeur, Vendeur, StringComparison.OrdinalIgnoreCase)) &&
                p.DateRequise!.Value.Date >= RangeStart &&
                p.DateRequise!.Value.Date <= RangeEnd).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de lecture des projets : {ex.Message}";
        }
    }

    private async Task LoadOfsAsync()
    {
        var filePath = Path.Combine(_env.WebRootPath, "files", "AvancementOF", "Avancement des OF - Heures à faire.xlsx");
        if (!System.IO.File.Exists(filePath)) return;

        try
        {
            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheet(1);
            var range = ws.RangeUsed();
            if (range is null) return;

            var all = new List<AvancementOF>();
            foreach (var row in range.RowsUsed().Skip(1))
            {
                var cells = row.Cells().ToList();
                if (cells.Count < 2) continue;
                if (cells[0].GetValue<string>().StartsWith("Nbre", StringComparison.OrdinalIgnoreCase)) break;
                var projet = GetString(cells, 0);
                if (string.IsNullOrEmpty(projet)) continue;

                var ordre = new AvancementOF { BadValues = new Dictionary<string, string>() };
                ordre.Projet = projet;
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
                all.Add(ordre);
            }

            var corrections = await _correctionService.GetAllAsync(CorrectionFields.PageAvancement);
            _correctionService.ApplyAvancement(all, corrections);

            var manual = _db.ManuelOFs.ToList();
            all.AddRange(manual.Select(ToAvancement));

            Ordres = all.Where(o =>
                o.RequisFinal.HasValue &&
                o.RequisFinal.Value.Date >= RangeStart &&
                o.RequisFinal.Value.Date <= RangeEnd).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de lecture des OF : {ex.Message}";
        }
    }

    private void ComputeTotals()
    {
        TotalProjets = Projets.Count;
        HeuresRestantes = Projets.Sum(p => p.Restant ?? 0);
        var now = DateTime.Today;
        var retard = Projets.Where(p => p.DateRequise.HasValue && p.DateRequise < now && (p.Restant ?? 0) > 0).ToList();
        ProjetsEnRetard = retard.Count;
        HeuresRestantesEnRetard = retard.Sum(p => p.Restant ?? 0);

        TotalOfs = Ordres.Count;
        TotalHeuresStations = 0;
        foreach (var (label, prop) in StationColumns.Stations)
        {
            TotalHeuresStations += Ordres.Sum(o => ParseHour(GetStationValue(o, prop)));
        }
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
            BadValues = m.BadValues
        };
    }

    private static double ParseHour(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        var t = val.Trim();
        if (t == "-" || t == "N/A" || t == "Terminée") return 0;
        if (double.TryParse(t.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
            return num;
        return 0;
    }

    private static string? GetStationValue(AvancementOF of, string propertyName)
    {
        var prop = typeof(AvancementOF).GetProperty(propertyName);
        return prop?.GetValue(of)?.ToString();
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
