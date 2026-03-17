using Npgsql;

namespace DAL
{
    /// <summary>
    /// Proveedor de conexiones a PostgreSQL.
    /// Centraliza la cadena de conexión. Registrar como Singleton en DI.
    /// </summary>
    public class Conexion
    {
        private readonly string _connectionString;

        public Conexion(string connectionString)
        {
            _connectionString = connectionString;
        }

        public NpgsqlConnection ObtenerConexion()
            => new NpgsqlConnection(_connectionString);
    }
}
