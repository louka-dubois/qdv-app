using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QDVapp.Models;

public class ManuelOF
{
    [Key]
    public string Id { get; set; } = string.Empty;
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
    public bool Manuel { get; set; }

    [NotMapped]
    public Dictionary<string, string> BadValues { get; set; } = new();

    [NotMapped]
    public HashSet<string> CorrectedFields { get; set; } = new();
}
