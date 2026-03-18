using BE.Entidades;
using L;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositorios
{
    public class NotificacionRepositorio
    {
        private readonly Conexion  _conexion;
        private readonly AppLogger _logger;

        public NotificacionRepositorio(Conexion conexion, AppLogger logger)
        {
            _conexion = conexion;
            _logger   = logger;
        }

        private const string SelectBase = @"
            SELECT n.Id, n.PedidoId, n.Mensaje, n.Leida, n.FechaCreacion,
                   u.Nombre || ' ' || u.Apellido AS NombreUsuario, u.Email,
                   p.HorarioRetiro,
                   COALESCE(pg.MetodoPago, ''),
                   p.Total
            FROM   Notificaciones n
            INNER JOIN Pedidos  p  ON p.Id        = n.PedidoId
            INNER JOIN Usuarios u  ON u.IdUsuario = p.UsuarioId
            LEFT  JOIN Pagos    pg ON pg.PedidoId = n.PedidoId";

        private static Notificacion MapNotificacion(NpgsqlDataReader r) => new Notificacion
        {
            Id            = r.GetInt32(0),
            PedidoId      = r.GetInt32(1),
            Mensaje       = r.GetString(2),
            Leida         = r.GetBoolean(3),
            FechaCreacion = r.GetDateTime(4),
            NombreUsuario = r.GetString(5),
            EmailUsuario  = r.GetString(6),
            HorarioRetiro = r.GetDateTime(7),
            MetodoPago    = r.GetString(8),
            Total         = r.GetDecimal(9)
        };

        public async Task<int> AgregarAsync(Notificacion notificacion)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Notificaciones (PedidoId, Mensaje, Leida, FechaCreacion)
                    VALUES (@pid, @mensaje, FALSE, NOW())
                    RETURNING Id";
                cmd.Parameters.AddWithValue("@pid",     notificacion.PedidoId);
                cmd.Parameters.AddWithValue("@mensaje", notificacion.Mensaje);
                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                _logger.LogInfo($"DAL: Notificacion creada Id={id} PedidoId={notificacion.PedidoId}");
                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al crear notificacion", ex);
                return 0;
            }
        }

        public async Task<IEnumerable<Notificacion>> ObtenerTodasAsync()
        {
            var lista = new List<Notificacion>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " ORDER BY n.FechaCreacion DESC";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(MapNotificacion(reader));
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al obtener notificaciones", ex);
            }
            return lista;
        }

        public async Task<bool> MarcarComoLeidaAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Notificaciones SET Leida = TRUE WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Notificacion Id={id} marcada como leída");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al marcar notificacion Id={id}", ex);
                return false;
            }
        }

        public async Task<int> ContarNoLeidasAsync()
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Notificaciones WHERE Leida = FALSE";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al contar notificaciones no leídas", ex);
                return 0;
            }
        }
    }
}
