namespace ABST
{
    /// <summary>
    /// Envoltorio de resultado estándar para operaciones de escritura.
    /// Todas las capas lo usan sin que ABST dependa de nadie.
    /// </summary>
    public class Resultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public int? Id { get; set; }

        public static Resultado Ok(string mensaje = "Operación exitosa", int? id = null)
            => new Resultado { Exito = true, Mensaje = mensaje, Id = id };

        public static Resultado Error(string mensaje)
            => new Resultado { Exito = false, Mensaje = mensaje };
    }
}
