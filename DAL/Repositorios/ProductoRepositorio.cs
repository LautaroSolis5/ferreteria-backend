using ABST.Interfaces;
using BE.Entidades;
using L;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositorios
{
    public class ProductoRepositorio : IRepositorioBase<Producto>
    {
        private readonly Conexion _conexion;
        private readonly AppLogger _logger;

        public ProductoRepositorio(Conexion conexion, AppLogger logger)
        {
            _conexion = conexion;
            _logger = logger;
        }

        private static Producto MapProducto(NpgsqlDataReader r) => new Producto
        {
            Id            = r.GetInt32(0),
            Nombre        = r.GetString(1),
            Descripcion   = r.IsDBNull(2)  ? string.Empty : r.GetString(2),
            Precio        = r.GetDecimal(3),
            Stock         = r.GetInt32(4),
            CategoriaId   = r.GetInt32(5),
            ImagenUrl     = r.IsDBNull(6)  ? null : r.GetString(6),
            Activo        = r.GetBoolean(7),
            FechaCreacion = r.GetDateTime(8)
        };

        private const string SelectBase = @"
            SELECT Id, Nombre, Descripcion, Precio, Stock, CategoriaId, ImagenUrl, Activo, FechaCreacion
            FROM Productos";

        public async Task<IEnumerable<Producto>> ObtenerTodosAsync()
        {
            var lista = new List<Producto>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE Activo = TRUE ORDER BY Nombre";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(MapProducto(reader));
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al obtener todos los productos", ex);
            }
            return lista;
        }

        public async Task<Producto> ObtenerPorIdAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return MapProducto(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al obtener producto Id={id}", ex);
            }
            return null;
        }

        /// <summary>
        /// Método adicional específico de Producto (no está en IRepositorioBase).
        /// BLL lo usa directamente al inyectar ProductoRepositorio.
        /// </summary>
        public async Task<IEnumerable<Producto>> ObtenerPorCategoriaAsync(int categoriaId)
        {
            var lista = new List<Producto>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE CategoriaId = @catId AND Activo = TRUE ORDER BY Nombre";
                cmd.Parameters.AddWithValue("@catId", categoriaId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(MapProducto(reader));
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al obtener productos por categoría Id={categoriaId}", ex);
            }
            return lista;
        }

        public async Task<int> AgregarAsync(Producto entidad)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Productos (Nombre, Descripcion, Precio, Stock, CategoriaId, ImagenUrl, Activo, FechaCreacion)
                    VALUES (@nombre, @desc, @precio, @stock, @catId, @imagen, TRUE, @fecha)
                    RETURNING Id";
                cmd.Parameters.AddWithValue("@nombre", entidad.Nombre);
                cmd.Parameters.AddWithValue("@desc",   (object?)entidad.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@precio", entidad.Precio);
                cmd.Parameters.AddWithValue("@stock",  entidad.Stock);
                cmd.Parameters.AddWithValue("@catId",  entidad.CategoriaId);
                cmd.Parameters.AddWithValue("@imagen", (object?)entidad.ImagenUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha",  DateTime.UtcNow);
                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                _logger.LogInfo($"DAL: Producto agregado Id={id}");
                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al agregar producto", ex);
                return 0;
            }
        }

        public async Task<bool> ActualizarAsync(Producto entidad)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Productos
                    SET Nombre = @nombre, Descripcion = @desc, Precio = @precio,
                        Stock = @stock, CategoriaId = @catId, ImagenUrl = @imagen
                    WHERE Id = @id";
                cmd.Parameters.AddWithValue("@nombre", entidad.Nombre);
                cmd.Parameters.AddWithValue("@desc",   (object?)entidad.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@precio", entidad.Precio);
                cmd.Parameters.AddWithValue("@stock",  entidad.Stock);
                cmd.Parameters.AddWithValue("@catId",  entidad.CategoriaId);
                cmd.Parameters.AddWithValue("@imagen", (object?)entidad.ImagenUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id",     entidad.Id);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Producto Id={entidad.Id} actualizado");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al actualizar producto Id={entidad.Id}", ex);
                return false;
            }
        }

        public async Task<bool> EliminarAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Productos SET Activo = FALSE WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Producto Id={id} desactivado (baja lógica)");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al eliminar producto Id={id}", ex);
                return false;
            }
        }
    }
}
