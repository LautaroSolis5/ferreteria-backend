namespace BE.Entidades
{
    public class PedidoItem
    {
        public int     Id             { get; set; }
        public int     PedidoId       { get; set; }
        public int     ProductoId     { get; set; }
        public string  NombreProducto { get; set; } = string.Empty; // snapshot
        public decimal PrecioUnitario { get; set; }                  // snapshot
        public int     Cantidad       { get; set; }
        public decimal Subtotal       { get; set; }
    }
}
