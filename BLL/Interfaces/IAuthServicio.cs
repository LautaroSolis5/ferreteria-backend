using BE.Entidades;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IAuthServicio
    {
        Task<AuthResultado> RegistrarAsync(string nombre, string apellido, string email, string password);
        Task<AuthResultado> LoginManualAsync(string email, string password);
        Task<AuthResultado> LoginGoogleAsync(string idToken);
        Task<Usuario?>      ObtenerPorIdAsync(int id);

        // ─── Verificación de email ──────────────────────────────────────────────
        Task<AuthResultado> VerificarEmailAsync(string tokenRaw);
        Task<AuthResultado> ReenviarVerificacionAsync(string email);

        // ─── Recuperación de contraseña (usuario NO logueado) ───────────────────
        /// <summary>
        /// Genera un token de recuperación y envía email.
        /// SIEMPRE retorna Exito=true para no revelar si el email existe (anti-enumeración).
        /// </summary>
        Task<AuthResultado> SolicitarRecuperacionAsync(string email);

        /// <summary>Valida el token raw y actualiza la contraseña. El token se invalida al usarse.</summary>
        Task<AuthResultado> RestablecerPasswordAsync(string tokenRaw, string nuevaPassword);

        // ─── Cambio de contraseña (usuario logueado) ────────────────────────────
        Task<AuthResultado> CambiarPasswordAsync(int usuarioId, string passwordActual, string nuevaPassword);
    }
}
