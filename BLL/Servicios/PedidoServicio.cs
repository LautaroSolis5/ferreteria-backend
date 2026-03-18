using ABST;
using BE.Entidades;
using BLL.Interfaces;
using BLL.Models;
using DAL.Repositorios;
using L;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace BLL.Servicios
{
    public class PedidoServicio : IPedidoServicio
    {
        private readonly PedidoRepositorio        _pedidoRepo;
        private readonly ProductoRepositorio       _productoRepo;
        private readonly NotificacionRepositorio   _notifRepo;
        private readonly AppLogger                 _logger;

        public PedidoServicio(
            PedidoRepositorio      pedidoRepo,
            ProductoRepositorio    productoRepo,
            NotificacionRepositorio notifRepo,
            AppLogger              logger)
        {
            _pedidoRepo   = pedidoRepo;
            _productoRepo = productoRepo;
            _notifRepo    = notifRepo;
            _logger       = logger;
        }

        // ─── Crear pedido ─────────────────────────────────────────────────────────

        public async Task<Resultado> CrearAsync(int usuarioId, CrearPedidoRequest request)
        {
            // 1. Validaciones básicas
            if (request.Items == null || request.Items.Count == 0)
                return new Resultado { Exito = false, Mensaje = "El pedido debe tener al menos un producto." };

            if (!MetodosPago.EsValido(request.MetodoPago))
                return new Resultado { Exito = false, Mensaje = "Método de pago inválido." };

            if (!EsHorarioValido(request.HorarioRetiro))
                return new Resultado { Exito = false, Mensaje = "El horario de retiro está fuera del horario de atención." };

            // 2. Resolver productos (precio actual + nombre para snapshot) y validar stock
            var items   = new List<PedidoItem>();
            var stockDs = new List<(int ProductoId, int Cantidad)>(); // para rollback si falla

            foreach (var req in request.Items)
            {
                if (req.Cantidad <= 0)
                    return new Resultado { Exito = false, Mensaje = $"La cantidad para el producto {req.ProductoId} debe ser mayor a cero." };

                var producto = await _productoRepo.ObtenerPorIdAsync(req.ProductoId);
                if (producto == null || !producto.Activo)
                    return new Resultado { Exito = false, Mensaje = $"El producto {req.ProductoId} no existe o está inactivo." };

                // Descontar stock atómicamente
                var ok = await _productoRepo.DescontarStockAsync(req.ProductoId, req.Cantidad);
                if (!ok)
                {
                    // Restaurar los que ya se descontaron antes de este fallo
                    foreach (var (pId, cant) in stockDs)
                        await _productoRepo.RestaurarStockAsync(pId, cant);

                    return new Resultado
                    {
                        Exito   = false,
                        Mensaje = $"Stock insuficiente para \"{producto.Nombre}\". Stock disponible: {producto.Stock}."
                    };
                }

                stockDs.Add((req.ProductoId, req.Cantidad));
                items.Add(new PedidoItem
                {
                    ProductoId     = req.ProductoId,
                    NombreProducto = producto.Nombre,
                    PrecioUnitario = producto.Precio,
                    Cantidad       = req.Cantidad,
                    Subtotal       = producto.Precio * req.Cantidad
                });
            }

            // 3. Calcular total
            decimal total = 0;
            foreach (var item in items) total += item.Subtotal;

            // 4. Crear entidades
            var pedido = new Pedido
            {
                UsuarioId     = usuarioId,
                Estado        = EstadosPedido.Pendiente,
                HorarioRetiro = request.HorarioRetiro,
                Total         = total,
                Notas         = request.Notas,
                Items         = items
            };

            var pago = new Pago
            {
                MetodoPago = request.MetodoPago,
                Estado     = EstadosPago.Pendiente,
                Monto      = total
            };

            // 5. Persistir (transacción interna en el repositorio)
            var pedidoId = await _pedidoRepo.AgregarAsync(pedido, pago);
            if (pedidoId == 0)
            {
                // Rollback de stock si la transacción de DB falló
                foreach (var (pId, cant) in stockDs)
                    await _productoRepo.RestaurarStockAsync(pId, cant);

                return new Resultado { Exito = false, Mensaje = "Error al guardar el pedido. Intente nuevamente." };
            }

            // 6. Crear notificación para admin (no bloquea si falla)
            try
            {
                var mensaje = ConstruirMensajeNotificacion(pedidoId, request, items, total);
                await _notifRepo.AgregarAsync(new Notificacion
                {
                    PedidoId = pedidoId,
                    Mensaje  = mensaje
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"BLL: No se pudo crear notificación para pedido Id={pedidoId}", ex);
            }

            _logger.LogInfo($"BLL: Pedido creado Id={pedidoId} UsuarioId={usuarioId} Total={total}");
            return new Resultado { Exito = true, Mensaje = "Pedido creado correctamente.", Id = pedidoId };
        }

        // ─── Consultas ────────────────────────────────────────────────────────────

        public async Task<IEnumerable<Pedido>> ObtenerPorUsuarioAsync(int usuarioId)
        {
            _logger.LogInfo($"BLL: ObtenerPedidos UsuarioId={usuarioId}");
            return await _pedidoRepo.ObtenerPorUsuarioAsync(usuarioId);
        }

        public async Task<Pedido?> ObtenerPorIdAsync(int id)
        {
            _logger.LogInfo($"BLL: ObtenerPedido Id={id}");
            return await _pedidoRepo.ObtenerPorIdAsync(id);
        }

        public async Task<IEnumerable<Pedido>> ObtenerTodosAsync()
        {
            _logger.LogInfo("BLL: ObtenerTodos pedidos (admin)");
            return await _pedidoRepo.ObtenerTodosAsync();
        }

        // ─── Actualizar estado ────────────────────────────────────────────────────

        public async Task<Resultado> ActualizarEstadoAsync(int id, string nuevoEstado)
        {
            var estadosValidos = new[] {
                EstadosPedido.Pendiente, EstadosPedido.Confirmado,
                EstadosPedido.Listo, EstadosPedido.Retirado, EstadosPedido.Cancelado
            };
            if (!Array.Exists(estadosValidos, e => e == nuevoEstado))
                return new Resultado { Exito = false, Mensaje = "Estado de pedido inválido." };

            // Si se cancela, restaurar stock
            if (nuevoEstado == EstadosPedido.Cancelado)
            {
                var pedido = await _pedidoRepo.ObtenerPorIdAsync(id);
                if (pedido != null)
                {
                    foreach (var item in pedido.Items)
                        await _productoRepo.RestaurarStockAsync(item.ProductoId, item.Cantidad);
                }
            }

            var ok = await _pedidoRepo.ActualizarEstadoAsync(id, nuevoEstado);
            if (!ok) return new Resultado { Exito = false, Mensaje = "Pedido no encontrado." };

            _logger.LogInfo($"BLL: Pedido Id={id} estado -> {nuevoEstado}");
            return new Resultado { Exito = true, Mensaje = $"Estado actualizado a {nuevoEstado}." };
        }

        // ─── Horarios disponibles ────────────────────────────────────────────────

        public Task<IEnumerable<HorarioDisponible>> ObtenerHorariosDisponiblesAsync(DateTime fecha)
        {
            var slots = new List<HorarioDisponible>();
            var diaSemana = fecha.DayOfWeek;

            // Lunes(1)–Viernes(5): 09:00–18:00 | Sábado(6)–Domingo(0): 10:00–13:00
            int horaInicio, horaFin;
            if (diaSemana >= DayOfWeek.Monday && diaSemana <= DayOfWeek.Friday)
            {
                horaInicio = 9;
                horaFin    = 18;
            }
            else
            {
                horaInicio = 10;
                horaFin    = 13;
            }

            for (int h = horaInicio; h < horaFin; h++)
            {
                foreach (int min in new[] { 0, 30 })
                {
                    var slot = new DateTime(fecha.Year, fecha.Month, fecha.Day, h, min, 0);
                    slots.Add(new HorarioDisponible
                    {
                        Slot    = slot,
                        Display = slot.ToString("ddd dd/MM HH:mm", new CultureInfo("es-AR"))
                    });
                }
            }

            return Task.FromResult<IEnumerable<HorarioDisponible>>(slots);
        }

        // ─── Helpers privados ─────────────────────────────────────────────────────

        private static bool EsHorarioValido(DateTime horario)
        {
            var diaSemana = horario.DayOfWeek;
            int hora = horario.Hour;
            int min  = horario.Minute;
            if (min != 0 && min != 30) return false;

            if (diaSemana >= DayOfWeek.Monday && diaSemana <= DayOfWeek.Friday)
                return hora >= 9 && hora < 18;
            else
                return hora >= 10 && hora < 13;
        }

        private static string ConstruirMensajeNotificacion(
            int pedidoId, CrearPedidoRequest request,
            List<PedidoItem> items, decimal total)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Nuevo pedido #{pedidoId}");
            sb.AppendLine($"Retiro: {request.HorarioRetiro:ddd dd/MM HH:mm}");
            sb.AppendLine($"Pago: {request.MetodoPago}");
            sb.AppendLine("Productos:");
            foreach (var item in items)
                sb.AppendLine($"  - {item.NombreProducto} x{item.Cantidad} = ${item.Subtotal:N2}");
            sb.AppendLine($"Total: ${total:N2}");
            return sb.ToString();
        }
    }
}
