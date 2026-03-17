using BE.Entidades;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FerreteriaDB.Helpers
{
    /// <summary>
    /// Genera JWT firmados con HMAC-SHA256.
    /// Claims incluidos: sub (userId), email, role, nombre.
    /// No se incluyen datos sensibles (PasswordHash, etc.).
    /// </summary>
    public class JwtHelper
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int    _expirationHours;

        public JwtHelper(IConfiguration configuration)
        {
            _secretKey       = configuration["Jwt:SecretKey"]
                               ?? throw new InvalidOperationException("Jwt:SecretKey no configurado.");
            _issuer          = configuration["Jwt:Issuer"]   ?? "ferreteria-adrogue-api";
            _audience        = configuration["Jwt:Audience"] ?? "ferreteria-adrogue";
            _expirationHours = int.TryParse(configuration["Jwt:ExpirationHours"], out var h) ? h : 8;
        }

        public string GenerarToken(Usuario usuario)
        {
            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   usuario.IdUsuario.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(ClaimTypes.Role,               usuario.RolNombre),
                new Claim("nombre", $"{usuario.Nombre} {usuario.Apellido}".Trim()),
            };

            var token = new JwtSecurityToken(
                issuer:             _issuer,
                audience:           _audience,
                claims:             claims,
                expires:            DateTime.UtcNow.AddHours(_expirationHours),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
