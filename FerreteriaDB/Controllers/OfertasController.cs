using BE.Entidades;
using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace FerreteriaDB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OfertasController : ControllerBase
    {
        private readonly IOfertaServicio _servicio;
        public OfertasController(IOfertaServicio servicio) => _servicio = servicio;

        // GET api/ofertas/activa  — público, para la home
        [HttpGet("activa")]
        public async Task<IActionResult> GetActiva()
        {
            var oferta = await _servicio.ObtenerActivaAsync();
            return Ok(oferta); // null si no hay oferta activa
        }

        // GET api/ofertas  — solo Admin
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTodos()
            => Ok(await _servicio.ObtenerTodosAsync());

        // GET api/ofertas/{id}  — solo Admin
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPorId(int id)
        {
            var oferta = await _servicio.ObtenerPorIdAsync(id);
            if (oferta is null) return NotFound(new { mensaje = "Oferta no encontrada." });
            return Ok(oferta);
        }

        // POST api/ofertas  — solo Admin
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Crear([FromBody] Oferta oferta)
        {
            try
            {
                var creada = await _servicio.CrearAsync(oferta);
                return CreatedAtAction(nameof(GetPorId), new { id = creada.IdOferta }, creada);
            }
            catch (Exception ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        // PUT api/ofertas/{id}  — solo Admin
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Actualizar(int id, [FromBody] Oferta oferta)
        {
            oferta.IdOferta = id;
            try { return Ok(await _servicio.ActualizarAsync(oferta)); }
            catch (Exception ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        // DELETE api/ofertas/{id}  — solo Admin
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Eliminar(int id)
        {
            await _servicio.EliminarAsync(id);
            return NoContent();
        }

        // PATCH api/ofertas/{id}/toggle  — solo Admin
        [HttpPatch("{id:int}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Toggle(int id)
        {
            try { return Ok(await _servicio.ToggleActivaAsync(id)); }
            catch (Exception ex) { return BadRequest(new { mensaje = ex.Message }); }
        }
    }
}
