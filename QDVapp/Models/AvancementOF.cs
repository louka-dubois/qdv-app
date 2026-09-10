namespace QDVapp.Models;

public class AvancementOF
{
    public string? Id { get; set; }
    public bool Manuel { get; set; }
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
    public Dictionary<string, string> BadValues { get; set; } = new();
    public HashSet<string> CorrectedFields { get; set; } = new();
}

public static class StationColumns
{
    public static readonly (string Label, string Property)[] Stations =
    [
        ("Scie", nameof(AvancementOF.Scie)),
        ("Robot Plasma", nameof(AvancementOF.RobotPlasma)),
        ("Laser Tube", nameof(AvancementOF.LaserTube)),
        ("Table Plasma", nameof(AvancementOF.TablePlasma)),
        ("Table Laser", nameof(AvancementOF.TableLaser)),
        ("Pliage", nameof(AvancementOF.Pliage)),
        ("Roulage", nameof(AvancementOF.Roulage)),
        ("Machinage", nameof(AvancementOF.Machinage)),
        ("S/T Machine", nameof(AvancementOF.STMachine)),
        ("Montage", nameof(AvancementOF.Montage)),
        ("Tuyauterie Montage", nameof(AvancementOF.TuyauterieMontage)),
        ("Soudage", nameof(AvancementOF.Soudage)),
        ("Tuyauterie Soudage", nameof(AvancementOF.TuyauterieSoudage)),
        ("Soudage Robot", nameof(AvancementOF.SoudageRobot)),
        ("S/T Montage Soudage", nameof(AvancementOF.STMontageSoudage)),
        ("Inspection", nameof(AvancementOF.Inspection)),
        ("S/T Inspection", nameof(AvancementOF.STInspection)),
        ("Réparation", nameof(AvancementOF.Reparation)),
        ("Peinture", nameof(AvancementOF.Peinture)),
        ("S/T Peinture", nameof(AvancementOF.STPeinture)),
        ("Emballage", nameof(AvancementOF.Emballage)),
    ];
}
