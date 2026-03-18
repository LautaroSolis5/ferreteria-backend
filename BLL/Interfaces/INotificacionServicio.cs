using ABST;
using BE.Entidades;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface INotificacionServicio
    {
        Task<IEnumerable<Notificacion>> ObtenerTodasAsync();
        Task<Resultado>                 MarcarComoLeidaAsync(int id);
        Task<int>                       ContarNoLeidasAsync();
    }
}
