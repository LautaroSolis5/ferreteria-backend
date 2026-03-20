using BLL.Interfaces;
using FerreteriaDB.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FerreteriaDB.Controllers
{
    // ─── DTOs de request ─────────────────────────────────────────────────────────
    public record RegisterRequest(string Nombre, string Apellido, string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record GoogleLoginRequest(string IdToken);
    public record ResendVerificationRequest(string Email);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Token, string NuevaPassword);
    public record ChangePasswordRequest(string PasswordActual, string NuevaPassword, string ConfirmarPassword);

    // ─── Controller ──────────────────────────────────────────────────────────────

    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthServicio _authServicio;
        private readonly JwtHelper     _jwtHelper;

        public AuthController(IAuthServicio authServicio, JwtHelper jwtHelper)
        {
            _authServicio = authServicio;
            _jwtHelper    = jwtHelper;
        }

        // POST /api/auth/register
        // No devuelve JWT: el usuario debe verificar su email primero.
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var resultado = await _authServicio.RegistrarAsync(
                req.Nombre, req.Apellido, req.Email, req.Password);

            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });

            return Ok(new
            {
                exito         = true,
                emailPendiente = true,
                email         = resultado.Usuario!.Email,
                mensaje       = "Registro exitoso. Te enviamos un email para verificar tu cuenta. Revisá tu bandeja de entrada (y spam)."
            });
        }

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var resultado = await _authServicio.LoginManualAsync(req.Email, req.Password);

            if (!resultado.Exito)
            {
                // Caso especial: credenciales correctas pero email sin verificar
                if (resultado.RequiereVerificacion)
                    return StatusCode(403, new
                    {
                        mensaje        = resultado.Mensaje,
                        emailVerificado = false,
                        email          = resultado.Usuario!.Email
                    });

                return Unauthorized(new { mensaje = resultado.Mensaje });
            }

            var token = _jwtHelper.GenerarToken(resultado.Usuario!);
            return Ok(BuildResponse(resultado.Usuario!, token));
        }

        // POST /api/auth/google-login
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
        {
            var resultado = await _authServicio.LoginGoogleAsync(req.IdToken);

            if (!resultado.Exito)
                return Unauthorized(new { mensaje = resultado.Mensaje });

            var token = _jwtHelper.GenerarToken(resultado.Usuario!);
            return Ok(BuildResponse(resultado.Usuario!, token));
        }

        // GET /api/auth/verify-email?token=xxx
        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { mensaje = "Token no proporcionado." });

            var resultado = await _authServicio.VerificarEmailAsync(token);

            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });

            return Ok(new
            {
                exito   = true,
                mensaje = "¡Email verificado correctamente! Ya podés iniciar sesión.",
                email   = resultado.Usuario!.Email
            });
        }

        // POST /api/auth/resend-verification
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest req)
        {
            var resultado = await _authServicio.ReenviarVerificacionAsync(req.Email);

            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });

            return Ok(new
            {
                exito   = true,
                mensaje = "Te reenviamos el email de verificación. Revisá tu bandeja de entrada."
            });
        }

        // GET /api/auth/me  →  requiere token válido
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value;

            if (!int.TryParse(subClaim, out int userId))
                return Unauthorized(new { mensaje = "Token inválido." });

            var usuario = await _authServicio.ObtenerPorIdAsync(userId);
            if (usuario == null) return NotFound(new { mensaje = "Usuario no encontrado." });
            if (!usuario.Activo) return Unauthorized(new { mensaje = "Cuenta desactivada." });

            return Ok(new
            {
                id             = usuario.IdUsuario,
                nombre         = usuario.Nombre,
                apellido       = usuario.Apellido,
                nombreCompleto = $"{usuario.Nombre} {usuario.Apellido}".Trim(),
                email          = usuario.Email,
                rol            = usuario.RolNombre,
                authProvider   = usuario.AuthProvider,
                emailVerificado = usuario.EmailVerificado,
            });
        }

        // POST /api/auth/forgot-password
        // SIEMPRE devuelve 200 con el mismo mensaje → no revela si el email existe
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            await _authServicio.SolicitarRecuperacionAsync(req.Email ?? string.Empty);
            return Ok(new
            {
                exito   = true,
                mensaje = "Si ese email está registrado, en breve recibirás un enlace para restablecer tu contraseña. Revisá tu bandeja de entrada (y spam)."
            });
        }

        // POST /api/auth/reset-password
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            var resultado = await _authServicio.RestablecerPasswordAsync(req.Token, req.NuevaPassword);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });

            return Ok(new { exito = true, mensaje = resultado.Mensaje });
        }

        // POST /api/auth/change-password  →  requiere JWT válido
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            if (req.NuevaPassword != req.ConfirmarPassword)
                return BadRequest(new { mensaje = "La nueva contraseña y la confirmación no coinciden." });

            var subClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value;
            if (!int.TryParse(subClaim, out int userId))
                return Unauthorized(new { mensaje = "Token inválido." });

            var resultado = await _authServicio.CambiarPasswordAsync(userId, req.PasswordActual, req.NuevaPassword);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });

            return Ok(new { exito = true, mensaje = resultado.Mensaje });
        }

        // ─── Helper privado ───────────────────────────────────────────────────────

        private static object BuildResponse(BE.Entidades.Usuario u, string token) => new
        {
            token,
            id             = u.IdUsuario,
            email          = u.Email,
            nombre         = u.Nombre,
            apellido       = u.Apellido,
            nombreCompleto = $"{u.Nombre} {u.Apellido}".Trim(),
            rol            = u.RolNombre,
        };
    }
}
