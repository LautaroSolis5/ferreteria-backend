using System;

namespace BE.Entidades
{
    public class Pago
    {
        public int      Id                 { get; set; }
        public int      PedidoId           { get; set; }
        public string   MetodoPago         { get; set; } = string.Empty;
        public string   Estado             { get; set; } = EstadosPago.Pendiente;
        public decimal  Monto              { get; set; }
        public string?  ExternalId         { get; set; }  // ID transacción MercadoPago
        public DateTime FechaCreacion      { get; set; } = DateTime.UtcNow;
        public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
    }
}
