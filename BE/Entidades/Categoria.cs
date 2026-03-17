using ABST;

namespace BE.Entidades
{
    public class Categoria
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;

        public Resultado Validar()
        {
            if (string.IsNullOrWhiteSpace(Nombre))
                return Resultado.Error("El nombre de la categoría es obligatorio.");

            return Resultado.Ok();
        }
    }
}
