using ABST;
using BE.Entidades;
using BLL.Interfaces;
using DAL.Repositorios;
using L;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Servicios
{
    public class NotificacionServicio : INotificacionServicio
    {
        private readonly NotificacionRepositorio _repo;
        private readonly AppLogger               _logger;

        public NotificacionServicio(NotificacionRepositorio repo, AppLogger logger)
        {
            _repo   = repo;
            _logger = logger;
        }

        public async Task<IEnumerable<Notificacion>> ObtenerTodasAsync()
        {
            _logger.LogInfo("BLL: ObtenerTodas notificaciones (admin)");
            return await _repo.ObtenerTodasAsync();
        }

        public async Task<Resultado> MarcarComoLeidaAsync(int id)
        {
            var ok = await _repo.MarcarComoLeidaAsync(id);
            if (!ok) return new Resultado { Exito = false, Mensaje = "Notificación no encontrada." };
            _logger.LogInfo($"BLL: Notificacion Id={id} marcada como leída");
            return new Resultado { Exito = true, Mensaje = "Notificación marcada como leída." };
        }

        public async Task<int> ContarNoLeidasAsync()
        {
            return await _repo.ContarNoLeidasAsync();
        }
    }
}
