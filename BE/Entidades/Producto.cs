using ABST;
using System;

namespace BE.Entidades
{
    public class Producto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public int CategoriaId { get; set; }
        public string ImagenUrl { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Validación de dominio. Usa Resultado de ABST para coherencia entre capas.
        /// </summary>
        public Resultado Validar()
        {
            if (string.IsNullOrWhiteSpace(Nombre))
                return Resultado.Error("El nombre del producto es obligatorio.");
            if (Precio <= 0)
                return Resultado.Error("El precio debe ser mayor a cero.");
            if (Stock < 0)
                return Resultado.Error("El stock no puede ser negativo.");
            if (CategoriaId <= 0)
                return Resultado.Error("Debe especificarse una categoría válida.");

            return Resultado.Ok();
        }
    }
}
