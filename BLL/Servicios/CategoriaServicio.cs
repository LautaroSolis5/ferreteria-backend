using ABST;
using BE.Entidades;
using BLL.Interfaces;
using DAL.Repositorios;
using L;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Servicios
{
    public class CategoriaServicio : ICategoriaServicio
    {
        private readonly CategoriaRepositorio _repo;
        private readonly AppLogger _logger;

        public CategoriaServicio(CategoriaRepositorio repo, AppLogger logger)
        {
            _repo   = repo;
            _logger = logger;
        }

        public async Task<IEnumerable<Categoria>> ObtenerTodosAsync()
        {
            _logger.LogInfo("BLL: ObtenerTodos categorías");
            return await _repo.ObtenerTodosAsync();
        }

        public async Task<Categoria> ObtenerPorIdAsync(int id)
        {
            _logger.LogInfo($"BLL: ObtenerPorId categoría Id={id}");
            return await _repo.ObtenerPorIdAsync(id);
        }

        public async Task<Resultado> AgregarAsync(Categoria categoria)
        {
            var validacion = categoria.Validar();
            if (!validacion.Exito)
            {
                _logger.LogWarning($"BLL: Validación fallida al agregar categoría: {validacion.Mensaje}");
                return validacion;
            }

            var id = await _repo.AgregarAsync(categoria);
            if (id > 0)
            {
                _logger.LogInfo($"BLL: Categoría agregada correctamente Id={id}");
                return Resultado.Ok("Categoría agregada correctamente.", id);
            }

            _logger.LogError("BLL: No se pudo agregar la categoría (DAL devolvió 0)");
            return Resultado.Error("No se pudo agregar la categoría. Intentá de nuevo.");
        }

        public async Task<Resultado> ActualizarAsync(Categoria categoria)
        {
            var validacion = categoria.Validar();
            if (!validacion.Exito)
            {
                _logger.LogWarning($"BLL: Validación fallida al actualizar categoría Id={categoria.Id}: {validacion.Mensaje}");
                return validacion;
            }

            var ok = await _repo.ActualizarAsync(categoria);
            if (ok)
            {
                _logger.LogInfo($"BLL: Categoría Id={categoria.Id} actualizada correctamente");
                return Resultado.Ok("Categoría actualizada correctamente.");
            }

            _logger.LogError($"BLL: No se pudo actualizar la categoría Id={categoria.Id}");
            return Resultado.Error("No se pudo actualizar la categoría. Verificá que exista.");
        }

        public async Task<Resultado> EliminarAsync(int id)
        {
            var ok = await _repo.EliminarAsync(id);
            if (ok)
            {
                _logger.LogInfo($"BLL: Categoría Id={id} eliminada correctamente");
                return Resultado.Ok("Categoría eliminada correctamente.");
            }

            _logger.LogError($"BLL: No se pudo eliminar la categoría Id={id}");
            return Resultado.Error("No se pudo eliminar la categoría. Verificá que exista.");
        }
    }
}
