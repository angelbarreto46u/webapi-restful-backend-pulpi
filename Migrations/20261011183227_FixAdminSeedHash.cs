using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations
{
    /// <inheritdoc />
    public partial class FixAdminSeedHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$D617JfxeQKa8yRsjedCWRO5xyPEbzwrxjrSvjMtN1UI/HPnRC90ye");
        }
    }
}
