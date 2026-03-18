using System;
using System.Collections.Generic;

namespace BLL.Models
{
    public class PedidoItemRequest
    {
        public int ProductoId { get; set; }
        public int Cantidad   { get; set; }
    }

    public class CrearPedidoRequest
    {
        public List<PedidoItemRequest> Items         { get; set; } = new();
        public string                  MetodoPago    { get; set; } = string.Empty;
        public DateTime                HorarioRetiro { get; set; }
        public string?                 Notas         { get; set; }
    }
}
