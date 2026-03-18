using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FerreteriaDB.Controllers
{
    [ApiController]
    [Route("api/notificaciones")]
    [Authorize(Roles = "Admin")]
    public class NotificacionesController : ControllerBase
    {
        private readonly INotificacionServicio _servicio;

        public NotificacionesController(INotificacionServicio servicio)
        {
            _servicio = servicio;
        }

        // GET /api/notificaciones
        [HttpGet]
        public async Task<IActionResult> ObtenerTodas()
        {
            var lista = await _servicio.ObtenerTodasAsync();
            return Ok(lista);
        }

        // PUT /api/notificaciones/{id}/leida
        [HttpPut("{id:int}/leida")]
        public async Task<IActionResult> MarcarComoLeida(int id)
        {
            var resultado = await _servicio.MarcarComoLeidaAsync(id);
            if (!resultado.Exito)
                return NotFound(new { mensaje = resultado.Mensaje });
            return Ok(new { mensaje = resultado.Mensaje });
        }

        // GET /api/notificaciones/no-leidas/count
        [HttpGet("no-leidas/count")]
        public async Task<IActionResult> ContarNoLeidas()
        {
            var count = await _servicio.ContarNoLeidasAsync();
            return Ok(new { count });
        }
    }
}
