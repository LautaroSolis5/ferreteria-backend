using ABST;
using BE.Entidades;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface ICategoriaServicio
    {
        Task<IEnumerable<Categoria>> ObtenerTodosAsync();
        Task<Categoria> ObtenerPorIdAsync(int id);
        Task<Resultado> AgregarAsync(Categoria categoria);
        Task<Resultado> ActualizarAsync(Categoria categoria);
        Task<Resultado> EliminarAsync(int id);
    }
}
