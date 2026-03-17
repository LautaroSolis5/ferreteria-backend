using BE.Entidades;
using BLL.Interfaces;
using DAL.Repositorios;
using L;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Servicios
{
    public class AuthServicio : IAuthServicio
    {
        private readonly UsuarioRepositorio _usuarioRepo;
        private readonly AppLogger          _logger;
        private readonly IEmailServicio     _emailServicio;

        public AuthServicio(
            UsuarioRepositorio usuarioRepo,
            AppLogger          logger,
            IEmailServicio     emailServicio)
        {
            _usuarioRepo   = usuarioRepo;
            _logger        = logger;
            _emailServicio = emailServicio;
        }

        // ─── Helpers de token ────────────────────────────────────────────────────

        /// <summary>Genera 32 bytes aleatorios como string hexadecimal (64 chars).</summary>
        private static string GenerarTokenRaw()
            => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();

        /// <summary>
        /// Hashea el token raw con SHA256.
        /// Solo el hash se guarda en DB; el raw va en el enlace del email.
        /// </summary>
        private static string HashToken(string rawToken)
            => Convert.ToHexString(
                   SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))
               ).ToLower();

        // ─── Registro manual ─────────────────────────────────────────────────────

        public async Task<AuthResultado> RegistrarAsync(
            string nombre, string apellido, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return AuthResultado.Error("Email y contraseña son requeridos.");

            if (password.Length < 6)
                return AuthResultado.Error("La contraseña debe tener al menos 6 caracteres.");

            var existente = await _usuarioRepo.BuscarPorEmailAsync(email);
            if (existente != null)
                return AuthResultado.Error("El email ya está registrado.");

            string hash     = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
            string tokenRaw = GenerarTokenRaw();
            string tokenHash = HashToken(tokenRaw);

            var usuario = new Usuario
            {
                Nombre            = nombre.Trim(),
                Apellido          = apellido.Trim(),
                Email             = email.ToLower().Trim(),
                PasswordHash      = hash,
                RolId             = 2,
                RolNombre         = "Usuario",
                Activo            = true,
                AuthProvider      = "local",
                FechaCreacion     = DateTime.UtcNow,
                UltimoLogin       = null,
                EmailVerificado   = false,
                TokenVerificacion = tokenHash,
                TokenExpiracion   = DateTime.UtcNow.AddHours(24),
            };

            var id = await _usuarioRepo.AgregarAsync(usuario);
            if (id == 0) return AuthResultado.Error("Error al crear el usuario. Intentá de nuevo.");

            usuario.IdUsuario = id;
            _logger.LogInfo($"BLL: Usuario registrado Id={id} email={usuario.Email}");

            // Enviar email de verificación (si falla, el registro igual se completó)
            try
            {
                await _emailServicio.EnviarVerificacionAsync(usuario.Email, usuario.Nombre, tokenRaw);
            }
            catch (Exception ex)
            {
                _logger.LogError($"BLL: No se pudo enviar email de verificación a {usuario.Email}", ex);
                // No retornamos error: el usuario ya está creado, puede pedir reenvío
            }

            return AuthResultado.Ok(usuario);
        }

        // ─── Login manual ────────────────────────────────────────────────────────

        public async Task<AuthResultado> LoginManualAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return AuthResultado.Error("Email y contraseña son requeridos.");

            var usuario = await _usuarioRepo.BuscarPorEmailAsync(email);

            if (usuario == null)
                return AuthResultado.Error("Credenciales incorrectas.");

            if (!usuario.Activo)
                return AuthResultado.Error("La cuenta está desactivada.");

            if (string.IsNullOrEmpty(usuario.PasswordHash))
                return AuthResultado.Error("Esta cuenta usa inicio de sesión con Google.");

            if (!BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
                return AuthResultado.Error("Credenciales incorrectas.");

            // Verificar que el email fue confirmado
            if (!usuario.EmailVerificado)
                return AuthResultado.EmailSinVerificar(usuario);

            await _usuarioRepo.ActualizarUltimoLoginAsync(usuario.IdUsuario);
            _logger.LogInfo($"BLL: Login exitoso Id={usuario.IdUsuario} email={usuario.Email}");
            return AuthResultado.Ok(usuario);
        }

        // ─── Login con Google ────────────────────────────────────────────────────

        public async Task<AuthResultado> LoginGoogleAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                return AuthResultado.Error("Token de Google no proporcionado.");

            Google.Apis.Auth.GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("BLL: Token de Google inválido", ex);
                return AuthResultado.Error("Token de Google inválido o expirado.");
            }

            var email = payload.Email?.ToLower().Trim();
            if (string.IsNullOrEmpty(email))
                return AuthResultado.Error("No se pudo obtener el email de Google.");

            var usuario = await _usuarioRepo.BuscarPorEmailAsync(email);

            if (usuario == null)
            {
                // Primera vez: el email de Google ya está verificado por Google
                usuario = new Usuario
                {
                    Nombre            = payload.GivenName  ?? payload.Name ?? "Usuario",
                    Apellido          = payload.FamilyName ?? string.Empty,
                    Email             = email,
                    PasswordHash      = null,
                    RolId             = 2,
                    RolNombre         = "Usuario",
                    Activo            = true,
                    AuthProvider      = "google",
                    ProviderUserId    = payload.Subject,
                    FechaCreacion     = DateTime.UtcNow,
                    UltimoLogin       = DateTime.UtcNow,
                    EmailVerificado   = true,   // Google ya verificó el email
                    TokenVerificacion = null,
                    TokenExpiracion   = null,
                };

                var id = await _usuarioRepo.AgregarAsync(usuario);
                if (id == 0) return AuthResultado.Error("Error al crear el usuario con Google.");

                usuario.IdUsuario = id;
                _logger.LogInfo($"BLL: Usuario Google creado Id={id} email={email}");
            }
            else
            {
                if (!usuario.Activo) return AuthResultado.Error("La cuenta está desactivada.");
                await _usuarioRepo.ActualizarUltimoLoginAsync(usuario.IdUsuario);
                _logger.LogInfo($"BLL: Login Google exitoso Id={usuario.IdUsuario} email={email}");
            }

            return AuthResultado.Ok(usuario);
        }

        // ─── Verificar email ─────────────────────────────────────────────────────

        public async Task<AuthResultado> VerificarEmailAsync(string tokenRaw)
        {
            if (string.IsNullOrWhiteSpace(tokenRaw))
                return AuthResultado.Error("Token de verificación inválido.");

            string tokenHash = HashToken(tokenRaw);

            var usuario = await _usuarioRepo.BuscarPorTokenHashAsync(tokenHash);
            if (usuario == null)
                return AuthResultado.Error("El enlace de verificación es inválido o ya fue utilizado.");

            if (usuario.TokenExpiracion.HasValue && DateTime.UtcNow > usuario.TokenExpiracion.Value)
                return AuthResultado.Error("El enlace de verificación expiró. Solicitá uno nuevo desde la página de login.");

            await _usuarioRepo.MarcarEmailVerificadoAsync(usuario.IdUsuario);
            _logger.LogInfo($"BLL: Email verificado para usuario Id={usuario.IdUsuario} email={usuario.Email}");

            return AuthResultado.Ok(usuario);
        }

        // ─── Reenviar verificación ───────────────────────────────────────────────

        public async Task<AuthResultado> ReenviarVerificacionAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return AuthResultado.Error("El email es requerido.");

            var usuario = await _usuarioRepo.BuscarPorEmailAsync(email);
            if (usuario == null)
                return AuthResultado.Error("No existe una cuenta con ese email.");

            if (usuario.EmailVerificado)
                return AuthResultado.Error("El email ya está verificado. Podés iniciar sesión.");

            string tokenRaw  = GenerarTokenRaw();
            string tokenHash = HashToken(tokenRaw);
            DateTime expiry  = DateTime.UtcNow.AddHours(24);

            await _usuarioRepo.ActualizarTokenVerificacionAsync(usuario.IdUsuario, tokenHash, expiry);

            try
            {
                await _emailServicio.EnviarVerificacionAsync(usuario.Email, usuario.Nombre, tokenRaw);
                _logger.LogInfo($"BLL: Email de verificación reenviado a {usuario.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"BLL: Error al reenviar email de verificación a {usuario.Email}", ex);
                return AuthResultado.Error("No se pudo enviar el email. Intentá de nuevo más tarde.");
            }

            return AuthResultado.Ok(usuario);
        }

        // ─── Obtener por ID ──────────────────────────────────────────────────────

        public async Task<Usuario?> ObtenerPorIdAsync(int id) =>
            await _usuarioRepo.ObtenerPorIdAsync(id);
    }
}
