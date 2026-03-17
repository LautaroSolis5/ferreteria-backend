using ABST;
using BE.Entidades;
using BLL.Interfaces;
using DAL.Repositorios;
using L;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Servicios
{
    public class ProductoServicio : IProductoServicio
    {
        private readonly ProductoRepositorio _repo;
        private readonly AppLogger _logger;

        public ProductoServicio(ProductoRepositorio repo, AppLogger logger)
        {
            _repo   = repo;
            _logger = logger;
        }

        public async Task<IEnumerable<Producto>> ObtenerTodosAsync()
        {
            _logger.LogInfo("BLL: ObtenerTodos productos");
            return await _repo.ObtenerTodosAsync();
        }

        public async Task<Producto> ObtenerPorIdAsync(int id)
        {
            _logger.LogInfo($"BLL: ObtenerPorId producto Id={id}");
            return await _repo.ObtenerPorIdAsync(id);
        }

        public async Task<IEnumerable<Producto>> ObtenerPorCategoriaAsync(int categoriaId)
        {
            _logger.LogInfo($"BLL: ObtenerPorCategoria Id={categoriaId}");
            return await _repo.ObtenerPorCategoriaAsync(categoriaId);
        }

        public async Task<Resultado> AgregarAsync(Producto producto)
        {
            var validacion = producto.Validar();
            if (!validacion.Exito)
            {
                _logger.LogWarning($"BLL: Validación fallida al agregar producto: {validacion.Mensaje}");
                return validacion;
            }

            var id = await _repo.AgregarAsync(producto);
            if (id > 0)
            {
                _logger.LogInfo($"BLL: Producto agregado correctamente Id={id}");
                return Resultado.Ok("Producto agregado correctamente.", id);
            }

            _logger.LogError("BLL: No se pudo agregar el producto (DAL devolvió 0)");
            return Resultado.Error("No se pudo agregar el producto. Intentá de nuevo.");
        }

        public async Task<Resultado> ActualizarAsync(Producto producto)
        {
            var validacion = producto.Validar();
            if (!validacion.Exito)
            {
                _logger.LogWarning($"BLL: Validación fallida al actualizar producto Id={producto.Id}: {validacion.Mensaje}");
                return validacion;
            }

            var ok = await _repo.ActualizarAsync(producto);
            if (ok)
            {
                _logger.LogInfo($"BLL: Producto Id={producto.Id} actualizado correctamente");
                return Resultado.Ok("Producto actualizado correctamente.");
            }

            _logger.LogError($"BLL: No se pudo actualizar el producto Id={producto.Id}");
            return Resultado.Error("No se pudo actualizar el producto. Verificá que exista.");
        }

        public async Task<Resultado> EliminarAsync(int id)
        {
            var ok = await _repo.EliminarAsync(id);
            if (ok)
            {
                _logger.LogInfo($"BLL: Producto Id={id} eliminado correctamente");
                return Resultado.Ok("Producto eliminado correctamente.");
            }

            _logger.LogError($"BLL: No se pudo eliminar el producto Id={id}");
            return Resultado.Error("No se pudo eliminar el producto. Verificá que exista.");
        }
    }
}
