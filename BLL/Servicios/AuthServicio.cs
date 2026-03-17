using BE.Entidades;
using BLL.Interfaces;
using DAL.Repositorios;
using L;
using System;
using System.Threading.Tasks;

namespace BLL.Servicios
{
    public class AuthServicio : IAuthServicio
    {
        private readonly UsuarioRepositorio _usuarioRepo;
        private readonly AppLogger          _logger;

        public AuthServicio(UsuarioRepositorio usuarioRepo, AppLogger logger)
        {
            _usuarioRepo = usuarioRepo;
            _logger      = logger;
        }

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

            // BCrypt maneja el salt automáticamente; workFactor=12 es seguro y razonable
            string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

            var usuario = new Usuario
            {
                Nombre         = nombre.Trim(),
                Apellido       = apellido.Trim(),
                Email          = email.ToLower().Trim(),
                PasswordHash   = hash,
                RolId          = 2,       // Rol "Usuario" (no Admin)
                RolNombre      = "Usuario",
                Activo         = true,
                AuthProvider   = "local",
                FechaCreacion  = DateTime.UtcNow,
                UltimoLogin    = DateTime.UtcNow,
            };

            var id = await _usuarioRepo.AgregarAsync(usuario);
            if (id == 0) return AuthResultado.Error("Error al crear el usuario. Intentá de nuevo.");

            usuario.IdUsuario = id;
            _logger.LogInfo($"BLL: Usuario registrado Id={id} email={usuario.Email}");
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

            await _usuarioRepo.ActualizarUltimoLoginAsync(usuario.IdUsuario);
            _logger.LogInfo($"BLL: Login exitoso Id={usuario.IdUsuario} email={usuario.Email}");
            return AuthResultado.Ok(usuario);
        }

        // ─── Login con Google ────────────────────────────────────────────────────
        // El frontend envía el ID token de Google. Este método lo valida con la
        // API de Google y crea o recupera el usuario correspondiente.

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
                // Primera vez: crear usuario automáticamente con rol "Usuario"
                usuario = new Usuario
                {
                    Nombre         = payload.GivenName  ?? payload.Name ?? "Usuario",
                    Apellido       = payload.FamilyName ?? string.Empty,
                    Email          = email,
                    PasswordHash   = null,          // Google no usa contraseña
                    RolId          = 2,
                    RolNombre      = "Usuario",
                    Activo         = true,
                    AuthProvider   = "google",
                    ProviderUserId = payload.Subject, // ID único de Google
                    FechaCreacion  = DateTime.UtcNow,
                    UltimoLogin    = DateTime.UtcNow,
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

        // ─── Obtener por ID (para /auth/me) ─────────────────────────────────────

        public async Task<Usuario?> ObtenerPorIdAsync(int id) =>
            await _usuarioRepo.ObtenerPorIdAsync(id);
    }
}
