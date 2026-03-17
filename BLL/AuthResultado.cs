using BE.Entidades;

namespace BLL
{
    /// <summary>
    /// Resultado de una operación de autenticación.
    /// Transporta el Usuario autenticado o el mensaje de error.
    /// </summary>
    public class AuthResultado
    {
        public bool     Exito   { get; set; }
        public string   Mensaje { get; set; } = string.Empty;
        public Usuario? Usuario { get; set; }

        public static AuthResultado Ok(Usuario usuario) =>
            new() { Exito = true, Mensaje = "OK", Usuario = usuario };

        public static AuthResultado Error(string mensaje) =>
            new() { Exito = false, Mensaje = mensaje };
    }
}
