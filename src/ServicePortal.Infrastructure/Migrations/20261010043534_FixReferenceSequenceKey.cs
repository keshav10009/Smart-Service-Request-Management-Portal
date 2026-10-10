using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServicePortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixReferenceSequenceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceSequences");

            migrationBuilder.CreateTable(
                name: "ReferenceSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceSequences_Year",
                table: "ReferenceSequences",
                column: "Year",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceSequences");

            migrationBuilder.CreateTable(
                name: "ReferenceSequences",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceSequences", x => x.Year);
                });
        }
    }
}
