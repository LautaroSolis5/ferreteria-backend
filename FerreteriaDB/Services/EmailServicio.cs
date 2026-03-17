using BLL.Interfaces;
using L;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FerreteriaDB.Services
{
    /// <summary>
    /// Implementación de IEmailServicio usando la API REST de Brevo (puerto 443).
    /// Evita el bloqueo de puertos SMTP en entornos cloud como Render free tier.
    /// </summary>
    public class EmailServicio : IEmailServicio
    {
        private readonly IConfiguration _config;
        private readonly AppLogger      _logger;
        private readonly HttpClient     _http;

        private const string BrevoApiUrl = "https://api.brevo.com/v3/smtp/email";

        public EmailServicio(IConfiguration config, AppLogger logger, IHttpClientFactory httpFactory)
        {
            _config = config;
            _logger = logger;
            _http   = httpFactory.CreateClient("brevo");
        }

        public async Task EnviarVerificacionAsync(
            string destinatarioEmail, string nombre, string tokenRaw)
        {
            var frontendUrl     = _config["Email:FrontendBaseUrl"] ?? "https://ferreteria-adrogue.onrender.com";
            var verificationUrl = $"{frontendUrl}/verificar-email?token={tokenRaw}";

            var asunto   = "Verificá tu email – Ferretería Adrogué";
            var htmlBody = BuildVerificationEmail(nombre, verificationUrl);

            await EnviarAsync(destinatarioEmail, nombre, asunto, htmlBody);
        }

        // ─── Envío vía API REST Brevo ─────────────────────────────────────────────

        private async Task EnviarAsync(
            string destinatarioEmail, string destinatarioNombre,
            string asunto, string htmlBody)
        {
            var apiKey    = _config["Email:BrevoApiKey"]  ?? throw new InvalidOperationException("Email:BrevoApiKey no configurado.");
            var fromEmail = _config["Email:FromEmail"]    ?? throw new InvalidOperationException("Email:FromEmail no configurado.");
            var fromName  = _config["Email:FromName"]     ?? "Ferretería Adrogué";

            var payload = new
            {
                sender      = new { name = fromName,           email = fromEmail },
                to          = new[] { new { email = destinatarioEmail, name = destinatarioNombre } },
                subject     = asunto,
                htmlContent = htmlBody
            };

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoApiUrl);
            request.Headers.Add("api-key", apiKey);
            request.Content = content;

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Brevo API error {(int)response.StatusCode}: {body}");
            }

            _logger.LogInfo($"Email enviado vía Brevo API a {destinatarioEmail} — {asunto}");
        }

        // ─── Template HTML ────────────────────────────────────────────────────────

        private static string BuildVerificationEmail(string nombre, string verificationUrl) => $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Verificá tu email</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f4;font-family:Arial,Helvetica,sans-serif"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f4f4;padding:40px 0"">
    <tr>
      <td align=""center"">
        <table width=""600"" cellpadding=""0"" cellspacing=""0""
               style=""background:#ffffff;border-radius:10px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08)"">

          <!-- Header -->
          <tr>
            <td style=""background:#FF6B00;padding:32px 40px;text-align:center"">
              <h1 style=""margin:0;color:#ffffff;font-size:26px;font-weight:700;letter-spacing:-0.5px"">
                🔧 Ferretería Adrogué
              </h1>
              <p style=""margin:8px 0 0;color:rgba(255,255,255,0.85);font-size:14px"">
                Av. Espora 1180, Adrogué
              </p>
            </td>
          </tr>

          <!-- Cuerpo -->
          <tr>
            <td style=""padding:40px 40px 32px"">
              <h2 style=""margin:0 0 16px;color:#1a1a2e;font-size:22px"">
                ¡Bienvenido/a, {nombre}!
              </h2>
              <p style=""margin:0 0 12px;color:#444;font-size:15px;line-height:1.6"">
                Gracias por registrarte en <strong>Ferretería Adrogué</strong>.
                Para completar tu registro y acceder a tu cuenta, necesitamos verificar
                tu dirección de email.
              </p>
              <p style=""margin:0 0 28px;color:#444;font-size:15px;line-height:1.6"">
                Hacé clic en el botón de abajo para confirmar tu email:
              </p>

              <!-- Botón -->
              <table cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 28px"">
                <tr>
                  <td align=""center"" style=""border-radius:8px;background:#FF6B00"">
                    <a href=""{verificationUrl}""
                       target=""_blank""
                       style=""display:inline-block;padding:14px 36px;color:#ffffff;
                               font-size:15px;font-weight:700;text-decoration:none;
                               border-radius:8px;letter-spacing:0.2px"">
                      ✅ Verificar mi email
                    </a>
                  </td>
                </tr>
              </table>

              <!-- Enlace alternativo -->
              <p style=""margin:0 0 8px;color:#888;font-size:13px"">
                Si el botón no funciona, copiá y pegá este enlace en tu navegador:
              </p>
              <p style=""margin:0 0 28px;word-break:break-all"">
                <a href=""{verificationUrl}"" style=""color:#FF6B00;font-size:12px"">{verificationUrl}</a>
              </p>

              <!-- Aviso de expiración -->
              <div style=""background:#fff8f0;border-left:4px solid #FF6B00;padding:12px 16px;border-radius:0 6px 6px 0"">
                <p style=""margin:0;color:#666;font-size:13px"">
                  ⏱ Este enlace expira en <strong>24 horas</strong>.
                  Si expiró, podés solicitar uno nuevo desde la página de inicio de sesión.
                </p>
              </div>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""background:#f8f8f8;padding:20px 40px;text-align:center;border-top:1px solid #eee"">
              <p style=""margin:0 0 4px;color:#aaa;font-size:12px"">
                Si no creaste una cuenta en Ferretería Adrogué, podés ignorar este email.
              </p>
              <p style=""margin:0;color:#aaa;font-size:12px"">
                📍 Av. Espora 1180, Adrogué &nbsp;|&nbsp; 📞 +54 9 11 3115-0908
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }
}
