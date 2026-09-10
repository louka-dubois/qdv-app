using Microsoft.EntityFrameworkCore;
using QDVapp.Data;
using QDVapp.Models;

namespace QDVapp.Services;

public enum CorrectionFieldType { String, Double, Int, DateTime }

public static class CorrectionFields
{
    public const string PageAtelier = "Atelier";
    public const string PageAvancement = "Avancement";

    public static readonly Dictionary<string, CorrectionFieldType> Atelier =
        new(StringComparer.Ordinal)
        {
            ["Statut"] = CorrectionFieldType.String,
            ["NoCmd"] = CorrectionFieldType.String,
            ["RefOF"] = CorrectionFieldType.String,
            ["CodeArticle"] = CorrectionFieldType.String,
            ["Description"] = CorrectionFieldType.String,
            ["CommentaireOF"] = CorrectionFieldType.String,
            ["Vendeur"] = CorrectionFieldType.String,
            ["NoteFab"] = CorrectionFieldType.String,
            ["DateRequise"] = CorrectionFieldType.DateTime,
            ["JrsSTPlanifie"] = CorrectionFieldType.Double,
            ["Planifie"] = CorrectionFieldType.Double,
            ["Fait"] = CorrectionFieldType.Double,
            ["Restant"] = CorrectionFieldType.Double,
        };

    public static readonly Dictionary<string, CorrectionFieldType> Avancement =
        new(StringComparer.Ordinal)
        {
            ["Projet"] = CorrectionFieldType.String,
            ["RefOF"] = CorrectionFieldType.String,
            ["StatutOF"] = CorrectionFieldType.String,
            ["DescriptionArticle"] = CorrectionFieldType.String,
            ["Priorite"] = CorrectionFieldType.Int,
            ["RequisFab"] = CorrectionFieldType.DateTime,
            ["RequisFinal"] = CorrectionFieldType.DateTime,
            ["Scie"] = CorrectionFieldType.String,
            ["RobotPlasma"] = CorrectionFieldType.String,
            ["LaserTube"] = CorrectionFieldType.String,
            ["TablePlasma"] = CorrectionFieldType.String,
            ["TableLaser"] = CorrectionFieldType.String,
            ["Pliage"] = CorrectionFieldType.String,
            ["Roulage"] = CorrectionFieldType.String,
            ["Machinage"] = CorrectionFieldType.String,
            ["STMachine"] = CorrectionFieldType.String,
            ["Montage"] = CorrectionFieldType.String,
            ["TuyauterieMontage"] = CorrectionFieldType.String,
            ["Soudage"] = CorrectionFieldType.String,
            ["TuyauterieSoudage"] = CorrectionFieldType.String,
            ["SoudageRobot"] = CorrectionFieldType.String,
            ["STMontageSoudage"] = CorrectionFieldType.String,
            ["Inspection"] = CorrectionFieldType.String,
            ["STInspection"] = CorrectionFieldType.String,
            ["Reparation"] = CorrectionFieldType.String,
            ["Peinture"] = CorrectionFieldType.String,
            ["STPeinture"] = CorrectionFieldType.String,
            ["Emballage"] = CorrectionFieldType.String,
            ["CommentaireInspection"] = CorrectionFieldType.String,
            ["CommentaireOF"] = CorrectionFieldType.String,
        };
}

public class CorrectionService
{
    private readonly ApplicationDbContext _db;

    public CorrectionService(ApplicationDbContext db) => _db = db;

    public string? Validate(string page, string field, string raw, out string normalized)
    {
        var reg = page == CorrectionFields.PageAvancement ? CorrectionFields.Avancement : CorrectionFields.Atelier;
        normalized = raw.Trim();

        if (!reg.TryGetValue(field, out var type))
        {
            normalized = "";
            return "Champ inconnu.";
        }
        if (string.IsNullOrWhiteSpace(normalized))
            return "Entrez une valeur valide.";

        switch (type)
        {
            case CorrectionFieldType.Double:
                normalized = NormalizeDouble(normalized);
                if (!double.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    normalized = "";
                    return "Cette valeur doit être un nombre.";
                }
                break;
            case CorrectionFieldType.Int:
                if (!int.TryParse(normalized, out _))
                {
                    normalized = "";
                    return "Cette valeur doit être un nombre entier.";
                }
                break;
            case CorrectionFieldType.DateTime:
                if (DateTime.TryParse(normalized, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                {
                    normalized = dt.ToString("yyyy-MM-dd");
                }
                else
                {
                    normalized = "";
                    return "Valeur de date invalide (format AAAA-MM-JJ attendu).";
                }
                break;
        }

        return null;
    }

    private static string NormalizeDouble(string raw)
    {
        raw = raw.Trim();
        if (raw.IndexOf(',') >= 0)
            raw = raw.Replace(',', '.');
        return raw;
    }

    public async Task<ProjectCorrection?> UpsertAsync(string page, string projectKey, string field, string value)
    {
        var existing = await _db.Corrections
            .FirstOrDefaultAsync(c => c.Page == page && c.ProjectKey == projectKey && c.Field == field);

        if (existing is not null)
        {
            existing.Value = value;
        }
        else
        {
            existing = new ProjectCorrection { Page = page, ProjectKey = projectKey, Field = field, Value = value };
            _db.Corrections.Add(existing);
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task RemoveAsync(string page, string projectKey, string field)
    {
        var existing = await _db.Corrections
            .FirstOrDefaultAsync(c => c.Page == page && c.ProjectKey == projectKey && c.Field == field);
        if (existing is not null)
        {
            _db.Corrections.Remove(existing);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<ProjectCorrection>> GetAllAsync(string page)
        => await _db.Corrections.AsNoTracking().Where(c => c.Page == page).ToListAsync();

    public void ApplyAtelier(IEnumerable<ProjetUsine> projets, IEnumerable<ProjectCorrection> corrections)
    {
        foreach (var p in projets)
        {
            var recompute = false;
            foreach (var c in corrections.Where(c => c.ProjectKey == p.NoCmd))
            {
                if (!CorrectionFields.Atelier.TryGetValue(c.Field, out var type))
                    continue;

                switch (type)
                {
                    case CorrectionFieldType.String:
                        p.SetStringField(c.Field, c.Value);
                        p.BadValues.Remove(c.Field);
                        p.CorrectedFields.Add(c.Field);
                        break;
                    case CorrectionFieldType.Double:
                        if (double.TryParse(c.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                        {
                            switch (c.Field)
                            {
                                case "JrsSTPlanifie": p.JrsSTPlanifie = d; break;
                                case "Planifie": p.Planifie = d; recompute = true; break;
                                case "Fait": p.Fait = d; recompute = true; break;
                                case "Restant": p.Restant = d; recompute = true; break;
                            }
                            p.BadValues.Remove(c.Field);
                            p.CorrectedFields.Add(c.Field);
                        }
                        break;
                    case CorrectionFieldType.DateTime:
                        if (DateTime.TryParse(c.Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                        {
                            p.DateRequise = dt;
                            p.BadValues.Remove(c.Field);
                            p.CorrectedFields.Add(c.Field);
                        }
                        break;
                }
            }

            if (recompute)
            {
                var plan = p.Planifie;
                var rest = p.Restant;
                p.TempsTotalFinEstime = p.Fait.HasValue || rest.HasValue ? (p.Fait ?? 0) + (rest ?? 0) : null;
                var diff = plan.HasValue && p.TempsTotalFinEstime.HasValue ? plan - p.TempsTotalFinEstime : null;
                p.Diff = diff?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                p.PctDiff = PctDiffString(diff, plan);
                p.DeclAv = plan is > 0 && rest.HasValue ? (plan - rest) / plan : plan == 0 ? 0 : null;
            }
        }
    }

    public void ApplyAvancement(IEnumerable<AvancementOF> ordres, IEnumerable<ProjectCorrection> corrections)
    {
        var byKey = corrections.ToLookup(c => c.ProjectKey);
        foreach (var o in ordres)
        {
            foreach (var c in byKey[o.Projet ?? ""])
            {
                if (!CorrectionFields.Avancement.TryGetValue(c.Field, out var type))
                    continue;

                switch (type)
                {
                    case CorrectionFieldType.String:
                        o.SetStringField(c.Field, c.Value);
                        o.BadValues.Remove(c.Field);
                        o.CorrectedFields.Add(c.Field);
                        break;
                    case CorrectionFieldType.Int:
                        if (int.TryParse(c.Value, out var i))
                        {
                            o.Priorite = i;
                            o.BadValues.Remove(c.Field);
                            o.CorrectedFields.Add(c.Field);
                        }
                        break;
                    case CorrectionFieldType.DateTime:
                        if (DateTime.TryParse(c.Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                        {
                            if (c.Field == "RequisFab") o.RequisFab = dt;
                            else o.RequisFinal = dt;
                            o.BadValues.Remove(c.Field);
                            o.CorrectedFields.Add(c.Field);
                        }
                        break;
                }
            }
        }
    }

    private static string? PctDiffString(double? diff, double? plan)
    {
        if (diff.HasValue && plan is > 0)
            return (diff / plan)?.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        if (plan == 0)
            return diff == 0 ? "0" : "Erreur";
        return null;
    }
}

public static class CorrectionFieldSetter
{
    public static void SetStringField(this ProjetUsine p, string field, string value)
    {
        var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        switch (field)
        {
            case "Statut": p.Statut = v; break;
            case "NoCmd": p.NoCmd = v; break;
            case "RefOF": p.RefOF = v; break;
            case "CodeArticle": p.CodeArticle = v; break;
            case "Description": p.Description = v; break;
            case "CommentaireOF": p.CommentaireOF = v; break;
            case "Vendeur": p.Vendeur = v; break;
            case "NoteFab": p.NoteFab = v; break;
        }
    }

    public static void SetStringField(this AvancementOF o, string field, string value)
    {
        var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        switch (field)
        {
            case "Projet": o.Projet = v; break;
            case "RefOF": o.RefOF = v; break;
            case "StatutOF": o.StatutOF = v; break;
            case "DescriptionArticle": o.DescriptionArticle = v; break;
            case "Scie": o.Scie = v; break;
            case "RobotPlasma": o.RobotPlasma = v; break;
            case "LaserTube": o.LaserTube = v; break;
            case "TablePlasma": o.TablePlasma = v; break;
            case "TableLaser": o.TableLaser = v; break;
            case "Pliage": o.Pliage = v; break;
            case "Roulage": o.Roulage = v; break;
            case "Machinage": o.Machinage = v; break;
            case "STMachine": o.STMachine = v; break;
            case "Montage": o.Montage = v; break;
            case "TuyauterieMontage": o.TuyauterieMontage = v; break;
            case "Soudage": o.Soudage = v; break;
            case "TuyauterieSoudage": o.TuyauterieSoudage = v; break;
            case "SoudageRobot": o.SoudageRobot = v; break;
            case "STMontageSoudage": o.STMontageSoudage = v; break;
            case "Inspection": o.Inspection = v; break;
            case "STInspection": o.STInspection = v; break;
            case "Reparation": o.Reparation = v; break;
            case "Peinture": o.Peinture = v; break;
            case "STPeinture": o.STPeinture = v; break;
            case "Emballage": o.Emballage = v; break;
            case "CommentaireInspection": o.CommentaireInspection = v; break;
            case "CommentaireOF": o.CommentaireOF = v; break;
        }
    }
}
