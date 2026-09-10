using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDVapp.Migrations
{
    /// <inheritdoc />
    public partial class AddManuelOF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManuelOFs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Projet = table.Column<string>(type: "TEXT", nullable: true),
                    RefOF = table.Column<string>(type: "TEXT", nullable: true),
                    StatutOF = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionArticle = table.Column<string>(type: "TEXT", nullable: true),
                    Priorite = table.Column<int>(type: "INTEGER", nullable: true),
                    RequisFab = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequisFinal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Scie = table.Column<string>(type: "TEXT", nullable: true),
                    RobotPlasma = table.Column<string>(type: "TEXT", nullable: true),
                    LaserTube = table.Column<string>(type: "TEXT", nullable: true),
                    TablePlasma = table.Column<string>(type: "TEXT", nullable: true),
                    TableLaser = table.Column<string>(type: "TEXT", nullable: true),
                    Pliage = table.Column<string>(type: "TEXT", nullable: true),
                    Roulage = table.Column<string>(type: "TEXT", nullable: true),
                    Machinage = table.Column<string>(type: "TEXT", nullable: true),
                    STMachine = table.Column<string>(type: "TEXT", nullable: true),
                    Montage = table.Column<string>(type: "TEXT", nullable: true),
                    TuyauterieMontage = table.Column<string>(type: "TEXT", nullable: true),
                    Soudage = table.Column<string>(type: "TEXT", nullable: true),
                    TuyauterieSoudage = table.Column<string>(type: "TEXT", nullable: true),
                    SoudageRobot = table.Column<string>(type: "TEXT", nullable: true),
                    STMontageSoudage = table.Column<string>(type: "TEXT", nullable: true),
                    Inspection = table.Column<string>(type: "TEXT", nullable: true),
                    STInspection = table.Column<string>(type: "TEXT", nullable: true),
                    Reparation = table.Column<string>(type: "TEXT", nullable: true),
                    Peinture = table.Column<string>(type: "TEXT", nullable: true),
                    STPeinture = table.Column<string>(type: "TEXT", nullable: true),
                    Emballage = table.Column<string>(type: "TEXT", nullable: true),
                    CommentaireInspection = table.Column<string>(type: "TEXT", nullable: true),
                    CommentaireOF = table.Column<string>(type: "TEXT", nullable: true),
                    Manuel = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManuelOFs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManuelOFs");
        }
    }
}
