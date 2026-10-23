using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations
{
    /// <inheritdoc />
    public partial class SeventhCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Diferencia",
                table: "SesionesCaja");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Diferencia",
                table: "SesionesCaja",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
