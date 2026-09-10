using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDVapp.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Corrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Page = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectKey = table.Column<string>(type: "TEXT", nullable: false),
                    Field = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Corrections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Corrections_Page_ProjectKey_Field",
                table: "Corrections",
                columns: new[] { "Page", "ProjectKey", "Field" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Corrections");
        }
    }
}
