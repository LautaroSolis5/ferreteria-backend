using BE.Entidades;
using BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FerreteriaDB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly IProductoServicio _servicio;

        public ProductosController(IProductoServicio servicio)
        {
            _servicio = servicio;
        }

        // GET api/productos
        [HttpGet]
        public async Task<IActionResult> GetTodos()
        {
            var productos = await _servicio.ObtenerTodosAsync();
            return Ok(productos);
        }

        // GET api/productos/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPorId(int id)
        {
            var producto = await _servicio.ObtenerPorIdAsync(id);
            if (producto is null)
                return NotFound(new { mensaje = $"Producto con Id={id} no encontrado." });
            return Ok(producto);
        }

        // GET api/productos/categoria/3
        [HttpGet("categoria/{categoriaId:int}")]
        public async Task<IActionResult> GetPorCategoria(int categoriaId)
        {
            var productos = await _servicio.ObtenerPorCategoriaAsync(categoriaId);
            return Ok(productos);
        }

        // POST api/productos
        [HttpPost]
        public async Task<IActionResult> Agregar([FromBody] Producto producto)
        {
            var resultado = await _servicio.AgregarAsync(producto);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });
            return CreatedAtAction(nameof(GetPorId), new { id = resultado.Id }, resultado);
        }

        // PUT api/productos/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Actualizar(int id, [FromBody] Producto producto)
        {
            if (id != producto.Id)
                return BadRequest(new { mensaje = "El Id de la URL no coincide con el del cuerpo." });
            var resultado = await _servicio.ActualizarAsync(producto);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });
            return Ok(resultado);
        }

        // DELETE api/productos/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var resultado = await _servicio.EliminarAsync(id);
            if (!resultado.Exito)
                return NotFound(new { mensaje = resultado.Mensaje });
            return Ok(resultado);
        }
    }
}
