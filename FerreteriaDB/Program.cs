using BLL.Interfaces;
using BLL.Servicios;
using DAL;
using DAL.Repositorios;
using FerreteriaDB.Data;
using L;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuración ────────────────────────────────────────────────────────────

var connectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("No se encontró la connection string 'PostgreSQL'. Configurala en appsettings o como variable de entorno.");
var rutaLog          = builder.Configuration["Logger:RutaArchivo"] ?? "logs/ferreteria.log";
var origenes         = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new string[0];

// ─── DI: registro de servicios ────────────────────────────────────────────────

// L - Logger singleton (comparte el lock de escritura en archivo)
builder.Services.AddSingleton<AppLogger>(_ => new AppLogger(rutaLog));

// DAL - Conexión singleton (solo guarda el connection string)
builder.Services.AddSingleton<Conexion>(_ => new Conexion(connectionString));

// DAL - Repositorios scoped (una instancia por request HTTP)
builder.Services.AddScoped<ProductoRepositorio>();
builder.Services.AddScoped<CategoriaRepositorio>();

// BLL - Servicios scoped, registrados contra sus interfaces
builder.Services.AddScoped<IProductoServicio, ProductoServicio>();
builder.Services.AddScoped<ICategoriaServicio, CategoriaServicio>();

// ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS para el frontend React
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(origenes)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ─── Pipeline ─────────────────────────────────────────────────────────────────

var app = builder.Build();

// Inicializar PostgreSQL al arrancar (crea tablas e inserta datos iniciales si están vacías)
using (var scope = app.Services.CreateScope())
{
    var conexion = scope.ServiceProvider.GetRequiredService<Conexion>();
    var logger   = scope.ServiceProvider.GetRequiredService<AppLogger>();
    DbInitializer.Inicializar(conexion, logger);
}

// Swagger disponible siempre (útil para verificar el deploy en Render)
app.UseSwagger();
app.UseSwaggerUI();

// HTTPS redirection solo en desarrollo local
// En producción Render termina SSL en su proxy inverso
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("FrontendPolicy");
app.MapControllers();

app.Run();
