using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using webapi_restful_backend_pulpi.Data;

#nullable disable

namespace webapi_restful_backend_pulpi.Migrations;

[DbContext(typeof(PulpiDbContext))]
[Migration("20261013100000_AddFinancialCoreAndRemoveClients")]
partial class AddFinancialCoreAndRemoveClients
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => PulpiModelSnapshotDefinition.Build(modelBuilder);
}
