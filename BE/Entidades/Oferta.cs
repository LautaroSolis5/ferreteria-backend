using ABST;
using System;
using System.Collections.Generic;

namespace BE.Entidades
{
    public class Oferta
    {
        public int      IdOferta       { get; set; }
        public string   Titulo         { get; set; } = string.Empty;
        public string   Descripcion    { get; set; } = string.Empty;
        public decimal  PrecioOferta   { get; set; }
        public decimal? PrecioAnterior { get; set; }
        public string   ImagenUrl      { get; set; }
        public bool     EsCombo        { get; set; } = false;
        public bool     Activa         { get; set; } = false;
        public DateTime? FechaInicio   { get; set; }
        public DateTime? FechaFin      { get; set; }
        public DateTime  FechaCreacion { get; set; } = DateTime.UtcNow;

        // Productos incluidos en la oferta (hidratados)
        public List<int>     ProductoIds { get; set; } = new();
        public List<Producto> Productos  { get; set; } = new();

        public Resultado Validar()
        {
            if (string.IsNullOrWhiteSpace(Titulo))
                return Resultado.Error("El título de la oferta es obligatorio.");
            if (PrecioOferta <= 0)
                return Resultado.Error("El precio de la oferta debe ser mayor a cero.");
            return Resultado.Ok();
        }
    }
}
