using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QDVapp.Models;

public class ProjetUsine
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = "";
    public string? Statut { get; set; }
    public string? NoCmd { get; set; }
    public string? RefOF { get; set; }
    public string? CodeArticle { get; set; }
    public string? Description { get; set; }
    public string? CommentaireOF { get; set; }
    public string? Vendeur { get; set; }
    public DateTime? DateRequise { get; set; }
    public double? JrsSTPlanifie { get; set; }
    public double? Planifie { get; set; }
    public double? Fait { get; set; }
    public double? DeclAv { get; set; }
    public double? Restant { get; set; }
    public double? TempsTotalFinEstime { get; set; }
    public string? Diff { get; set; }
    public string? PctDiff { get; set; }
    public string? NoteFab { get; set; }
    public bool Manuel { get; set; }

    [NotMapped]
    public Dictionary<string, string> BadValues { get; set; } = new();

    [NotMapped]
    public HashSet<string> CorrectedFields { get; set; } = new();
}
