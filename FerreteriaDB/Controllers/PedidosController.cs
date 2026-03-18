using BE.Entidades;
using BLL.Interfaces;
using BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace FerreteriaDB.Controllers
{
    [ApiController]
    [Route("api/pedidos")]
    public class PedidosController : ControllerBase
    {
        private readonly IPedidoServicio _servicio;

        public PedidosController(IPedidoServicio servicio)
        {
            _servicio = servicio;
        }

        // ─── GET /api/pedidos/horarios?fecha=yyyy-MM-dd ───────────────────────────

        [HttpGet("horarios")]
        [Authorize]
        public async Task<IActionResult> ObtenerHorarios([FromQuery] string fecha)
        {
            if (!DateTime.TryParse(fecha, out var fechaDate))
                return BadRequest(new { mensaje = "Formato de fecha inválido. Use yyyy-MM-dd." });

            if (fechaDate.Date < DateTime.UtcNow.Date)
                return BadRequest(new { mensaje = "No se pueden consultar horarios de fechas pasadas." });

            var horarios = await _servicio.ObtenerHorariosDisponiblesAsync(fechaDate);
            return Ok(horarios);
        }

        // ─── POST /api/pedidos ────────────────────────────────────────────────────

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Crear([FromBody] CrearPedidoRequest request)
        {
            var usuarioId = ObtenerUsuarioId();
            if (usuarioId == 0) return Unauthorized();

            var resultado = await _servicio.CrearAsync(usuarioId, request);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });

            return CreatedAtAction(nameof(ObtenerPorId),
                new { id = resultado.Id },
                new { id = resultado.Id, mensaje = resultado.Mensaje });
        }

        // ─── GET /api/pedidos ─────────────────────────────────────────────────────
        // Devuelve los pedidos del usuario autenticado

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ObtenerMisPedidos()
        {
            var usuarioId = ObtenerUsuarioId();
            if (usuarioId == 0) return Unauthorized();

            var pedidos = await _servicio.ObtenerPorUsuarioAsync(usuarioId);
            return Ok(pedidos);
        }

        // ─── GET /api/pedidos/{id} ────────────────────────────────────────────────

        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var pedido = await _servicio.ObtenerPorIdAsync(id);
            if (pedido == null) return NotFound();

            // Usuario solo puede ver sus propios pedidos; admin puede ver cualquiera
            var usuarioId = ObtenerUsuarioId();
            var esAdmin   = User.IsInRole("Admin");
            if (!esAdmin && pedido.UsuarioId != usuarioId)
                return Forbid();

            return Ok(pedido);
        }

        // ─── GET /api/pedidos/admin/todos ─────────────────────────────────────────

        [HttpGet("admin/todos")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ObtenerTodos()
        {
            var pedidos = await _servicio.ObtenerTodosAsync();
            return Ok(pedidos);
        }

        // ─── PUT /api/pedidos/{id}/estado ─────────────────────────────────────────

        [HttpPut("{id:int}/estado")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoRequest body)
        {
            var resultado = await _servicio.ActualizarEstadoAsync(id, body.Estado);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });
            return Ok(new { mensaje = resultado.Mensaje });
        }

        // ─── PUT /api/pedidos/{id}/pago-estado ───────────────────────────────────

        [HttpPut("{id:int}/pago-estado")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ActualizarEstadoPago(int id, [FromBody] ActualizarEstadoPagoRequest body)
        {
            var resultado = await _servicio.ActualizarEstadoPagoAsync(id, body.Estado);
            if (!resultado.Exito)
                return BadRequest(new { mensaje = resultado.Mensaje });
            return Ok(new { mensaje = resultado.Mensaje });
        }

        // ─── Helper ───────────────────────────────────────────────────────────────

        private int ObtenerUsuarioId()
        {
            // JwtSecurityTokenHandler mapea "sub" → ClaimTypes.NameIdentifier por defecto
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }
    }

    public record ActualizarEstadoRequest(string Estado);
    public record ActualizarEstadoPagoRequest(string Estado);
}
