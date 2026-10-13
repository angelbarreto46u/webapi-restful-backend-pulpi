using System.Text.Json;
using webapi_restful_backend_pulpi.Data;
using webapi_restful_backend_pulpi.Models;

namespace webapi_restful_backend_pulpi.Services;

public static class AuditoriaHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Registrar(PulpiDbContext context, int? idUsuario, string entidad, long? idRegistro,
        string accion, object? antes = null, object? despues = null, string? motivo = null)
    {
        context.AuditoriaAcciones.Add(new AuditoriaAccion
        {
            IdUsuario = idUsuario,
            Entidad = entidad,
            IdRegistro = idRegistro,
            Accion = accion,
            FechaHora = DateTime.UtcNow,
            DatosAnteriores = antes is null ? null : JsonSerializer.Serialize(antes, JsonOptions),
            DatosNuevos = despues is null ? null : JsonSerializer.Serialize(despues, JsonOptions),
            Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()
        });
    }
}
