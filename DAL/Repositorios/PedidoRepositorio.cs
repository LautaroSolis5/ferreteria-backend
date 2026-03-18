using BE.Entidades;
using L;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositorios
{
    public class PedidoRepositorio
    {
        private readonly Conexion  _conexion;
        private readonly AppLogger _logger;

        public PedidoRepositorio(Conexion conexion, AppLogger logger)
        {
            _conexion = conexion;
            _logger   = logger;
        }

        // ─── Mapeos ───────────────────────────────────────────────────────────────

        private static Pedido MapPedido(NpgsqlDataReader r) => new Pedido
        {
            Id                 = r.GetInt32(0),
            UsuarioId          = r.GetInt32(1),
            NombreUsuario      = r.GetString(2),
            EmailUsuario       = r.GetString(3),
            Estado             = r.GetString(4),
            HorarioRetiro      = r.GetDateTime(5),
            FechaCreacion      = r.GetDateTime(6),
            FechaActualizacion = r.GetDateTime(7),
            Total              = r.GetDecimal(8),
            Notas              = r.IsDBNull(9) ? null : r.GetString(9)
        };

        private static PedidoItem MapItem(NpgsqlDataReader r) => new PedidoItem
        {
            Id             = r.GetInt32(0),
            PedidoId       = r.GetInt32(1),
            ProductoId     = r.GetInt32(2),
            NombreProducto = r.GetString(3),
            PrecioUnitario = r.GetDecimal(4),
            Cantidad       = r.GetInt32(5),
            Subtotal       = r.GetDecimal(6)
        };

        private static Pago? MapPago(NpgsqlDataReader r)
        {
            if (r.IsDBNull(10)) return null;
            return new Pago
            {
                Id                 = r.GetInt32(10),
                PedidoId           = r.GetInt32(0),
                MetodoPago         = r.GetString(11),
                Estado             = r.GetString(12),
                Monto              = r.GetDecimal(13),
                ExternalId         = r.IsDBNull(14) ? null : r.GetString(14),
                FechaCreacion      = r.GetDateTime(15),
                FechaActualizacion = r.GetDateTime(16)
            };
        }

        private const string SelectBase = @"
            SELECT p.Id, p.UsuarioId,
                   u.Nombre || ' ' || u.Apellido AS NombreUsuario, u.Email,
                   p.Estado, p.HorarioRetiro, p.FechaCreacion, p.FechaActualizacion,
                   p.Total, p.Notas,
                   pg.Id, pg.MetodoPago, pg.Estado, pg.Monto, pg.ExternalId,
                   pg.FechaCreacion, pg.FechaActualizacion
            FROM   Pedidos p
            INNER JOIN Usuarios u  ON u.IdUsuario = p.UsuarioId
            LEFT  JOIN Pagos    pg ON pg.PedidoId  = p.Id";

        // ─── Creación transaccional ───────────────────────────────────────────────

        /// <summary>
        /// Inserta Pedido + PedidoItems + Pago en una sola transacción.
        /// Devuelve el nuevo PedidoId, o 0 si falla.
        /// </summary>
        public async Task<int> AgregarAsync(Pedido pedido, Pago pago)
        {
            using var conn = _conexion.ObtenerConexion();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                // 1. INSERT Pedido
                var cmdPedido = conn.CreateCommand();
                cmdPedido.Transaction = tx;
                cmdPedido.CommandText = @"
                    INSERT INTO Pedidos
                        (UsuarioId, Estado, HorarioRetiro, FechaCreacion, FechaActualizacion, Total, Notas)
                    VALUES
                        (@uid, @estado, @retiro, NOW(), NOW(), @total, @notas)
                    RETURNING Id";
                cmdPedido.Parameters.AddWithValue("@uid",    pedido.UsuarioId);
                cmdPedido.Parameters.AddWithValue("@estado", pedido.Estado);
                cmdPedido.Parameters.AddWithValue("@retiro", pedido.HorarioRetiro);
                cmdPedido.Parameters.AddWithValue("@total",  pedido.Total);
                cmdPedido.Parameters.AddWithValue("@notas",  (object?)pedido.Notas ?? DBNull.Value);
                var pedidoId = Convert.ToInt32(await cmdPedido.ExecuteScalarAsync());

                // 2. INSERT PedidoItems
                foreach (var item in pedido.Items)
                {
                    var cmdItem = conn.CreateCommand();
                    cmdItem.Transaction = tx;
                    cmdItem.CommandText = @"
                        INSERT INTO PedidoItems
                            (PedidoId, ProductoId, NombreProducto, PrecioUnitario, Cantidad, Subtotal)
                        VALUES
                            (@pid, @prodId, @nombre, @precio, @cant, @sub)";
                    cmdItem.Parameters.AddWithValue("@pid",    pedidoId);
                    cmdItem.Parameters.AddWithValue("@prodId", item.ProductoId);
                    cmdItem.Parameters.AddWithValue("@nombre", item.NombreProducto);
                    cmdItem.Parameters.AddWithValue("@precio", item.PrecioUnitario);
                    cmdItem.Parameters.AddWithValue("@cant",   item.Cantidad);
                    cmdItem.Parameters.AddWithValue("@sub",    item.Subtotal);
                    await cmdItem.ExecuteNonQueryAsync();
                }

                // 3. INSERT Pago
                var cmdPago = conn.CreateCommand();
                cmdPago.Transaction = tx;
                cmdPago.CommandText = @"
                    INSERT INTO Pagos
                        (PedidoId, MetodoPago, Estado, Monto, ExternalId, FechaCreacion, FechaActualizacion)
                    VALUES
                        (@pid, @metodo, @estado, @monto, @extId, NOW(), NOW())";
                cmdPago.Parameters.AddWithValue("@pid",    pedidoId);
                cmdPago.Parameters.AddWithValue("@metodo", pago.MetodoPago);
                cmdPago.Parameters.AddWithValue("@estado", pago.Estado);
                cmdPago.Parameters.AddWithValue("@monto",  pago.Monto);
                cmdPago.Parameters.AddWithValue("@extId",  (object?)pago.ExternalId ?? DBNull.Value);
                await cmdPago.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                _logger.LogInfo($"DAL: Pedido creado Id={pedidoId} UsuarioId={pedido.UsuarioId}");
                return pedidoId;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError("DAL: Error al crear pedido — transacción revertida", ex);
                return 0;
            }
        }

        // ─── Obtener por ID (con Items y Pago hidratados) ────────────────────────

        public async Task<Pedido?> ObtenerPorIdAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();

                var cmdPedido = conn.CreateCommand();
                cmdPedido.CommandText = SelectBase + " WHERE p.Id = @id";
                cmdPedido.Parameters.AddWithValue("@id", id);

                Pedido? pedido = null;
                using (var reader = await cmdPedido.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        pedido = MapPedido(reader);
                        pedido.Pago = MapPago(reader);
                    }
                }
                if (pedido == null) return null;

                var cmdItems = conn.CreateCommand();
                cmdItems.CommandText = @"
                    SELECT Id, PedidoId, ProductoId, NombreProducto, PrecioUnitario, Cantidad, Subtotal
                    FROM   PedidoItems
                    WHERE  PedidoId = @pid
                    ORDER BY Id";
                cmdItems.Parameters.AddWithValue("@pid", id);
                using var readerItems = await cmdItems.ExecuteReaderAsync();
                while (await readerItems.ReadAsync())
                    pedido.Items.Add(MapItem(readerItems));

                return pedido;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al obtener pedido Id={id}", ex);
                return null;
            }
        }

        // ─── Obtener por usuario ──────────────────────────────────────────────────

        public async Task<IEnumerable<Pedido>> ObtenerPorUsuarioAsync(int usuarioId)
        {
            var lista = new List<Pedido>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE p.UsuarioId = @uid ORDER BY p.FechaCreacion DESC";
                cmd.Parameters.AddWithValue("@uid", usuarioId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var p = MapPedido(reader);
                    p.Pago = MapPago(reader);
                    lista.Add(p);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al obtener pedidos UsuarioId={usuarioId}", ex);
            }
            return lista;
        }

        // ─── Obtener todos (admin) ────────────────────────────────────────────────

        public async Task<IEnumerable<Pedido>> ObtenerTodosAsync()
        {
            var lista = new List<Pedido>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " ORDER BY p.FechaCreacion DESC";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var p = MapPedido(reader);
                    p.Pago = MapPago(reader);
                    lista.Add(p);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al obtener todos los pedidos", ex);
            }
            return lista;
        }

        // ─── Actualizar estado ────────────────────────────────────────────────────

        public async Task<bool> ActualizarEstadoAsync(int id, string nuevoEstado)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Pedidos
                    SET    Estado = @estado, FechaActualizacion = NOW()
                    WHERE  Id = @id";
                cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                cmd.Parameters.AddWithValue("@id",     id);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Pedido Id={id} estado -> {nuevoEstado}");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al actualizar estado pedido Id={id}", ex);
                return false;
            }
        }
    }
}
