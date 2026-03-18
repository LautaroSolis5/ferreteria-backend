using System;

namespace BE.Entidades
{
    public class Notificacion
    {
        public int      Id            { get; set; }
        public int      PedidoId      { get; set; }
        public string   Mensaje       { get; set; } = string.Empty;
        public bool     Leida         { get; set; } = false;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Populado via JOIN para el panel admin
        public string   NombreUsuario { get; set; } = string.Empty;
        public string   EmailUsuario  { get; set; } = string.Empty;
        public DateTime HorarioRetiro { get; set; }
        public string   MetodoPago    { get; set; } = string.Empty;
        public decimal  Total         { get; set; }
    }
}
