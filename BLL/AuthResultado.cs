using BE.Entidades;

namespace BLL
{
    /// <summary>
    /// Resultado de una operación de autenticación.
    /// Transporta el Usuario autenticado o el mensaje de error.
    /// </summary>
    public class AuthResultado
    {
        public bool     Exito                { get; set; }
        public string   Mensaje              { get; set; } = string.Empty;
        public Usuario? Usuario              { get; set; }

        /// <summary>
        /// true cuando el login falló exclusivamente porque el email no fue verificado.
        /// El controller lo usa para devolver 403 con datos extra al frontend.
        /// </summary>
        public bool     RequiereVerificacion { get; set; }

        public static AuthResultado Ok(Usuario usuario) =>
            new() { Exito = true, Mensaje = "OK", Usuario = usuario };

        public static AuthResultado Error(string mensaje) =>
            new() { Exito = false, Mensaje = mensaje };

        /// <summary>
        /// Login correcto pero email pendiente de verificación.
        /// </summary>
        public static AuthResultado EmailSinVerificar(Usuario usuario) =>
            new()
            {
                Exito                = false,
                Mensaje              = "Por favor verificá tu email antes de iniciar sesión.",
                Usuario              = usuario,
                RequiereVerificacion = true
            };
    }
}
