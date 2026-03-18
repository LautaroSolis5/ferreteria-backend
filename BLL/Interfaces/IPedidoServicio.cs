using ABST;
using BE.Entidades;
using BLL.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IPedidoServicio
    {
        Task<Resultado>                      CrearAsync(int usuarioId, CrearPedidoRequest request);
        Task<IEnumerable<Pedido>>            ObtenerPorUsuarioAsync(int usuarioId);
        Task<Pedido?>                        ObtenerPorIdAsync(int id);
        Task<IEnumerable<Pedido>>            ObtenerTodosAsync();
        Task<Resultado>                      ActualizarEstadoAsync(int id, string nuevoEstado);
        Task<IEnumerable<HorarioDisponible>> ObtenerHorariosDisponiblesAsync(DateTime fecha);
    }
}
