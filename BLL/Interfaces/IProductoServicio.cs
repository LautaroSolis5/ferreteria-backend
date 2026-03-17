using ABST;
using BE.Entidades;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IProductoServicio
    {
        Task<IEnumerable<Producto>> ObtenerTodosAsync();
        Task<Producto> ObtenerPorIdAsync(int id);
        Task<IEnumerable<Producto>> ObtenerPorCategoriaAsync(int categoriaId);
        Task<Resultado> AgregarAsync(Producto producto);
        Task<Resultado> ActualizarAsync(Producto producto);
        Task<Resultado> EliminarAsync(int id);
    }
}
