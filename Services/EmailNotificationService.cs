using System.Globalization;
using System.Net;
using System.Net.Mail;
using ITServiceDeskApp.Data;
using ITServiceDeskApp.Models;
using ITServiceDeskApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITServiceDeskApp.Services
{
    public class EmailNotificationService : IEmailNotificationService
    {
        private const string ConfigSection = "EmailNotifications";

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<EmailNotificationService> logger)
        {
            _context = context;
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

            if (string.IsNullOrWhiteSpace(settings.SmtpHost) ||
                string.IsNullOrWhiteSpace(settings.FromEmail) ||
                string.IsNullOrWhiteSpace(settings.UserName) ||
                string.IsNullOrWhiteSpace(settings.Password) ||
                settings.UserName.Contains("PENDING", StringComparison.OrdinalIgnoreCase) ||
                settings.Password.Contains("PENDING", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Email habilitado pero falta configuracion SMTP real (Host/From/User/Password).");
                return;
            }

            var recipients = await GetRoleRecipientsAsync(cancellationToken);
            if (recipients.Count == 0)
            {
                _logger.LogWarning("No hay destinatarios de correo validos para roles Administrador/Tecnico IT.");
                return;
            }

            var subject = BuildSubject(ticket, settings.SubjectPrefix);
            var body = BuildBody(ticket, createdBy, settings.TimeZoneId);

            foreach (var recipient in recipients)
            {
                try
                {
                    await SendEmailAsync(settings, recipient, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando correo a {Recipient} para ticket {TicketNumber}", recipient, ticket.TicketNumber);
                }
            }
        }

        private async Task SendEmailAsync(EmailSettings settings, string recipient, string subject, string body)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromEmail!, settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(recipient);

            using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
            {
                EnableSsl = settings.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(settings.UserName, settings.Password),
                Timeout = 15000
            };

            await client.SendMailAsync(message);
        }

        private async Task<List<string>> GetRoleRecipientsAsync(CancellationToken cancellationToken)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive &&
                            (u.Role == UserRole.Administrator || u.Role == UserRole.Technician) &&
                            !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!.Trim().ToLower())
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        private EmailSettings LoadSettings()
        {
            var section = _configuration.GetSection(ConfigSection);

            var enabled = section.GetValue<bool>("Enabled");
            var enabledEnv = Environment.GetEnvironmentVariable("EMAIL_ENABLED");
            if (!string.IsNullOrWhiteSpace(enabledEnv) && bool.TryParse(enabledEnv, out var envEnabled))
            {
                enabled = envEnabled;
            }

            var smtpPort = section.GetValue<int?>("SmtpPort") ?? 587;
            var smtpPortEnv = Environment.GetEnvironmentVariable("SMTP_PORT");
            if (!string.IsNullOrWhiteSpace(smtpPortEnv) && int.TryParse(smtpPortEnv, out var parsedPort))
            {
                smtpPort = parsedPort;
            }

            var useSsl = section.GetValue<bool?>("UseSsl") ?? true;
            var useSslEnv = Environment.GetEnvironmentVariable("SMTP_USE_SSL");
            if (!string.IsNullOrWhiteSpace(useSslEnv) && bool.TryParse(useSslEnv, out var parsedSsl))
            {
                useSsl = parsedSsl;
            }

            return new EmailSettings
            {
                Enabled = enabled,
                SmtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? section["SmtpHost"],
                SmtpPort = smtpPort,
                UseSsl = useSsl,
                UserName = Environment.GetEnvironmentVariable("SMTP_USER") ?? section["UserName"],
                Password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? section["Password"],
                FromEmail = Environment.GetEnvironmentVariable("SMTP_FROM") ?? section["FromEmail"],
                FromName = section["FromName"] ?? "TRANSAN Service Desk",
                SubjectPrefix = section["SubjectPrefix"] ?? "[Service Desk]",
                TimeZoneId = section["TimeZoneId"] ?? "America/Managua"
            };
        }

        private static string BuildSubject(Ticket ticket, string subjectPrefix)
        {
            return $"{subjectPrefix} Nueva incidencia {ticket.TicketNumber}";
        }

        private static string BuildBody(Ticket ticket, string? createdBy, string timeZoneId)
        {
            var tz = ResolveTimeZone(timeZoneId);
            var createdUtc = ticket.CreatedDate.Kind switch
            {
                DateTimeKind.Utc => ticket.CreatedDate,
                DateTimeKind.Local => ticket.CreatedDate.ToUniversalTime(),
                _ => DateTime.SpecifyKind(ticket.CreatedDate, DateTimeKind.Utc)
            };

            var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(createdUtc, tz);
            var culture = CultureInfo.GetCultureInfo("es-NI");

            return string.Join("\n", new[]
            {
                "Se registró una nueva incidencia en TRANSAN Service Desk.",
                string.Empty,
                $"Ticket: {ticket.TicketNumber}",
                $"Generada por: {Safe(createdBy)}",
                $"Solicitante: {Safe(ticket.RequestingUser)}",
                $"Fecha/Hora: {createdLocal:dd/MM/yyyy hh:mm tt}",
                $"Tipo: {Safe(ticket.IncidentType)}",
                $"Prioridad: {ticket.Priority}",
                $"Estado: {ticket.Status}",
                $"Area: {Safe(ticket.Department)}",
                $"Sitio: {Safe(ticket.Site)}",
                $"Tecnico: {Safe(ticket.AssignedTechnician, "Sin asignar")}",
                string.Empty,
                "Descripcion:",
                Safe(ticket.Description)
            });
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

        private static string Safe(string? value, string fallback = "N/A")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private sealed class EmailSettings
        {
            public bool Enabled { get; set; }
            public string? SmtpHost { get; set; }
            public int SmtpPort { get; set; } = 587;
            public bool UseSsl { get; set; } = true;
            public string? UserName { get; set; }
            public string? Password { get; set; }
            public string? FromEmail { get; set; }
            public string FromName { get; set; } = "TRANSAN Service Desk";
            public string SubjectPrefix { get; set; } = "[Service Desk]";
            public string TimeZoneId { get; set; } = "America/Managua";
        }
    }
}
