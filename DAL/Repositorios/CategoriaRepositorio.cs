using ABST.Interfaces;
using BE.Entidades;
using L;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositorios
{
    public class CategoriaRepositorio : IRepositorioBase<Categoria>
    {
        private readonly Conexion _conexion;
        private readonly AppLogger _logger;

        public CategoriaRepositorio(Conexion conexion, AppLogger logger)
        {
            _conexion = conexion;
            _logger = logger;
        }

        private static Categoria MapCategoria(NpgsqlDataReader r) => new Categoria
        {
            Id          = r.GetInt32(0),
            Nombre      = r.GetString(1),
            Descripcion = r.IsDBNull(2) ? string.Empty : r.GetString(2),
            Activo      = r.GetBoolean(3)
        };

        public async Task<IEnumerable<Categoria>> ObtenerTodosAsync()
        {
            var lista = new List<Categoria>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Nombre, Descripcion, Activo FROM Categorias WHERE Activo = TRUE ORDER BY Nombre";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    lista.Add(MapCategoria(reader));
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al obtener todas las categorías", ex);
            }
            return lista;
        }

        public async Task<Categoria> ObtenerPorIdAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Nombre, Descripcion, Activo FROM Categorias WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return MapCategoria(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al obtener categoría Id={id}", ex);
            }
            return null;
        }

        public async Task<int> AgregarAsync(Categoria entidad)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Categorias (Nombre, Descripcion, Activo)
                    VALUES (@nombre, @desc, TRUE)
                    RETURNING Id";
                cmd.Parameters.AddWithValue("@nombre", entidad.Nombre);
                cmd.Parameters.AddWithValue("@desc", (object?)entidad.Descripcion ?? DBNull.Value);
                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                _logger.LogInfo($"DAL: Categoría agregada Id={id}");
                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al agregar categoría", ex);
                return 0;
            }
        }

        public async Task<bool> ActualizarAsync(Categoria entidad)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Categorias SET Nombre = @nombre, Descripcion = @desc
                    WHERE Id = @id";
                cmd.Parameters.AddWithValue("@nombre", entidad.Nombre);
                cmd.Parameters.AddWithValue("@desc", (object?)entidad.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", entidad.Id);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Categoría Id={entidad.Id} actualizada");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al actualizar categoría Id={entidad.Id}", ex);
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
                cmd.CommandText = "UPDATE Categorias SET Activo = FALSE WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Categoría Id={id} desactivada (baja lógica)");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al eliminar categoría Id={id}", ex);
                return false;
            }
        }
    }
}
