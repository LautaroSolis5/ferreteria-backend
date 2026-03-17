using System.Collections.Generic;
using System.Threading.Tasks;

namespace ABST.Interfaces
{
    /// <summary>
    /// Contrato genérico CRUD. ABST usa genéricos para no depender de BE.
    /// DAL implementa este contrato con los tipos concretos de BE.
    /// </summary>
    public interface IRepositorioBase<T> where T : class
    {
        Task<IEnumerable<T>> ObtenerTodosAsync();
        Task<T> ObtenerPorIdAsync(int id);
        Task<int> AgregarAsync(T entidad);
        Task<bool> ActualizarAsync(T entidad);
        Task<bool> EliminarAsync(int id);
    }
}
