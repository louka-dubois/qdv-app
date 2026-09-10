using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDVapp.Migrations
{
    /// <inheritdoc />
    public partial class AddProjetUsine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Statut = table.Column<string>(type: "TEXT", nullable: true),
                    NoCmd = table.Column<string>(type: "TEXT", nullable: true),
                    RefOF = table.Column<string>(type: "TEXT", nullable: true),
                    CodeArticle = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CommentaireOF = table.Column<string>(type: "TEXT", nullable: true),
                    Vendeur = table.Column<string>(type: "TEXT", nullable: true),
                    DateRequise = table.Column<DateTime>(type: "TEXT", nullable: true),
                    JrsSTPlanifie = table.Column<double>(type: "REAL", nullable: true),
                    Planifie = table.Column<double>(type: "REAL", nullable: true),
                    Fait = table.Column<double>(type: "REAL", nullable: true),
                    DeclAv = table.Column<double>(type: "REAL", nullable: true),
                    Restant = table.Column<double>(type: "REAL", nullable: true),
                    TempsTotalFinEstime = table.Column<double>(type: "REAL", nullable: true),
                    Diff = table.Column<string>(type: "TEXT", nullable: true),
                    PctDiff = table.Column<string>(type: "TEXT", nullable: true),
                    NoteFab = table.Column<string>(type: "TEXT", nullable: true),
                    Manuel = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projets", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Projets");
        }
    }
}
