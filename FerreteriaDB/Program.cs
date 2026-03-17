using BLL.Interfaces;
using BLL.Servicios;
using DAL;
using DAL.Repositorios;
using FerreteriaDB.Data;
using FerreteriaDB.Helpers;
using FerreteriaDB.Services;
using L;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuración ────────────────────────────────────────────────────────────

var connectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("No se encontró la connection string 'PostgreSQL'.");
var rutaLog  = builder.Configuration["Logger:RutaArchivo"] ?? "logs/ferreteria.log";
var origenes = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

// ─── DI: Logger y DB ─────────────────────────────────────────────────────────

builder.Services.AddSingleton<AppLogger>(_ => new AppLogger(rutaLog));
builder.Services.AddSingleton<Conexion>(_ => new Conexion(connectionString));

// ─── DI: Repositorios ────────────────────────────────────────────────────────

builder.Services.AddScoped<ProductoRepositorio>();
builder.Services.AddScoped<CategoriaRepositorio>();
builder.Services.AddScoped<UsuarioRepositorio>();

// ─── DI: Servicios ───────────────────────────────────────────────────────────

builder.Services.AddHttpClient("brevo");
builder.Services.AddScoped<IProductoServicio,  ProductoServicio>();
builder.Services.AddScoped<ICategoriaServicio, CategoriaServicio>();
builder.Services.AddScoped<IEmailServicio,     EmailServicio>();
builder.Services.AddScoped<IAuthServicio,      AuthServicio>();

// ─── JWT Helper ──────────────────────────────────────────────────────────────

builder.Services.AddSingleton<JwtHelper>();

// ─── JWT Authentication ──────────────────────────────────────────────────────

var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey no configurado.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero, // sin tolerancia de expiración
        };
    });

builder.Services.AddAuthorization();

// ─── ASP.NET Core ────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Habilita el botón "Authorize" en Swagger para enviar Bearer token
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Ingresá el token JWT (sin el prefijo Bearer)"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── CORS ────────────────────────────────────────────────────────────────────

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

// Inicializar BD al arrancar
using (var scope = app.Services.CreateScope())
{
    var conexion = scope.ServiceProvider.GetRequiredService<Conexion>();
    var logger   = scope.ServiceProvider.GetRequiredService<AppLogger>();
    DbInitializer.Inicializar(conexion, logger);
}

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");

// IMPORTANTE: Authentication ANTES que Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
