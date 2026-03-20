using BE.Entidades;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IOfertaServicio
    {
        Task<Oferta>              ObtenerActivaAsync();
        Task<IEnumerable<Oferta>> ObtenerTodosAsync();
        Task<Oferta>              ObtenerPorIdAsync(int id);
        Task<Oferta>              CrearAsync(Oferta oferta);
        Task<Oferta>              ActualizarAsync(Oferta oferta);
        Task                      EliminarAsync(int id);
        Task<Oferta>              ToggleActivaAsync(int id);
    }
}
