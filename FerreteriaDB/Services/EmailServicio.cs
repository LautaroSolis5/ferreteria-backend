using BE.Entidades;
using BLL.Interfaces;
using L;
using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;
using System.Net.Http;
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
            _logger.LogInfo($"[EmailServicio] FrontendBaseUrl leído: '{frontendUrl}'");
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

        // ─── Confirmación de pedido ───────────────────────────────────────────────

        // ─── Propiedad pública para que BLL pueda construir URLs si necesita ────────
        public string FrontendBaseUrl =>
            _config["Email:FrontendBaseUrl"] ?? "https://ferreteria-adrogue.onrender.com";

        // ─── Recuperación de contraseña ───────────────────────────────────────────

        public async Task EnviarRecuperacionPasswordAsync(
            string destinatarioEmail, string nombre, string tokenRaw)
        {
            var recuperacionUrl = $"{FrontendBaseUrl}/restablecer-password?token={tokenRaw}";
            var asunto          = "🔑 Recuperá tu contraseña – Ferretería Adrogué";
            var htmlBody        = BuildRecuperacionEmail(nombre, recuperacionUrl);
            await EnviarAsync(destinatarioEmail, nombre, asunto, htmlBody);
        }

        // ─── Confirmación de pedido ───────────────────────────────────────────────

        public async Task EnviarConfirmacionPedidoAsync(Pedido pedido)
        {
            var asunto   = $"✅ Pedido #{pedido.Id} confirmado – Ferretería Adrogué";
            var htmlBody = BuildConfirmacionEmail(pedido);
            await EnviarAsync(pedido.EmailUsuario, pedido.NombreUsuario, asunto, htmlBody);
        }

        // ─── Actualización de pedido ──────────────────────────────────────────────

        public async Task EnviarActualizacionPedidoAsync(Pedido pedido, string descripcionCambio)
        {
            var asunto   = $"🔄 Actualización de tu pedido #{pedido.Id} – Ferretería Adrogué";
            var htmlBody = BuildActualizacionEmail(pedido, descripcionCambio);
            await EnviarAsync(pedido.EmailUsuario, pedido.NombreUsuario, asunto, htmlBody);
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

        // ─── Template: Confirmación de pedido ─────────────────────────────────────

        private static string BuildConfirmacionEmail(Pedido pedido)
        {
            var retiro = pedido.HorarioRetiro.ToString("dddd d 'de' MMMM 'a las' HH:mm", new CultureInfo("es-AR"));
            var itemsHtml = BuildItemsHtml(pedido);
            var pagoMetodo = pedido.Pago?.MetodoPago ?? "—";
            var pagoNota   = (pagoMetodo == "Efectivo" || pagoMetodo == "Debito")
                ? "<p style=\"margin:8px 0 0;color:#888;font-size:13px\">El pago se realiza al retirar en el local.</p>"
                : "";

            return $@"
<!DOCTYPE html>
<html lang=""es"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f4f4;padding:40px 0"">
  <tr><td align=""center"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0""
           style=""background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08)"">

      <!-- Header -->
      <tr><td style=""background:#FF6B00;padding:32px 40px;text-align:center"">
        <h1 style=""margin:0;color:#fff;font-size:26px;font-weight:700"">🔧 Ferretería Adrogué</h1>
        <p style=""margin:8px 0 0;color:rgba(255,255,255,0.85);font-size:14px"">Av. Espora 1180, Adrogué</p>
      </td></tr>

      <!-- Cuerpo -->
      <tr><td style=""padding:40px 40px 32px"">
        <h2 style=""margin:0 0 8px;color:#1a1a2e;font-size:22px"">¡Hola, {pedido.NombreUsuario}!</h2>
        <p style=""margin:0 0 24px;color:#555;font-size:15px;line-height:1.6"">
          Tu pedido <strong>#{pedido.Id}</strong> fue registrado correctamente.
          Aquí tenés el resumen:
        </p>

        <!-- Detalles -->
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border:1px solid #eee;border-radius:8px;overflow:hidden;margin-bottom:24px"">
          <tr style=""background:#fafafa"">
            <td style=""padding:12px 16px;font-size:14px;color:#555"">📅 Retiro</td>
            <td style=""padding:12px 16px;font-size:14px;color:#1a1a2e;font-weight:700;text-align:right"">
              {retiro}
            </td>
          </tr>
          <tr>
            <td style=""padding:12px 16px;font-size:14px;color:#555;border-top:1px solid #eee"">📍 Lugar</td>
            <td style=""padding:12px 16px;font-size:14px;color:#1a1a2e;font-weight:700;text-align:right;border-top:1px solid #eee"">
              Av. Espora 1180, Adrogué
            </td>
          </tr>
          <tr style=""background:#fafafa"">
            <td style=""padding:12px 16px;font-size:14px;color:#555;border-top:1px solid #eee"">💳 Método de pago</td>
            <td style=""padding:12px 16px;font-size:14px;color:#1a1a2e;font-weight:700;text-align:right;border-top:1px solid #eee"">
              {pagoMetodo}
              {pagoNota}
            </td>
          </tr>
        </table>

        <!-- Productos -->
        <h3 style=""margin:0 0 12px;font-size:15px;color:#1a1a2e"">Productos solicitados</h3>
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border:1px solid #eee;border-radius:8px;overflow:hidden;margin-bottom:24px"">
          <tr style=""background:#FF6B00"">
            <th style=""padding:10px 16px;text-align:left;font-size:13px;color:#fff;font-weight:600"">Producto</th>
            <th style=""padding:10px 16px;text-align:center;font-size:13px;color:#fff;font-weight:600"">Cant.</th>
            <th style=""padding:10px 16px;text-align:right;font-size:13px;color:#fff;font-weight:600"">Subtotal</th>
          </tr>
          {itemsHtml}
          <tr style=""background:#fff8f0"">
            <td colspan=""2"" style=""padding:12px 16px;font-size:15px;font-weight:700;color:#1a1a2e;border-top:2px solid #eee"">Total</td>
            <td style=""padding:12px 16px;font-size:15px;font-weight:700;color:#FF6B00;text-align:right;border-top:2px solid #eee"">
              {pedido.Total:C0}
            </td>
          </tr>
        </table>

        <div style=""background:#f0fdf4;border-left:4px solid #22c55e;padding:12px 16px;border-radius:0 6px 6px 0"">
          <p style=""margin:0;color:#166534;font-size:13px"">
            📬 Te avisaremos por email cada vez que haya una actualización en tu pedido.
          </p>
        </div>
      </td></tr>

      <!-- Footer -->
      <tr><td style=""background:#f8f8f8;padding:20px 40px;text-align:center;border-top:1px solid #eee"">
        <p style=""margin:0 0 4px;color:#aaa;font-size:12px"">Ferretería Adrogué — sistema automático de pedidos</p>
        <p style=""margin:0;color:#aaa;font-size:12px"">📍 Av. Espora 1180, Adrogué &nbsp;|&nbsp; 📞 +54 9 11 3115-0908</p>
      </td></tr>

    </table>
  </td></tr>
</table>
</body>
</html>";
        }

        // ─── Template: Actualización de pedido ────────────────────────────────────

        private static string BuildActualizacionEmail(Pedido pedido, string descripcionCambio)
        {
            var retiro    = pedido.HorarioRetiro.ToString("dddd d 'de' MMMM 'a las' HH:mm", new CultureInfo("es-AR"));
            var itemsHtml = BuildItemsHtml(pedido);
            var estadoColor = pedido.Estado switch
            {
                "Confirmado" => "#3b82f6",
                "Listo"      => "#8b5cf6",
                "Retirado"   => "#22c55e",
                "Cancelado"  => "#ef4444",
                _            => "#f59e0b"
            };
            var pagoColor = pedido.Pago?.Estado switch
            {
                "Aprobado"    => "#22c55e",
                "Rechazado"   => "#ef4444",
                "Reembolsado" => "#3b82f6",
                _             => "#f59e0b"
            };

            return $@"
<!DOCTYPE html>
<html lang=""es"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f4f4;padding:40px 0"">
  <tr><td align=""center"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0""
           style=""background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08)"">

      <!-- Header -->
      <tr><td style=""background:#FF6B00;padding:32px 40px;text-align:center"">
        <h1 style=""margin:0;color:#fff;font-size:26px;font-weight:700"">🔧 Ferretería Adrogué</h1>
        <p style=""margin:8px 0 0;color:rgba(255,255,255,0.85);font-size:14px"">Av. Espora 1180, Adrogué</p>
      </td></tr>

      <!-- Cuerpo -->
      <tr><td style=""padding:40px 40px 32px"">
        <h2 style=""margin:0 0 8px;color:#1a1a2e;font-size:22px"">¡Hola, {pedido.NombreUsuario}!</h2>
        <p style=""margin:0 0 20px;color:#555;font-size:15px;line-height:1.6"">
          Hay una actualización en tu pedido <strong>#{pedido.Id}</strong>:
        </p>

        <!-- Cambio destacado -->
        <div style=""background:#fff8f0;border:1px solid #ffd7b0;border-radius:8px;padding:16px 20px;margin-bottom:24px"">
          <p style=""margin:0;font-size:15px;color:#1a1a2e"">🔔 <strong>{descripcionCambio}</strong></p>
        </div>

        <!-- Estados actuales -->
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border:1px solid #eee;border-radius:8px;overflow:hidden;margin-bottom:24px"">
          <tr style=""background:#fafafa"">
            <td style=""padding:12px 16px;font-size:14px;color:#555"">Estado del pedido</td>
            <td style=""padding:12px 16px;text-align:right"">
              <span style=""background:{estadoColor};color:#fff;padding:3px 12px;border-radius:20px;font-size:13px;font-weight:600"">
                {pedido.Estado}
              </span>
            </td>
          </tr>
          <tr>
            <td style=""padding:12px 16px;font-size:14px;color:#555;border-top:1px solid #eee"">Estado del pago</td>
            <td style=""padding:12px 16px;text-align:right;border-top:1px solid #eee"">
              <span style=""background:{pagoColor};color:#fff;padding:3px 12px;border-radius:20px;font-size:13px;font-weight:600"">
                {pedido.Pago?.Estado ?? "—"}
              </span>
            </td>
          </tr>
          <tr style=""background:#fafafa"">
            <td style=""padding:12px 16px;font-size:14px;color:#555;border-top:1px solid #eee"">📅 Retiro</td>
            <td style=""padding:12px 16px;font-size:14px;color:#1a1a2e;font-weight:700;text-align:right;border-top:1px solid #eee"">
              {retiro}
            </td>
          </tr>
        </table>

        <!-- Productos -->
        <h3 style=""margin:0 0 12px;font-size:15px;color:#1a1a2e"">Tu pedido</h3>
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border:1px solid #eee;border-radius:8px;overflow:hidden;margin-bottom:24px"">
          <tr style=""background:#FF6B00"">
            <th style=""padding:10px 16px;text-align:left;font-size:13px;color:#fff;font-weight:600"">Producto</th>
            <th style=""padding:10px 16px;text-align:center;font-size:13px;color:#fff;font-weight:600"">Cant.</th>
            <th style=""padding:10px 16px;text-align:right;font-size:13px;color:#fff;font-weight:600"">Subtotal</th>
          </tr>
          {itemsHtml}
          <tr style=""background:#fff8f0"">
            <td colspan=""2"" style=""padding:12px 16px;font-size:15px;font-weight:700;color:#1a1a2e;border-top:2px solid #eee"">Total</td>
            <td style=""padding:12px 16px;font-size:15px;font-weight:700;color:#FF6B00;text-align:right;border-top:2px solid #eee"">
              {pedido.Total:C0}
            </td>
          </tr>
        </table>
      </td></tr>

      <!-- Footer -->
      <tr><td style=""background:#f8f8f8;padding:20px 40px;text-align:center;border-top:1px solid #eee"">
        <p style=""margin:0 0 4px;color:#aaa;font-size:12px"">Ferretería Adrogué — sistema automático de pedidos</p>
        <p style=""margin:0;color:#aaa;font-size:12px"">📍 Av. Espora 1180, Adrogué &nbsp;|&nbsp; 📞 +54 9 11 3115-0908</p>
      </td></tr>

    </table>
  </td></tr>
</table>
</body>
</html>";
        }

        // ─── Template: Recuperación de contraseña ─────────────────────────────────

        private static string BuildRecuperacionEmail(string nombre, string recuperacionUrl) => $@"
<!DOCTYPE html>
<html lang=""es"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f4f4;padding:40px 0"">
  <tr><td align=""center"">
    <table width=""600"" cellpadding=""0"" cellspacing=""0""
           style=""background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08)"">

      <!-- Header -->
      <tr><td style=""background:#FF6B00;padding:32px 40px;text-align:center"">
        <h1 style=""margin:0;color:#fff;font-size:26px;font-weight:700"">🔧 Ferretería Adrogué</h1>
        <p style=""margin:8px 0 0;color:rgba(255,255,255,0.85);font-size:14px"">Av. Espora 1180, Adrogué</p>
      </td></tr>

      <!-- Cuerpo -->
      <tr><td style=""padding:40px 40px 32px"">
        <h2 style=""margin:0 0 16px;color:#1a1a2e;font-size:22px"">
          ¡Hola, {nombre}!
        </h2>
        <p style=""margin:0 0 12px;color:#444;font-size:15px;line-height:1.6"">
          Recibimos una solicitud para restablecer la contraseña de tu cuenta en
          <strong>Ferretería Adrogué</strong>.
        </p>
        <p style=""margin:0 0 28px;color:#444;font-size:15px;line-height:1.6"">
          Hacé clic en el botón para crear una nueva contraseña:
        </p>

        <!-- Botón -->
        <table cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 28px"">
          <tr>
            <td align=""center"" style=""border-radius:8px;background:#FF6B00"">
              <a href=""{recuperacionUrl}""
                 target=""_blank""
                 style=""display:inline-block;padding:14px 36px;color:#ffffff;
                         font-size:15px;font-weight:700;text-decoration:none;
                         border-radius:8px;letter-spacing:0.2px"">
                🔑 Restablecer mi contraseña
              </a>
            </td>
          </tr>
        </table>

        <!-- Aviso de expiración -->
        <div style=""background:#fff8f0;border-left:4px solid #FF6B00;padding:12px 16px;border-radius:0 6px 6px 0;margin-bottom:20px"">
          <p style=""margin:0;color:#666;font-size:13px"">
            ⏱ Este enlace expira en <strong>1 hora</strong>.
            Si expiró, podés solicitar uno nuevo desde la página de inicio de sesión.
          </p>
        </div>

        <!-- Aviso de seguridad -->
        <div style=""background:#fef2f2;border-left:4px solid #ef4444;padding:12px 16px;border-radius:0 6px 6px 0"">
          <p style=""margin:0;color:#991b1b;font-size:13px"">
            🔒 Si no solicitaste este cambio, ignorá este email.
            Tu contraseña actual no fue modificada.
          </p>
        </div>
      </td></tr>

      <!-- Footer -->
      <tr><td style=""background:#f8f8f8;padding:20px 40px;text-align:center;border-top:1px solid #eee"">
        <p style=""margin:0 0 4px;color:#aaa;font-size:12px"">
          Si no solicitaste recuperar tu contraseña, podés ignorar este email con seguridad.
        </p>
        <p style=""margin:0;color:#aaa;font-size:12px"">
          📍 Av. Espora 1180, Adrogué &nbsp;|&nbsp; 📞 +54 9 11 3115-0908
        </p>
      </td></tr>

    </table>
  </td></tr>
</table>
</body>
</html>";

        // ─── Helper: filas de items para emails ───────────────────────────────────

        private static string BuildItemsHtml(Pedido pedido)
        {
            var sb = new StringBuilder();
            bool alt = false;
            foreach (var item in pedido.Items)
            {
                var bg = alt ? "" : "background:#fafafa;";
                sb.Append($@"
          <tr style=""{bg}"">
            <td style=""padding:10px 16px;font-size:14px;color:#1a1a2e;border-top:1px solid #eee"">{item.NombreProducto}</td>
            <td style=""padding:10px 16px;font-size:14px;color:#555;text-align:center;border-top:1px solid #eee"">{item.Cantidad}</td>
            <td style=""padding:10px 16px;font-size:14px;color:#1a1a2e;text-align:right;border-top:1px solid #eee"">{item.Subtotal:C0}</td>
          </tr>");
                alt = !alt;
            }
            return sb.ToString();
        }
    }
}
