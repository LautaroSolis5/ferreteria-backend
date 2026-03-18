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
                ActualizarEsquema(conn);   // ALTER TABLE para instalaciones existentes
                InsertarDatosIniciales(conn);
                logger.LogInfo("DbInitializer: Base de datos inicializada correctamente.");
            }
            catch (Exception ex)
            {
                logger.LogError("DbInitializer: Error al inicializar la base de datos.", ex);
                throw;
            }
        }

        // ─── Creación de tablas (instalaciones nuevas) ────────────────────────────

        private static void CrearTablas(NpgsqlConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                -- Tablas de catálogo
                CREATE TABLE IF NOT EXISTS Categorias (
                    Id          SERIAL        PRIMARY KEY,
                    Nombre      TEXT          NOT NULL,
                    Descripcion TEXT,
                    Activo      BOOLEAN       NOT NULL DEFAULT TRUE
                );

                CREATE TABLE IF NOT EXISTS Productos (
                    Id            SERIAL        PRIMARY KEY,
                    Nombre        TEXT          NOT NULL,
                    Descripcion   TEXT,
                    Precio        DECIMAL(18,2) NOT NULL,
                    Stock         INTEGER       NOT NULL DEFAULT 0,
                    CategoriaId   INTEGER       NOT NULL,
                    ImagenUrl     TEXT,
                    Activo        BOOLEAN       NOT NULL DEFAULT TRUE,
                    FechaCreacion TIMESTAMP     NOT NULL,
                    FOREIGN KEY (CategoriaId) REFERENCES Categorias(Id)
                );

                -- Tablas de autenticación
                CREATE TABLE IF NOT EXISTS Roles (
                    IdRol     SERIAL PRIMARY KEY,
                    NombreRol TEXT   NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS Usuarios (
                    IdUsuario         SERIAL    PRIMARY KEY,
                    Nombre            TEXT      NOT NULL,
                    Apellido          TEXT      NOT NULL DEFAULT '',
                    Email             TEXT      NOT NULL UNIQUE,
                    PasswordHash      TEXT,
                    RolId             INTEGER   NOT NULL REFERENCES Roles(IdRol),
                    Activo            BOOLEAN   NOT NULL DEFAULT TRUE,
                    AuthProvider      TEXT      NOT NULL DEFAULT 'local',
                    ProviderUserId    TEXT,
                    FechaCreacion     TIMESTAMP NOT NULL DEFAULT NOW(),
                    UltimoLogin       TIMESTAMP,
                    EmailVerificado   BOOLEAN   NOT NULL DEFAULT FALSE,
                    TokenVerificacion TEXT,
                    TokenExpiracion   TIMESTAMP
                );

                -- Tablas de pedidos
                CREATE TABLE IF NOT EXISTS Pedidos (
                    Id                 SERIAL        PRIMARY KEY,
                    UsuarioId          INTEGER       NOT NULL REFERENCES Usuarios(IdUsuario),
                    Estado             TEXT          NOT NULL DEFAULT 'Pendiente',
                    HorarioRetiro      TIMESTAMP     NOT NULL,
                    FechaCreacion      TIMESTAMP     NOT NULL DEFAULT NOW(),
                    FechaActualizacion TIMESTAMP     NOT NULL DEFAULT NOW(),
                    Total              DECIMAL(18,2) NOT NULL,
                    Notas              TEXT
                );

                CREATE TABLE IF NOT EXISTS PedidoItems (
                    Id             SERIAL        PRIMARY KEY,
                    PedidoId       INTEGER       NOT NULL REFERENCES Pedidos(Id) ON DELETE CASCADE,
                    ProductoId     INTEGER       NOT NULL REFERENCES Productos(Id),
                    NombreProducto TEXT          NOT NULL,
                    PrecioUnitario DECIMAL(18,2) NOT NULL,
                    Cantidad       INTEGER       NOT NULL,
                    Subtotal       DECIMAL(18,2) NOT NULL
                );

                -- Pagos separado para compatibilidad con webhooks MercadoPago
                CREATE TABLE IF NOT EXISTS Pagos (
                    Id                 SERIAL        PRIMARY KEY,
                    PedidoId           INTEGER       NOT NULL REFERENCES Pedidos(Id),
                    MetodoPago         TEXT          NOT NULL,
                    Estado             TEXT          NOT NULL DEFAULT 'Pendiente',
                    Monto              DECIMAL(18,2) NOT NULL,
                    ExternalId         TEXT,
                    FechaCreacion      TIMESTAMP     NOT NULL DEFAULT NOW(),
                    FechaActualizacion TIMESTAMP     NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS Notificaciones (
                    Id            SERIAL    PRIMARY KEY,
                    PedidoId      INTEGER   NOT NULL REFERENCES Pedidos(Id),
                    Mensaje       TEXT      NOT NULL,
                    Leida         BOOLEAN   NOT NULL DEFAULT FALSE,
                    FechaCreacion TIMESTAMP NOT NULL DEFAULT NOW()
                );

                -- Índices de rendimiento
                CREATE INDEX IF NOT EXISTS idx_pedidos_usuarioid    ON Pedidos(UsuarioId);
                CREATE INDEX IF NOT EXISTS idx_pedidoitems_pedidoid ON PedidoItems(PedidoId);
                CREATE INDEX IF NOT EXISTS idx_pagos_pedidoid       ON Pagos(PedidoId);
                CREATE INDEX IF NOT EXISTS idx_notif_leida          ON Notificaciones(Leida);";
            cmd.ExecuteNonQuery();
        }

        // ─── Migraciones para instalaciones existentes ───────────────────────────
        // Agrega columnas nuevas de forma segura si ya existe la tabla.

        private static void ActualizarEsquema(NpgsqlConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS EmailVerificado   BOOLEAN   NOT NULL DEFAULT FALSE;
                ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS TokenVerificacion TEXT;
                ALTER TABLE Usuarios ADD COLUMN IF NOT EXISTS TokenExpiracion   TIMESTAMP;";
            cmd.ExecuteNonQuery();
        }

        // ─── Datos iniciales ──────────────────────────────────────────────────────

        private static void InsertarDatosIniciales(NpgsqlConnection conn)
        {
            SeedRoles(conn);
            SeedAdminUsuario(conn);
            SeedCatalogo(conn);
        }

        private static void SeedRoles(NpgsqlConnection conn)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Roles (IdRol, NombreRol) VALUES (1, 'Admin'), (2, 'Usuario')
                ON CONFLICT (IdRol) DO NOTHING;";
            cmd.ExecuteNonQuery();
        }

        private static void SeedAdminUsuario(NpgsqlConnection conn)
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE Email = 'admin@ferreteria.com'";
            var existe = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            if (existe) return;

            string adminHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!", workFactor: 12);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Usuarios
                    (Nombre, Apellido, Email, PasswordHash, RolId, Activo,
                     AuthProvider, FechaCreacion, EmailVerificado)
                VALUES
                    ('Admin', 'Ferreteria', 'admin@ferreteria.com', @hash, 1, TRUE,
                     'local', NOW(), TRUE)
                ON CONFLICT (Email) DO NOTHING;";
            cmd.Parameters.AddWithValue("@hash", adminHash);
            cmd.ExecuteNonQuery();
        }

        private static void SeedCatalogo(NpgsqlConnection conn)
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
