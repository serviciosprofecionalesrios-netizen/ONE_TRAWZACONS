using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITServiceDeskApp.Services
{
    public class WhatsAppNotificationService : IWhatsAppNotificationService
    {
        private const string ConfigSection = "WhatsAppNotifications";

        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhatsAppNotificationService> _logger;

        public WhatsAppNotificationService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<WhatsAppNotificationService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task NotifyNewTicketAsync(Ticket ticket, string? createdBy, CancellationToken cancellationToken = default)
        {
            var settings = LoadSettings();

            if (!settings.Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.AccountSid) ||
                string.IsNullOrWhiteSpace(settings.AuthToken) ||
                string.IsNullOrWhiteSpace(settings.FromNumber) ||
                settings.AccountSid.Contains("PENDING", StringComparison.OrdinalIgnoreCase) ||
                settings.AuthToken.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("WhatsApp habilitado pero faltan credenciales Twilio reales (SID/AuthToken/From).");
                return;
            }

            var recipients = await GetRoleRecipientsAsync(settings.DefaultCountryCode, cancellationToken);
            if (recipients.Count == 0)
            {
                _logger.LogWarning("No hay destinatarios WhatsApp validos para roles Administrador/Tecnico IT.");
                return;
            }

            var timeZone = ResolveTimeZone(settings.TimeZoneId);
            var message = BuildMessage(ticket, createdBy, timeZone);

            foreach (var recipient in recipients)
            {
                try
                {
                    await SendTwilioMessageAsync(
                        settings.AccountSid,
                        settings.AuthToken,
                        settings.FromNumber,
                        recipient,
                        message,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando WhatsApp a {Recipient} para ticket {TicketNumber}", recipient, ticket.TicketNumber);
                }
            }
        }

        private async Task SendTwilioMessageAsync(
            string accountSid,
            string authToken,
            string from,
            string to,
            string body,
            CancellationToken cancellationToken)
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["From"] = NormalizeWhatsAppNumber(from),
                    ["To"] = to,
                    ["Body"] = body
                })
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var client = _httpClientFactory.CreateClient("TwilioWhatsApp");
            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Twilio respondio {StatusCode} al enviar WhatsApp. Detalle: {Error}",
                    (int)response.StatusCode,
                    error);
            }
        }

        private async Task<List<string>> GetRoleRecipientsAsync(string defaultCountryCode, CancellationToken cancellationToken)
        {
            var phones = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive &&
                            (u.Role == UserRole.Administrator || u.Role == UserRole.Technician) &&
                            !string.IsNullOrWhiteSpace(u.Phone))
                .Select(u => u.Phone!)
                .ToListAsync(cancellationToken);

            return phones
                .Select(phone => NormalizePhoneForWhatsApp(phone, defaultCountryCode))
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }

        private WhatsAppSettings LoadSettings()
        {
            var section = _configuration.GetSection(ConfigSection);
            var fromEnv = Environment.GetEnvironmentVariable("WHATSAPP_FROM_NUMBER");

            var enabled = section.GetValue<bool>("Enabled");
            var enabledEnv = Environment.GetEnvironmentVariable("WHATSAPP_ENABLED");

            if (!string.IsNullOrWhiteSpace(enabledEnv) && bool.TryParse(enabledEnv, out var envEnabled))
            {
                enabled = envEnabled;
            }

            return new WhatsAppSettings
            {
                Enabled = enabled,
                AccountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? section["TwilioAccountSid"],
                AuthToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? section["TwilioAuthToken"],
                FromNumber = fromEnv ?? section["FromNumber"],
                TimeZoneId = section["TimeZoneId"] ?? "America/Managua",
                DefaultCountryCode = section["DefaultCountryCode"] ?? "+505"
            };
        }

        private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return TimeZoneInfo.Utc;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static string BuildMessage(Ticket ticket, string? createdBy, TimeZoneInfo timeZone)
        {
            var createdUtc = ticket.CreatedDate.Kind switch
            {
                DateTimeKind.Utc => ticket.CreatedDate,
                DateTimeKind.Local => ticket.CreatedDate.ToUniversalTime(),
                _ => DateTime.SpecifyKind(ticket.CreatedDate, DateTimeKind.Utc)
            };

            var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(createdUtc, timeZone);
            var culture = CultureInfo.GetCultureInfo("es-NI");

            var description = ticket.Description ?? string.Empty;
            if (description.Length > 180)
            {
                description = description[..180] + "...";
            }

            return string.Join("\n", new[]
            {
                "Nueva incidencia registrada",
                $"Ticket: {ticket.TicketNumber}",
                $"Generada por: {SafeValue(createdBy)}",
                $"Solicitante: {SafeValue(ticket.RequestingUser)}",
                $"Fecha/Hora: {createdLocal.ToString("dd/MM/yyyy hh:mm tt", culture)}",
                $"Tipo: {SafeValue(ticket.IncidentType)}",
                $"Prioridad: {ticket.Priority}",
                $"Estado: {ticket.Status}",
                $"Area: {SafeValue(ticket.Department)}",
                $"Sitio: {SafeValue(ticket.Site)}",
                $"Tecnico: {SafeValue(ticket.AssignedTechnician, "Sin asignar")}",
                $"Descripcion: {SafeValue(description)}"
            });
        }

        private static string NormalizeWhatsAppNumber(string value)
        {
            if (value.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return $"whatsapp:{value}";
        }

        private static string? NormalizePhoneForWhatsApp(string value, string defaultCountryCode)
        {
            var raw = value.Trim();
            if (raw.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw["whatsapp:".Length..];
            }

            var hasPlus = raw.StartsWith("+", StringComparison.Ordinal);
            var digits = new string(raw.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(digits))
            {
                return null;
            }

            if (hasPlus)
            {
                return $"whatsapp:+{digits}";
            }

            if (digits.StartsWith("505", StringComparison.Ordinal) && digits.Length >= 11)
            {
                return $"whatsapp:+{digits}";
            }

            if (digits.Length == 8)
            {
                var cc = string.IsNullOrWhiteSpace(defaultCountryCode) ? "+505" : defaultCountryCode.Trim();
                if (!cc.StartsWith("+", StringComparison.Ordinal))
                {
                    cc = "+" + cc;
                }

                return $"whatsapp:{cc}{digits}";
            }

            return $"whatsapp:+{digits}";
        }

        private static string SafeValue(string? value, string fallback = "N/A")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private sealed class WhatsAppSettings
        {
            public bool Enabled { get; set; }
            public string? AccountSid { get; set; }
            public string? AuthToken { get; set; }
            public string? FromNumber { get; set; }
            public string TimeZoneId { get; set; } = "America/Managua";
            public string DefaultCountryCode { get; set; } = "+505";
        }
    }
}

