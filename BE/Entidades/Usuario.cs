namespace BE.Entidades
{
    public class Usuario
    {
        public int       IdUsuario      { get; set; }
        public string    Nombre         { get; set; } = string.Empty;
        public string    Apellido       { get; set; } = string.Empty;
        public string    Email          { get; set; } = string.Empty;
        public string?   PasswordHash   { get; set; }
        public int       RolId          { get; set; }
        public string    RolNombre      { get; set; } = string.Empty;  // populado via JOIN
        public bool      Activo         { get; set; } = true;
        public string    AuthProvider   { get; set; } = "local";       // "local" | "google"
        public string?   ProviderUserId { get; set; }                  // Google sub
        public DateTime  FechaCreacion  { get; set; }
        public DateTime? UltimoLogin    { get; set; }
    }
}
