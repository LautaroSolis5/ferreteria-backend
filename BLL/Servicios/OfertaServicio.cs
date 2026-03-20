using BE.Entidades;
using BLL.Interfaces;
using DAL.Repositorios;
using L;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Servicios
{
    public class OfertaServicio : IOfertaServicio
    {
        private readonly OfertaRepositorio _repo;
        private readonly AppLogger         _logger;

        public OfertaServicio(OfertaRepositorio repo, AppLogger logger)
        {
            _repo   = repo;
            _logger = logger;
        }

        public Task<Oferta>              ObtenerActivaAsync()      => _repo.ObtenerActivaAsync();
        public Task<IEnumerable<Oferta>> ObtenerTodosAsync()       => _repo.ObtenerTodosAsync();
        public Task<Oferta>              ObtenerPorIdAsync(int id) => _repo.ObtenerPorIdAsync(id);
        public Task<Oferta>              ToggleActivaAsync(int id) => _repo.ToggleActivaAsync(id);
        public Task                      EliminarAsync(int id)     => _repo.EliminarAsync(id);

        public async Task<Oferta> CrearAsync(Oferta oferta)
        {
            var val = oferta.Validar();
            if (!val.Exitoso) throw new Exception(val.Mensaje);
            return await _repo.CrearAsync(oferta);
        }

        public async Task<Oferta> ActualizarAsync(Oferta oferta)
        {
            var val = oferta.Validar();
            if (!val.Exitoso) throw new Exception(val.Mensaje);
            return await _repo.ActualizarAsync(oferta);
        }
    }
}
