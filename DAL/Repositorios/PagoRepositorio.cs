using BE.Entidades;
using L;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace DAL.Repositorios
{
    public class PagoRepositorio
    {
        private readonly Conexion  _conexion;
        private readonly AppLogger _logger;

        public PagoRepositorio(Conexion conexion, AppLogger logger)
        {
            _conexion = conexion;
            _logger   = logger;
        }

        private static Pago MapPago(NpgsqlDataReader r) => new Pago
        {
            Id                 = r.GetInt32(0),
            PedidoId           = r.GetInt32(1),
            MetodoPago         = r.GetString(2),
            Estado             = r.GetString(3),
            Monto              = r.GetDecimal(4),
            ExternalId         = r.IsDBNull(5) ? null : r.GetString(5),
            FechaCreacion      = r.GetDateTime(6),
            FechaActualizacion = r.GetDateTime(7)
        };

        public async Task<Pago?> ObtenerPorPedidoAsync(int pedidoId)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT Id, PedidoId, MetodoPago, Estado, Monto, ExternalId,
                           FechaCreacion, FechaActualizacion
                    FROM   Pagos
                    WHERE  PedidoId = @pid";
                cmd.Parameters.AddWithValue("@pid", pedidoId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync()) return MapPago(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al obtener pago PedidoId={pedidoId}", ex);
            }
            return null;
        }

        /// <summary>
        /// Actualiza estado y opcionalmente el ExternalId del pago.
        /// Usado por webhook de MercadoPago y por el admin para aprobar pagos en local.
        /// </summary>
        public async Task<bool> ActualizarEstadoAsync(int pedidoId, string nuevoEstado, string? externalId = null)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Pagos
                    SET    Estado             = @estado,
                           ExternalId         = COALESCE(@extId, ExternalId),
                           FechaActualizacion = NOW()
                    WHERE  PedidoId = @pid";
                cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                cmd.Parameters.AddWithValue("@extId",  (object?)externalId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pid",    pedidoId);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Pago PedidoId={pedidoId} estado -> {nuevoEstado}");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al actualizar pago PedidoId={pedidoId}", ex);
                return false;
            }
        }
    }
}
