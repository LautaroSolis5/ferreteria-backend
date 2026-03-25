using DAL.Repositorios;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                AutoReturn = "approved",
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

                if (!body.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "payment")
                    return Ok();

                if (!body.TryGetProperty("data", out var dataEl) ||
                    !dataEl.TryGetProperty("id", out var idEl))
                    return Ok();

                // El id puede venir como string o número
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
                // Siempre devolver 200 para que MP no reintente indefinidamente
                return Ok();
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
