using DAL;
using L;
using Npgsql;
using System;

namespace FerreteriaDB.Data
{
    public static class DbInitializer
    {
        public static void Inicializar(Conexion conexion, AppLogger logger)
        {
            try
            {
                using var conn = conexion.ObtenerConexion();
                conn.Open();
                CrearTablas(conn);
                InsertarDatosIniciales(conn);
                logger.LogInfo("DbInitializer: Base de datos inicializada correctamente.");
            }
            catch (Exception ex)
            {
                logger.LogError("DbInitializer: Error al inicializar la base de datos.", ex);
                throw;
            }
        }

        private static void CrearTablas(NpgsqlConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Categorias (
                    Id          SERIAL          PRIMARY KEY,
                    Nombre      TEXT            NOT NULL,
                    Descripcion TEXT,
                    Activo      BOOLEAN         NOT NULL DEFAULT TRUE
                );

                CREATE TABLE IF NOT EXISTS Productos (
                    Id            SERIAL          PRIMARY KEY,
                    Nombre        TEXT            NOT NULL,
                    Descripcion   TEXT,
                    Precio        DECIMAL(18,2)   NOT NULL,
                    Stock         INTEGER         NOT NULL DEFAULT 0,
                    CategoriaId   INTEGER         NOT NULL,
                    ImagenUrl     TEXT,
                    Activo        BOOLEAN         NOT NULL DEFAULT TRUE,
                    FechaCreacion TIMESTAMP       NOT NULL,
                    FOREIGN KEY (CategoriaId) REFERENCES Categorias(Id)
                );";
            cmd.ExecuteNonQuery();
        }

        private static void InsertarDatosIniciales(NpgsqlConnection conn)
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Categorias";
            var count = Convert.ToInt32(checkCmd.ExecuteScalar());
            if (count > 0) return;

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Categorias (Nombre, Descripcion, Activo) VALUES
                    ('Herramientas Manuales',   'Martillos, destornilladores, alicates y más', TRUE),
                    ('Herramientas Eléctricas', 'Taladros, amoladoras, sierras eléctricas',    TRUE),
                    ('Fijaciones',              'Tornillos, tuercas, clavos, bulones',          TRUE),
                    ('Pinturas y Accesorios',   'Pinturas, rodillos, pinceles, masilla',        TRUE),
                    ('Plomería',                'Caños, llaves, selladores',                    TRUE),
                    ('Electricidad',            'Cables, interruptores, enchufes',              TRUE);

                INSERT INTO Productos (Nombre, Descripcion, Precio, Stock, CategoriaId, Activo, FechaCreacion) VALUES
                    ('Martillo 500g',                'Martillo de carpintero mango madera',        2850.00, 25,  1, TRUE, NOW()),
                    ('Destornillador Philips N°2',   'Destornillador de cruz punta magnética',      890.00, 50,  1, TRUE, NOW()),
                    ('Alicate Universal 8',          'Alicate universal mango aislado 1000V',      1950.00, 30,  1, TRUE, NOW()),
                    ('Taladro Percutor 650W',        'Taladro percutor con maletín y accesorios', 28500.00, 10,  2, TRUE, NOW()),
                    ('Amoladora 115mm',              'Amoladora angular 700W con disco',          22000.00,  8,  2, TRUE, NOW()),
                    ('Tornillos autorroscantes x100','Tornillos autorroscantes galvanizados 4x40',   650.00,100,  3, TRUE, NOW()),
                    ('Pintura Látex Interior 4L',    'Pintura látex blanca para interior',         8900.00, 20,  4, TRUE, NOW()),
                    ('Llave Inglesa 12',             'Llave inglesa ajustable cromada',            3200.00, 15,  5, TRUE, NOW());";
            cmd.ExecuteNonQuery();
        }
    }
}
