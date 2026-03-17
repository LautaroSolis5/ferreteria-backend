using BE.Entidades;
using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FerreteriaDB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaServicio _servicio;

        public CategoriasController(ICategoriaServicio servicio)
        {
            _servicio = servicio;
        }

        // GET api/categorias
        [HttpGet]
        public async Task<IActionResult> GetTodos()
        {
            var categorias = await _servicio.ObtenerTodosAsync();
            return Ok(categorias);
        }

        // GET api/categorias/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPorId(int id)
        {
            var categoria = await _servicio.ObtenerPorIdAsync(id);
            if (categoria is null)
                return NotFound(new { mensaje = $"Categoría con Id={id} no encontrada." });
            return Ok(categoria);
        }

        // POST api/categorias  →  solo Admin
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Agregar([FromBody] Categoria categoria)
        {
            var resultado = await _servicio.AgregarAsync(categoria);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });
            return CreatedAtAction(nameof(GetPorId), new { id = resultado.Id }, resultado);
        }

        // PUT api/categorias/5  →  solo Admin
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Actualizar(int id, [FromBody] Categoria categoria)
        {
            if (id != categoria.Id)
                return BadRequest(new { mensaje = "El Id de la URL no coincide con el del cuerpo." });
            var resultado = await _servicio.ActualizarAsync(categoria);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });
            return Ok(resultado);
        }

        // DELETE api/categorias/5  →  solo Admin
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var resultado = await _servicio.EliminarAsync(id);
            if (!resultado.Exito)
                return NotFound(new { mensaje = resultado.Mensaje });
            return Ok(resultado);
        }
    }
}
