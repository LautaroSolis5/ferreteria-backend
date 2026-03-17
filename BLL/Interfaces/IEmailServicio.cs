using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Contrato de envío de emails. La implementación concreta vive en FerreteriaDB
    /// y se inyecta via DI, manteniendo BLL libre de dependencias de infraestructura.
    /// </summary>
    public interface IEmailServicio
    {
        /// <summary>
        /// Envía el email de verificación de cuenta.
        /// </summary>
        /// <param name="destinatarioEmail">Email del usuario recién registrado.</param>
        /// <param name="nombre">Nombre del usuario para el saludo.</param>
        /// <param name="tokenRaw">Token en texto plano (va en el enlace del email).</param>
        Task EnviarVerificacionAsync(string destinatarioEmail, string nombre, string tokenRaw);
    }
}
