using DAL.Repositorios;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FerreteriaDB.Controllers
{
    [ApiController]
    [Route("api/pagos")]
    public class PagosController : ControllerBase
    {
        private readonly IConfiguration  _config;
        private readonly PagoRepositorio _pagoRepo;

        public PagosController(IConfiguration config, PagoRepositorio pagoRepo)
        {
            _config   = config;
            _pagoRepo = pagoRepo;
        }

        // ── POST /api/pagos/preferencia ───────────────────────────────────────
        [HttpPost("preferencia")]
        [Authorize]
        public async Task<IActionResult> CrearPreferencia([FromBody] CrearPreferenciaDto req)
        {
            MercadoPagoConfig.AccessToken = _config["MercadoPago:AccessToken"];
            var frontendUrl = _config["FrontendBaseUrl"] ?? "https://ferreteria-adrogue.onrender.com";

            var items = req.Items.Select(i => new PreferenceItemRequest
            {
                Title      = i.Titulo,
                Quantity   = i.Cantidad,
                CurrencyId = "ARS",
                UnitPrice  = i.PrecioUnitario,
            }).ToList();

            var backendUrl = _config["BackendBaseUrl"] ?? "https://ferreteria-backend.onrender.com";

            var preferenceRequest = new PreferenceRequest
            {
                Items             = items,
                ExternalReference = req.PedidoId.ToString(),
                BackUrls          = new PreferenceBackUrlsRequest
                {
                    Success = $"{frontendUrl}/pago/exitoso",
                    Failure = $"{frontendUrl}/pago/fallido",
                    Pending = $"{frontendUrl}/pago/pendiente",
                },
                AutoReturn      = "approved",
                NotificationUrl = $"{backendUrl}/api/pagos/webhook",
            };

            var client     = new PreferenceClient();
            var preference = await client.CreateAsync(preferenceRequest);

            return Ok(new
            {
                preferenceId = preference.Id,
                initPoint    = preference.InitPoint,
            });
        }

        // ── POST /api/pagos/webhook ───────────────────────────────────────────
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromBody] JsonElement body)
        {
            try
            {
                MercadoPagoConfig.AccessToken = _config["MercadoPago:AccessToken"];

                // ── Validar firma x-signature ─────────────────────────────────
                var secret = _config["MercadoPago:WebhookSecret"];
                if (!string.IsNullOrEmpty(secret))
                {
                    var xSignature = Request.Headers["x-signature"].FirstOrDefault() ?? "";
                    var xRequestId = Request.Headers["x-request-id"].FirstOrDefault() ?? "";
                    var dataIdQp   = Request.Query["data.id"].FirstOrDefault() ?? "";

                    string? ts = null, v1 = null;
                    foreach (var part in xSignature.Split(','))
                    {
                        var kv = part.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var k = kv[0].Trim();
                            if (k == "ts") ts = kv[1].Trim();
                            else if (k == "v1") v1 = kv[1].Trim();
                        }
                    }

                    if (ts != null && v1 != null)
                    {
                        // Construir manifest según doc de MP
                        var parts = new List<string>();
                        if (!string.IsNullOrEmpty(dataIdQp))   parts.Add($"id:{dataIdQp}");
                        if (!string.IsNullOrEmpty(xRequestId)) parts.Add($"request-id:{xRequestId}");
                        parts.Add($"ts:{ts}");
                        var manifest = string.Join(";", parts) + ";";

                        using var hmac  = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                        var computed    = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest))).ToLower();

                        if (computed != v1) return Ok(); // firma inválida, ignorar
                    }
                }

                // ── Procesar notificación ─────────────────────────────────────
                if (!body.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "payment")
                    return Ok();

                if (!body.TryGetProperty("data", out var dataEl) ||
                    !dataEl.TryGetProperty("id", out var idEl))
                    return Ok();

                var paymentIdStr = idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString()
                    : idEl.GetInt64().ToString();

                if (!long.TryParse(paymentIdStr, out var paymentId)) return Ok();

                var paymentClient = new PaymentClient();
                var payment       = await paymentClient.GetAsync(paymentId);

                if (payment?.ExternalReference == null) return Ok();
                if (!int.TryParse(payment.ExternalReference, out var pedidoId)) return Ok();

                var estado = payment.Status switch
                {
                    "approved" => "Aprobado",
                    "pending"  => "Pendiente",
                    "rejected" => "Rechazado",
                    _          => "Pendiente",
                };

                await _pagoRepo.ActualizarEstadoAsync(pedidoId, estado, paymentId.ToString());

                return Ok();
            }
            catch
            {
                return Ok(); // Siempre 200 para que MP no reintente
            }
        }
    }

    public class CrearPreferenciaDto
    {
        public int PedidoId { get; set; }
        public List<ItemPreferenciaDto> Items { get; set; } = new();
    }

    public class ItemPreferenciaDto
    {
        public string  Titulo          { get; set; } = string.Empty;
        public int     Cantidad        { get; set; }
        public decimal PrecioUnitario  { get; set; }
    }
}
