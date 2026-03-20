using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Contrato de envío de emails. La implementación concreta vive en FerreteriaDB
    /// y se inyecta via DI, manteniendo BLL libre de dependencias de infraestructura.
    /// </summary>
    public interface IEmailServicio
    {
        /// <summary>Envía el email de verificación de cuenta.</summary>
        Task EnviarVerificacionAsync(string destinatarioEmail, string nombre, string tokenRaw);

        /// <summary>
        /// Envía el email con el enlace de recuperación de contraseña.
        /// El EmailServicio construye la URL usando FrontendBaseUrl + tokenRaw.
        /// </summary>
        Task EnviarRecuperacionPasswordAsync(string destinatarioEmail, string nombre, string tokenRaw);

        /// <summary>Envía al usuario la confirmación de su pedido recién creado.</summary>
        Task EnviarConfirmacionPedidoAsync(BE.Entidades.Pedido pedido);

        /// <summary>
        /// Envía al usuario una actualización de su pedido
        /// (cambio de estado o de estado de pago).
        /// </summary>
        Task EnviarActualizacionPedidoAsync(BE.Entidades.Pedido pedido, string descripcionCambio);
    }
}
