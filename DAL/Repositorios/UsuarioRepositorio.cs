using BE.Entidades;
using L;
using Npgsql;
using System;
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

        // Columnas: 0-11 base + 12-14 verificación de email + 15-16 recuperación de contraseña
        private const string SelectBase = @"
            SELECT u.IdUsuario, u.Nombre, u.Apellido, u.Email, u.PasswordHash,
                   u.RolId, r.NombreRol, u.Activo, u.AuthProvider,
                   u.ProviderUserId, u.FechaCreacion, u.UltimoLogin,
                   u.EmailVerificado, u.TokenVerificacion, u.TokenExpiracion,
                   u.TokenRecuperacion, u.TokenRecuperacionExpiracion
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
            IdUsuario         = r.GetInt32(0),
            Nombre            = r.GetString(1),
            Apellido          = r.GetString(2),
            Email             = r.GetString(3),
            PasswordHash      = r.IsDBNull(4)  ? null            : r.GetString(4),
            RolId             = r.GetInt32(5),
            RolNombre         = r.GetString(6),
            Activo            = r.GetBoolean(7),
            AuthProvider      = r.GetString(8),
            ProviderUserId    = r.IsDBNull(9)  ? null            : r.GetString(9),
            FechaCreacion     = r.GetDateTime(10),
            UltimoLogin       = r.IsDBNull(11) ? (DateTime?)null : r.GetDateTime(11),
            EmailVerificado              = r.GetBoolean(12),
            TokenVerificacion            = r.IsDBNull(13) ? null            : r.GetString(13),
            TokenExpiracion              = r.IsDBNull(14) ? (DateTime?)null : r.GetDateTime(14),
            TokenRecuperacion            = r.IsDBNull(15) ? null            : r.GetString(15),
            TokenRecuperacionExpiracion  = r.IsDBNull(16) ? (DateTime?)null : r.GetDateTime(16),
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

        /// <summary>
        /// Busca usuario por el HASH del token de verificación.
        /// Solo devuelve usuarios con email aún no verificado.
        /// </summary>
        public async Task<Usuario?> BuscarPorTokenHashAsync(string tokenHash)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase +
                    " WHERE u.TokenVerificacion = @hash AND u.EmailVerificado = FALSE";
                cmd.Parameters.AddWithValue("@hash", tokenHash);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync()) return MapUsuario(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al buscar usuario por token de verificación", ex);
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
                         AuthProvider, ProviderUserId, FechaCreacion, UltimoLogin,
                         EmailVerificado, TokenVerificacion, TokenExpiracion)
                    VALUES
                        (@nombre, @apellido, @email, @hash, @rolId, @activo,
                         @provider, @providerId, @fecha, @login,
                         @emailVerificado, @tokenHash, @tokenExp)
                    RETURNING IdUsuario";
                cmd.Parameters.AddWithValue("@nombre",          u.Nombre);
                cmd.Parameters.AddWithValue("@apellido",        u.Apellido);
                cmd.Parameters.AddWithValue("@email",           u.Email.ToLower().Trim());
                cmd.Parameters.AddWithValue("@hash",            (object?)u.PasswordHash      ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rolId",           u.RolId);
                cmd.Parameters.AddWithValue("@activo",          u.Activo);
                cmd.Parameters.AddWithValue("@provider",        u.AuthProvider);
                cmd.Parameters.AddWithValue("@providerId",      (object?)u.ProviderUserId    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fecha",           u.FechaCreacion);
                cmd.Parameters.AddWithValue("@login",           (object?)u.UltimoLogin       ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@emailVerificado", u.EmailVerificado);
                cmd.Parameters.AddWithValue("@tokenHash",       (object?)u.TokenVerificacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tokenExp",        (object?)u.TokenExpiracion   ?? DBNull.Value);

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

        /// <summary>
        /// Marca el email del usuario como verificado y limpia los campos de token.
        /// </summary>
        public async Task MarcarEmailVerificadoAsync(int idUsuario)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Usuarios
                    SET EmailVerificado   = TRUE,
                        TokenVerificacion = NULL,
                        TokenExpiracion   = NULL
                    WHERE IdUsuario = @id";
                cmd.Parameters.AddWithValue("@id", idUsuario);
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Email verificado para IdUsuario={idUsuario}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al marcar email verificado Id={idUsuario}", ex);
            }
        }

        /// <summary>
        /// Actualiza el token de verificación (para reenvíos).
        /// </summary>
        public async Task ActualizarTokenVerificacionAsync(int idUsuario, string tokenHash, DateTime expiracion)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Usuarios
                    SET TokenVerificacion = @hash,
                        TokenExpiracion   = @exp
                    WHERE IdUsuario = @id";
                cmd.Parameters.AddWithValue("@hash", tokenHash);
                cmd.Parameters.AddWithValue("@exp",  expiracion);
                cmd.Parameters.AddWithValue("@id",   idUsuario);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al actualizar token de verificación Id={idUsuario}", ex);
            }
        }

        // ─── Recuperación de contraseña ──────────────────────────────────────────

        /// <summary>Busca usuario por el HASH del token de recuperación (token aún no usado).</summary>
        public async Task<Usuario?> BuscarPorTokenRecuperacionAsync(string tokenHash)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = SelectBase +
                    " WHERE u.TokenRecuperacion = @hash AND u.TokenRecuperacion IS NOT NULL";
                cmd.Parameters.AddWithValue("@hash", tokenHash);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync()) return MapUsuario(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError("DAL: Error al buscar usuario por token de recuperación", ex);
            }
            return null;
        }

        /// <summary>Guarda el hash del token de recuperación y su fecha de expiración.</summary>
        public async Task GuardarTokenRecuperacionAsync(int idUsuario, string tokenHash, DateTime expiracion)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Usuarios
                    SET TokenRecuperacion            = @hash,
                        TokenRecuperacionExpiracion  = @exp
                    WHERE IdUsuario = @id";
                cmd.Parameters.AddWithValue("@hash", tokenHash);
                cmd.Parameters.AddWithValue("@exp",  expiracion);
                cmd.Parameters.AddWithValue("@id",   idUsuario);
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: Token recuperación guardado para IdUsuario={idUsuario}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al guardar token de recuperación Id={idUsuario}", ex);
            }
        }

        /// <summary>Invalida el token de recuperación sin cambiar la contraseña (token expirado).</summary>
        public async Task LimpiarTokenRecuperacionAsync(int idUsuario)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Usuarios
                    SET TokenRecuperacion            = NULL,
                        TokenRecuperacionExpiracion  = NULL
                    WHERE IdUsuario = @id";
                cmd.Parameters.AddWithValue("@id", idUsuario);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al limpiar token de recuperación Id={idUsuario}", ex);
            }
        }

        /// <summary>
        /// Actualiza el PasswordHash e invalida el token de recuperación en una sola operación.
        /// </summary>
        public async Task<bool> ActualizarPasswordHashAsync(int idUsuario, string nuevoHash)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Usuarios
                    SET PasswordHash                 = @hash,
                        TokenRecuperacion            = NULL,
                        TokenRecuperacionExpiracion  = NULL
                    WHERE IdUsuario = @id";
                cmd.Parameters.AddWithValue("@hash", nuevoHash);
                cmd.Parameters.AddWithValue("@id",   idUsuario);
                var filas = await cmd.ExecuteNonQueryAsync();
                _logger.LogInfo($"DAL: PasswordHash actualizado para IdUsuario={idUsuario}");
                return filas > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DAL: Error al actualizar PasswordHash Id={idUsuario}", ex);
                return false;
            }
        }
    }
}
