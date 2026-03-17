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
    }
}
