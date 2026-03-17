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
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var resultado = await _authServicio.RegistrarAsync(
                req.Nombre, req.Apellido, req.Email, req.Password);

            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });

            var token = _jwtHelper.GenerarToken(resultado.Usuario!);
            return Ok(BuildResponse(resultado.Usuario!, token));
        }

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var resultado = await _authServicio.LoginManualAsync(req.Email, req.Password);

            if (!resultado.Exito)
                return Unauthorized(new { mensaje = resultado.Mensaje });

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
            });
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
