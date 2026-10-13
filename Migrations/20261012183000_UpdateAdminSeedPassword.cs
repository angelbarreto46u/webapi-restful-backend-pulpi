using Microsoft.EntityFrameworkCore.Migrations;
using webapi_restful_backend_pulpi.Data;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations;

public partial class UpdateAdminSeedPassword : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.UpdateData(
        table: "Usuarios", keyColumn: "Id", keyValue: 1,
        column: "PasswordHash", value: "$2a$11$fviDAosITtmkFY31wK6rS.Nkt9vVRYci6O9IiB6citji1gn8a6AEW");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.UpdateData(
        table: "Usuarios", keyColumn: "Id", keyValue: 1,
        column: "PasswordHash", value: "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy");
}
