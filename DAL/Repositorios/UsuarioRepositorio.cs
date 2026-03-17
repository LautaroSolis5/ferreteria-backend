using BE.Entidades;
using L;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositorios
{
    /// <summary>
    /// Acceso a datos de Usuarios. El SELECT siempre hace JOIN con Roles
    /// para obtener RolNombre sin necesitar un repositorio adicional.
    /// </summary>
    public class UsuarioRepositorio
    {
        private readonly Conexion  _conexion;
        private readonly AppLogger _logger;

        private const string SelectBase = @"
            SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Email, u.PasswordHash,
                   u.RolId, r.NombreRol, u.Activo, u.AuthProvider,
                   u.ProviderUserId, u.FechaCreacion, u.UltimoLogin
            FROM   Usuarios u
            INNER JOIN Roles r ON r.IdRol = u.RolId";

        public UsuarioRepositorio(Conexion conexion, AppLogger logger)
        {
            _conexion = conexion;
            _logger   = logger;
        }

        // ─── Mapeo ───────────────────────────────────────────────────────────────

        private static Usuario MapUsuario(NpgsqlDataReader r) => new Usuario
        {
            IdUsuario      = r.GetInt32(0),
            Nombre         = r.GetString(1),
            Apellido       = r.GetString(2),
            Email          = r.GetString(3),
            PasswordHash   = r.IsDBNull(4)  ? null              : r.GetString(4),
            RolId          = r.GetInt32(5),
            RolNombre      = r.GetString(6),
            Activo         = r.GetBoolean(7),
            AuthProvider   = r.GetString(8),
            ProviderUserId = r.IsDBNull(9)  ? null              : r.GetString(9),
            FechaCreacion  = r.GetDateTime(10),
            UltimoLogin    = r.IsDBNull(11) ? (DateTime?)null   : r.GetDateTime(11),
        };

        // ─── Consultas ───────────────────────────────────────────────────────────

        public async Task<Usuario?> ObtenerPorIdAsync(int id)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE u.IdUsuario = @id";
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync()) return MapUsuario(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al obtener usuario Id={id}", ex);
            }
            return null;
        }

        public async Task<Usuario?> BuscarPorEmailAsync(string email)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase + " WHERE LOWER(u.Email) = LOWER(@email)";
                cmd.Parameters.AddWithValue("@email", email);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync()) return MapUsuario(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al buscar usuario por email", ex);
            }
            return null;
        }

        // ─── Escritura ───────────────────────────────────────────────────────────

        public async Task<int> AgregarAsync(Usuario u)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Usuarios
                        (Nombre, Apellido, Email, PasswordHash, RolId, Activo,
                         AuthProvider, ProviderUserId, FechaCreacion, UltimoLogin)
                    VALUES
                        (@nombre, @apellido, @email, @hash, @rolId, @activo,
                         @provider, @providerId, @fecha, @login)
                    RETURNING IdUsuario";
                cmd.Parameters.AddWithValue("@nombre",     u.Nombre);
                cmd.Parameters.AddWithValue("@apellido",   u.Apellido);
                cmd.Parameters.AddWithValue("@email",      u.Email.ToLower().Trim());
                cmd.Parameters.AddWithValue("@hash",       (object?)u.PasswordHash ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rolId",      u.RolId);
                cmd.Parameters.AddWithValue("@activo",     u.Activo);
                cmd.Parameters.AddWithValue("@provider",   u.AuthProvider);
                cmd.Parameters.AddWithValue("@providerId", (object?)u.ProviderUserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha",      u.FechaCreacion);
                cmd.Parameters.AddWithValue("@login",      (object?)u.UltimoLogin ?? DBNull.Value);

                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                _logger.LogInfo($"DAL: Usuario creado Id={id} email={u.Email}");
                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al agregar usuario", ex);
                return 0;
            }
        }

        public async Task ActualizarUltimoLoginAsync(int idUsuario)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Usuarios SET UltimoLogin = NOW() WHERE IdUsuario = @id";
                cmd.Parameters.AddWithValue("@id", idUsuario);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al actualizar UltimoLogin Id={idUsuario}", ex);
            }
        }
    }
}
