using System;
using System.Collections.Generic;

namespace BE.Entidades
{
    public static class EstadosPedido
    {
        public const string Pendiente  = "Pendiente";
        public const string Confirmado = "Confirmado";
        public const string Listo      = "Listo";
        public const string Retirado   = "Retirado";
        public const string Cancelado  = "Cancelado";
    }

    public static class EstadosPago
    {
        public const string Pendiente   = "Pendiente";
        public const string Aprobado    = "Aprobado";
        public const string Rechazado   = "Rechazado";
        public const string Reembolsado = "Reembolsado";
    }

    public static class MetodosPago
    {
        public const string Efectivo    = "Efectivo";
        public const string Debito      = "Debito";
        public const string MercadoPago = "MercadoPago";

        public static bool EsValido(string metodo) =>
            metodo == Efectivo || metodo == Debito || metodo == MercadoPago;
    }

    public class Pedido
    {
        public int      Id                 { get; set; }
        public int      UsuarioId          { get; set; }

        // Populado via JOIN
        public string   NombreUsuario      { get; set; } = string.Empty;
        public string   EmailUsuario       { get; set; } = string.Empty;

        public string   Estado             { get; set; } = EstadosPedido.Pendiente;
        public DateTime HorarioRetiro      { get; set; }
        public DateTime FechaCreacion      { get; set; } = DateTime.UtcNow;
        public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
        public decimal  Total              { get; set; }
        public string?  Notas              { get; set; }

        public bool StockDescontado { get; set; }

        // Relaciones populadas en ObtenerPorIdAsync
        public List<PedidoItem> Items { get; set; } = new();
        public Pago?            Pago  { get; set; }
    }
}
