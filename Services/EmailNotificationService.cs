using System.Net;
using System.Net.Mail;
using System.Text;
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

            if (!HasValidSmtpSettings(settings))
            {
                _logger.LogWarning("Email habilitado pero falta configuracion SMTP real (Host/From/User/Password).");
                return;
            }

            var recipients = await GetRoleRecipientsAsync(cancellationToken);
            if (recipients.Count == 0)
            {
                _logger.LogWarning("No hay destinatarios de correo validos para roles Administrador/CoordinadorIT/Tecnico IT.");
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

        public async Task NotifyTicketClosedAsync(Ticket ticket, string? closedBy, CancellationToken cancellationToken = default)
        {
            var settings = LoadSettings();

            if (!settings.Enabled)
            {
                return;
            }

            if (!HasValidSmtpSettings(settings))
            {
                _logger.LogWarning("Email habilitado pero falta configuracion SMTP real (Host/From/User/Password).");
                return;
            }

            var recipients = await GetRoleRecipientsAsync(cancellationToken);
            if (recipients.Count == 0)
            {
                _logger.LogWarning("No hay destinatarios de correo validos para roles Administrador/CoordinadorIT/Tecnico IT.");
                return;
            }

            var subject = BuildClosedSubject(ticket, settings.SubjectPrefix);
            var body = BuildClosedBody(ticket, closedBy, settings.TimeZoneId);

            foreach (var recipient in recipients)
            {
                try
                {
                    await SendEmailAsync(settings, recipient, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando correo de cierre a {Recipient} para ticket {TicketNumber}", recipient, ticket.TicketNumber);
                }
            }
        }

        public async Task NotifyMaintenanceOrderReminderAsync(
            Ticket ticket,
            string reminderWindowLabel,
            CancellationToken cancellationToken = default)
        {
            var settings = LoadSettings();
            if (!settings.Enabled || !HasValidSmtpSettings(settings))
            {
                return;
            }

            var recipients = await GetMaintenanceOrderRecipientsAsync(ticket, cancellationToken);
            if (recipients.Count == 0)
            {
                return;
            }

            var subject = $"{settings.SubjectPrefix} Orden mantenimiento {reminderWindowLabel} - {Safe(ticket.TicketNumber)}";
            var body = BuildMaintenanceOrderBody(
                ticket,
                settings.TimeZoneId,
                $"Alerta automatica SLA ({reminderWindowLabel})");

            foreach (var recipient in recipients)
            {
                try
                {
                    await SendEmailAsync(settings, recipient, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error enviando alerta SLA de mantenimiento a {Recipient} para {TicketNumber}.",
                        recipient,
                        ticket.TicketNumber);
                }
            }
        }

        public async Task NotifyMaintenanceOrderCostApprovalRequiredAsync(
            Ticket ticket,
            CancellationToken cancellationToken = default)
        {
            var settings = LoadSettings();
            if (!settings.Enabled || !HasValidSmtpSettings(settings))
            {
                return;
            }

            var recipients = await GetMaintenanceOrderRecipientsAsync(ticket, cancellationToken);
            if (recipients.Count == 0)
            {
                return;
            }

            var subject = $"{settings.SubjectPrefix} Aprobacion de costos requerida - {Safe(ticket.TicketNumber)}";
            var body = BuildMaintenanceOrderBody(
                ticket,
                settings.TimeZoneId,
                "Alerta automatica de aprobacion de costos");

            foreach (var recipient in recipients)
            {
                try
                {
                    await SendEmailAsync(settings, recipient, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error enviando alerta de aprobacion de costos a {Recipient} para {TicketNumber}.",
                        recipient,
                        ticket.TicketNumber);
                }
            }
        }

        public async Task NotifyMaintenanceAppointmentCreatedAsync(
            MaintenanceAppointment appointment,
            string? createdBy,
            CancellationToken cancellationToken = default)
        {
            var settings = LoadSettings();

            if (!settings.Enabled || !HasValidSmtpSettings(settings))
            {
                return;
            }

            var recipients = await GetMaintenanceRecipientsAsync(appointment, cancellationToken);
            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "No hay destinatarios validos para la cita de mantenimiento {AppointmentNumber}.",
                    appointment.AppointmentNumber);
                return;
            }

            var subject = $"{settings.SubjectPrefix} Nueva cita de mantenimiento {appointment.AppointmentNumber}";
            var body = BuildMaintenanceAppointmentBody(
                appointment,
                createdBy,
                settings.TimeZoneId,
                "Nueva cita programada");

            foreach (var recipient in recipients)
            {
                try
                {
                    await SendEmailAsync(settings, recipient, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error enviando correo de cita a {Recipient} para {AppointmentNumber}.",
                        recipient,
                        appointment.AppointmentNumber);
                }
            }
        }

        public async Task NotifyMaintenanceAppointmentReminderAsync(
            MaintenanceAppointment appointment,
            string reminderWindowLabel,
            CancellationToken cancellationToken = default)
        {
            var settings = LoadSettings();

            if (!settings.Enabled || !HasValidSmtpSettings(settings))
            {
                return;
            }

            var recipients = await GetMaintenanceRecipientsAsync(appointment, cancellationToken);
            if (recipients.Count == 0)
            {
                return;
            }

            var subject = $"{settings.SubjectPrefix} Recordatorio {reminderWindowLabel} - {appointment.AppointmentNumber}";
            var body = BuildMaintenanceAppointmentBody(
                appointment,
                null,
                settings.TimeZoneId,
                $"Recordatorio automatico ({reminderWindowLabel})");

            foreach (var recipient in recipients)
            {
                try
                {
                    await SendEmailAsync(settings, recipient, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error enviando recordatorio de cita a {Recipient} para {AppointmentNumber}.",
                        recipient,
                        appointment.AppointmentNumber);
                }
            }
        }

        private static bool HasValidSmtpSettings(EmailSettings settings)
        {
            return !string.IsNullOrWhiteSpace(settings.SmtpHost) &&
                   !string.IsNullOrWhiteSpace(settings.FromEmail) &&
                   !string.IsNullOrWhiteSpace(settings.UserName) &&
                   !string.IsNullOrWhiteSpace(settings.Password) &&
                   !settings.UserName.Contains("PENDING", StringComparison.OrdinalIgnoreCase) &&
                   !settings.Password.Contains("PENDING", StringComparison.OrdinalIgnoreCase);
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
                            (u.Role == UserRole.Administrator ||
                             u.Role == UserRole.CoordinadorIT ||
                             u.Role == UserRole.Technician) &&
                            !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!.Trim().ToLower())
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        private async Task<List<string>> GetMaintenanceRecipientsAsync(
            MaintenanceAppointment appointment,
            CancellationToken cancellationToken)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var email in ExtractEmails(appointment.RecipientUsers))
            {
                recipients.Add(email);
            }

            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(appointment.RequestingUser))
            {
                names.Add(appointment.RequestingUser.Trim());
            }

            if (!string.IsNullOrWhiteSpace(appointment.AssignedTechnician))
            {
                names.Add(appointment.AssignedTechnician.Trim());
            }

            if (names.Count > 0)
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .Where(x => x.IsActive && names.Contains(x.FullName) && !string.IsNullOrWhiteSpace(x.Email))
                    .Select(x => x.Email!.Trim().ToLower())
                    .ToListAsync(cancellationToken);

                foreach (var email in users)
                {
                    recipients.Add(email);
                }
            }

            if (recipients.Count == 0)
            {
                var fallback = await GetRoleRecipientsAsync(cancellationToken);
                foreach (var email in fallback)
                {
                    recipients.Add(email);
                }
            }

            return recipients.ToList();
        }

        private async Task<List<string>> GetMaintenanceOrderRecipientsAsync(
            Ticket ticket,
            CancellationToken cancellationToken)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(ticket.RequestingUser))
            {
                names.Add(ticket.RequestingUser.Trim());
            }

            if (!string.IsNullOrWhiteSpace(ticket.AssignedTechnician))
            {
                names.Add(ticket.AssignedTechnician.Trim());
            }

            if (names.Count > 0)
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .Where(x => x.IsActive && names.Contains(x.FullName) && !string.IsNullOrWhiteSpace(x.Email))
                    .Select(x => x.Email!.Trim().ToLower())
                    .ToListAsync(cancellationToken);

                foreach (var email in users)
                {
                    recipients.Add(email);
                }
            }

            if (recipients.Count == 0)
            {
                var fallback = await GetRoleRecipientsAsync(cancellationToken);
                foreach (var email in fallback)
                {
                    recipients.Add(email);
                }
            }

            return recipients.ToList();
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
                FromName = section["FromName"] ?? "TRAWZACONS Service Desk",
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

            return string.Join("\n", new[]
            {
                "Se registró una nueva incidencia en TRAWZACONS Service Desk.",
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

        private static string BuildClosedSubject(Ticket ticket, string subjectPrefix)
        {
            return $"{subjectPrefix} Incidencia cerrada {ticket.TicketNumber}";
        }

        private static string BuildClosedBody(Ticket ticket, string? closedBy, string timeZoneId)
        {
            var tz = ResolveTimeZone(timeZoneId);
            var closedUtc = EnsureUtc(ticket.ClosedDate ?? DateTime.UtcNow);
            var closedLocal = TimeZoneInfo.ConvertTimeFromUtc(closedUtc, tz);

            return string.Join("\n", new[]
            {
                "Se cerró una incidencia en TRAWZACONS Service Desk.",
                string.Empty,
                $"Ticket: {ticket.TicketNumber}",
                $"Cerrada por: {Safe(closedBy)}",
                $"Solicitante: {Safe(ticket.RequestingUser)}",
                $"Fecha/Hora cierre: {closedLocal:dd/MM/yyyy hh:mm tt}",
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

        private static string BuildMaintenanceAppointmentBody(
            MaintenanceAppointment appointment,
            string? actor,
            string timeZoneId,
            string headline)
        {
            var tz = ResolveTimeZone(timeZoneId);
            var scheduledUtc = EnsureUtc(appointment.ScheduledFor);
            var scheduledLocal = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, tz);

            var body = new StringBuilder();
            body.AppendLine($"{headline} en TRAWZACONS Service Desk.");
            body.AppendLine();
            body.AppendLine($"Cita: {Safe(appointment.AppointmentNumber)}");
            body.AppendLine($"Fecha/Hora: {scheduledLocal:dd/MM/yyyy hh:mm tt}");
            body.AppendLine($"Estado: {Safe(appointment.Status)}");
            body.AppendLine($"Solicitante: {Safe(appointment.RequestingUser)}");
            body.AppendLine($"Tecnico: {Safe(appointment.AssignedTechnician, "Sin asignar")}");
            body.AppendLine($"Sitio: {Safe(appointment.Site)}");
            body.AppendLine($"Tenencia: {Safe(appointment.Tenencia)}");
            body.AppendLine($"Activo/Area: {Safe(appointment.AssetOrArea)}");
            body.AppendLine($"Tipo de mantenimiento: {Safe(appointment.MaintenanceType)}");

            if (!string.IsNullOrWhiteSpace(actor))
            {
                body.AppendLine($"Registrada por: {Safe(actor)}");
            }

            body.AppendLine();
            body.AppendLine("Descripcion:");
            body.AppendLine(Safe(appointment.Description));

            if (!string.IsNullOrWhiteSpace(appointment.Notes))
            {
                body.AppendLine();
                body.AppendLine("Notas:");
                body.AppendLine(appointment.Notes.Trim());
            }

            return body.ToString();
        }

        private static string BuildMaintenanceOrderBody(
            Ticket ticket,
            string timeZoneId,
            string headline)
        {
            var tz = ResolveTimeZone(timeZoneId);
            var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(ticket.CreatedDate), tz);
            var slaLocal = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(ticket.SLADeadline), tz);

            var status = ticket.Status.ToString();
            var stage = Safe(ticket.MaintenanceStage, "Recibida");
            var needsApproval = ticket.RequiresCostApproval && !ticket.CostApproved;

            var body = new StringBuilder();
            body.AppendLine($"{headline} en TRAWZACONS Service Desk.");
            body.AppendLine();
            body.AppendLine($"Orden: {Safe(ticket.TicketNumber)}");
            body.AppendLine($"Fecha creacion: {createdLocal:dd/MM/yyyy hh:mm tt}");
            body.AppendLine($"SLA limite: {slaLocal:dd/MM/yyyy hh:mm tt}");
            body.AppendLine($"Estado: {status}");
            body.AppendLine($"Etapa taller: {stage}");
            body.AppendLine($"Solicitante: {Safe(ticket.RequestingUser)}");
            body.AppendLine($"Tecnico: {Safe(ticket.AssignedTechnician, "Sin asignar")}");
            body.AppendLine($"Sitio: {Safe(ticket.Site)}");
            body.AppendLine($"Unidad/Equipo: {Safe(ticket.UnitCode, "No especificado")}");
            body.AppendLine($"Tipo de orden: {Safe(ticket.IncidentType)}");
            body.AppendLine();
            body.AppendLine("Costos estimados:");
            body.AppendLine($"- Mano de obra C$: {(ticket.LaborCostCordoba ?? 0m):N2}");
            body.AppendLine($"- Mano de obra $: {(ticket.LaborCostUsd ?? 0m):N2}");
            body.AppendLine($"- Servicio externo C$: {(ticket.ExternalCostCordoba ?? 0m):N2}");
            body.AppendLine($"- Servicio externo $: {(ticket.ExternalCostUsd ?? 0m):N2}");
            body.AppendLine($"- Componente C$: {(ticket.ChangedComponentCostCordoba ?? 0m):N2}");
            body.AppendLine($"- Componente $: {(ticket.ChangedComponentCostUsd ?? 0m):N2}");
            body.AppendLine($"- Requiere aprobacion: {(needsApproval ? "Si" : "No")}");

            if (needsApproval)
            {
                body.AppendLine("- Estado aprobacion: Pendiente");
            }
            else if (ticket.CostApproved)
            {
                body.AppendLine($"- Estado aprobacion: Aprobado por {Safe(ticket.CostApprovedBy, "N/A")}");
            }

            body.AppendLine();
            body.AppendLine("Descripcion:");
            body.AppendLine(Safe(ticket.Description));

            return body.ToString();
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
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

        private static string Safe(string? value, string fallback = "N/A")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static IEnumerable<string> ExtractEmails(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                yield break;
            }

            var parts = raw
                .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Contains("@", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var part in parts)
            {
                yield return part.ToLowerInvariant();
            }
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
            public string FromName { get; set; } = "TRAWZACONS Service Desk";
            public string SubjectPrefix { get; set; } = "[Service Desk]";
            public string TimeZoneId { get; set; } = "America/Managua";
        }
    }
}

