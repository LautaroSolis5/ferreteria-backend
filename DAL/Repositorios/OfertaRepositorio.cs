using BE.Entidades;
using L;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositorios
{
    public class OfertaRepositorio
    {
        private readonly Conexion  _conexion;
        private readonly AppLogger _logger;

        public OfertaRepositorio(Conexion conexion, AppLogger logger)
        {
            _conexion = conexion;
            _logger   = logger;
        }

        private static Oferta MapOferta(NpgsqlDataReader r) => new Oferta
        {
            IdOferta       = r.GetInt32(0),
            Titulo         = r.GetString(1),
            Descripcion    = r.IsDBNull(2)  ? string.Empty : r.GetString(2),
            PrecioOferta   = r.GetDecimal(3),
            PrecioAnterior = r.IsDBNull(4)  ? null : r.GetDecimal(4),
            ImagenUrl      = r.IsDBNull(5)  ? null : r.GetString(5),
            EsCombo        = r.GetBoolean(6),
            Activa         = r.GetBoolean(7),
            FechaInicio    = r.IsDBNull(8)  ? null : r.GetDateTime(8),
            FechaFin       = r.IsDBNull(9)  ? null : r.GetDateTime(9),
            FechaCreacion  = r.GetDateTime(10),
        };

        private const string SelectBase = @"
            SELECT IdOferta, Titulo, Descripcion, PrecioOferta, PrecioAnterior,
                   ImagenUrl, EsCombo, Activa, FechaInicio, FechaFin, FechaCreacion
            FROM Ofertas";

        private async Task HidratarProductosAsync(NpgsqlConnection conn, List<Oferta> lista)
        {
            if (!lista.Any()) return;
            var ids = lista.Select(o => o.IdOferta).ToArray();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT op.IdOferta, p.Id, p.Nombre, p.Descripcion, p.Precio,
                       p.Stock, p.CategoriaId, p.ImagenUrl, p.Activo, p.FechaCreacion
                FROM OfertaProductos op
                JOIN Productos p ON p.Id = op.IdProducto
                WHERE op.IdOferta = ANY(@ids)";
            cmd.Parameters.AddWithValue("ids", ids);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int idOferta = reader.GetInt32(0);
                var oferta   = lista.FirstOrDefault(o => o.IdOferta == idOferta);
                if (oferta == null) continue;
                var producto = new Producto
                {
                    Id            = reader.GetInt32(1),
                    Nombre        = reader.GetString(2),
                    Descripcion   = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Precio        = reader.GetDecimal(4),
                    Stock         = reader.GetInt32(5),
                    CategoriaId   = reader.GetInt32(6),
                    ImagenUrl     = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Activo        = reader.GetBoolean(8),
                    FechaCreacion = reader.GetDateTime(9),
                };
                oferta.Productos.Add(producto);
                oferta.ProductoIds.Add(producto.Id);
            }
        }

        public async Task<Oferta> ObtenerActivaAsync()
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE Activa = TRUE ORDER BY FechaCreacion DESC LIMIT 1";
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;
                var oferta = MapOferta(reader);
                reader.Close();
                await HidratarProductosAsync(conn, new List<Oferta> { oferta });
                return oferta;
            }
            catch (Exception ex) { _logger.LogError("DAL: Error al obtener oferta activa", ex); return null; }
        }

        public async Task<IEnumerable<Oferta>> ObtenerTodosAsync()
        {
            var lista = new List<Oferta>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " ORDER BY FechaCreacion DESC";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) lista.Add(MapOferta(reader));
                reader.Close();
                await HidratarProductosAsync(conn, lista);
            }
            catch (Exception ex) { _logger.LogError("DAL: Error al obtener todas las ofertas", ex); }
            return lista;
        }

        public async Task<Oferta> ObtenerPorIdAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE IdOferta = @id";
                cmd.Parameters.AddWithValue("id", id);
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;
                var oferta = MapOferta(reader);
                reader.Close();
                await HidratarProductosAsync(conn, new List<Oferta> { oferta });
                return oferta;
            }
            catch (Exception ex) { _logger.LogError($"DAL: Error al obtener oferta id={id}", ex); return null; }
        }

        public async Task<Oferta> CrearAsync(Oferta oferta)
        {
            using var conn = _conexion.ObtenerConexion();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                if (oferta.Activa)
                {
                    var des = conn.CreateCommand(); des.Transaction = tx;
                    des.CommandText = "UPDATE Ofertas SET Activa = FALSE";
                    await des.ExecuteNonQueryAsync();
                }
                var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Ofertas (Titulo, Descripcion, PrecioOferta, PrecioAnterior,
                        ImagenUrl, EsCombo, Activa, FechaInicio, FechaFin, FechaCreacion)
                    VALUES (@titulo,@desc,@precio,@precioAnt,@imagen,@esCombo,@activa,@inicio,@fin,NOW())
                    RETURNING IdOferta, FechaCreacion";
                cmd.Parameters.AddWithValue("titulo",    oferta.Titulo);
                cmd.Parameters.AddWithValue("desc",      (object)oferta.Descripcion    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("precio",    oferta.PrecioOferta);
                cmd.Parameters.AddWithValue("precioAnt", (object)oferta.PrecioAnterior ?? DBNull.Value);
                cmd.Parameters.AddWithValue("imagen",    (object)oferta.ImagenUrl      ?? DBNull.Value);
                cmd.Parameters.AddWithValue("esCombo",   oferta.EsCombo);
                cmd.Parameters.AddWithValue("activa",    oferta.Activa);
                cmd.Parameters.AddWithValue("inicio",    (object)oferta.FechaInicio    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("fin",       (object)oferta.FechaFin       ?? DBNull.Value);
                using var r = await cmd.ExecuteReaderAsync();
                await r.ReadAsync();
                oferta.IdOferta      = r.GetInt32(0);
                oferta.FechaCreacion = r.GetDateTime(1);
                r.Close();
                foreach (var pid in oferta.ProductoIds)
                {
                    var cp = conn.CreateCommand(); cp.Transaction = tx;
                    cp.CommandText = "INSERT INTO OfertaProductos (IdOferta, IdProducto) VALUES (@ido,@idp)";
                    cp.Parameters.AddWithValue("ido", oferta.IdOferta);
                    cp.Parameters.AddWithValue("idp", pid);
                    await cp.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
                return oferta;
            }
            catch (Exception ex) { await tx.RollbackAsync(); _logger.LogError("DAL: Error al crear oferta", ex); throw; }
        }

        public async Task<Oferta> ActualizarAsync(Oferta oferta)
        {
            using var conn = _conexion.ObtenerConexion();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                if (oferta.Activa)
                {
                    var des = conn.CreateCommand(); des.Transaction = tx;
                    des.CommandText = "UPDATE Ofertas SET Activa = FALSE WHERE IdOferta <> @id";
                    des.Parameters.AddWithValue("id", oferta.IdOferta);
                    await des.ExecuteNonQueryAsync();
                }
                var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = @"
                    UPDATE Ofertas SET Titulo=@titulo, Descripcion=@desc, PrecioOferta=@precio,
                        PrecioAnterior=@precioAnt, ImagenUrl=@imagen, EsCombo=@esCombo,
                        Activa=@activa, FechaInicio=@inicio, FechaFin=@fin
                    WHERE IdOferta=@id";
                cmd.Parameters.AddWithValue("titulo",    oferta.Titulo);
                cmd.Parameters.AddWithValue("desc",      (object)oferta.Descripcion    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("precio",    oferta.PrecioOferta);
                cmd.Parameters.AddWithValue("precioAnt", (object)oferta.PrecioAnterior ?? DBNull.Value);
                cmd.Parameters.AddWithValue("imagen",    (object)oferta.ImagenUrl      ?? DBNull.Value);
                cmd.Parameters.AddWithValue("esCombo",   oferta.EsCombo);
                cmd.Parameters.AddWithValue("activa",    oferta.Activa);
                cmd.Parameters.AddWithValue("inicio",    (object)oferta.FechaInicio    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("fin",       (object)oferta.FechaFin       ?? DBNull.Value);
                cmd.Parameters.AddWithValue("id",        oferta.IdOferta);
                await cmd.ExecuteNonQueryAsync();
                var del = conn.CreateCommand(); del.Transaction = tx;
                del.CommandText = "DELETE FROM OfertaProductos WHERE IdOferta=@id";
                del.Parameters.AddWithValue("id", oferta.IdOferta);
                await del.ExecuteNonQueryAsync();
                foreach (var pid in oferta.ProductoIds)
                {
                    var cp = conn.CreateCommand(); cp.Transaction = tx;
                    cp.CommandText = "INSERT INTO OfertaProductos (IdOferta, IdProducto) VALUES (@ido,@idp)";
                    cp.Parameters.AddWithValue("ido", oferta.IdOferta);
                    cp.Parameters.AddWithValue("idp", pid);
                    await cp.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
                return oferta;
            }
            catch (Exception ex) { await tx.RollbackAsync(); _logger.LogError($"DAL: Error al actualizar oferta", ex); throw; }
        }

        public async Task EliminarAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Ofertas WHERE IdOferta = @id";
                cmd.Parameters.AddWithValue("id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { _logger.LogError($"DAL: Error al eliminar oferta id={id}", ex); throw; }
        }

        public async Task<Oferta> ToggleActivaAsync(int id)
        {
            using var conn = _conexion.ObtenerConexion();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                var getCmd = conn.CreateCommand(); getCmd.Transaction = tx;
                getCmd.CommandText = "SELECT Activa FROM Ofertas WHERE IdOferta = @id";
                getCmd.Parameters.AddWithValue("id", id);
                var estadoActual = (bool)await getCmd.ExecuteScalarAsync();
                if (!estadoActual)
                {
                    var des = conn.CreateCommand(); des.Transaction = tx;
                    des.CommandText = "UPDATE Ofertas SET Activa = FALSE";
                    await des.ExecuteNonQueryAsync();
                }
                var cmd = conn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "UPDATE Ofertas SET Activa = NOT Activa WHERE IdOferta = @id";
                cmd.Parameters.AddWithValue("id", id);
                await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return await ObtenerPorIdAsync(id);
            }
            catch (Exception ex) { await tx.RollbackAsync(); _logger.LogError($"DAL: Error toggle oferta id={id}", ex); throw; }
        }
    }
}
