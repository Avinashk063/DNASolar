using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DNASoftech.Application.Interface;
using DNASoftech.Domain.Models;
using DNASoftech.Domain.Models.Settings;
using System.Net;
using System.Net.Mail;

namespace DNASoftech.Infrastructure.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(IEnumerable<string> to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_settings is null)
                {
                    _logger.LogError("Email settings are not configured.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_settings.SenderEmail))
                {
                    _logger.LogError("Sender email is not configured. Cannot send email with subject {Subject}", subject);
                    return false;
                }

                var recipients = to?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
                if (recipients.Count == 0)
                {
                    _logger.LogWarning("No email recipients provided for subject {Subject}", subject);
                    return false;
                }

                using var message = new MailMessage
                {
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                // Validate and set From address
                try
                {
                    message.From = new MailAddress(_settings.SenderEmail, _settings.SenderName ?? string.Empty);
                }
                catch (FormatException fx)
                {
                    _logger.LogError(fx, "Configured SenderEmail '{SenderEmail}' is not a valid email address.", _settings.SenderEmail);
                    return false;
                }

                // Add only valid recipient addresses
                foreach (var recipient in recipients)
                {
                    try
                    {
                        var addr = new MailAddress(recipient);
                        message.To.Add(addr);
                    }
                    catch (FormatException)
                    {
                        _logger.LogWarning("Skipping invalid recipient email: {Recipient}", recipient);
                    }
                }

                if (message.To.Count == 0)
                {
                    _logger.LogWarning("No valid recipient email addresses found for subject {Subject}", subject);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_settings.SmtpServer))
                {
                    _logger.LogError("SMTP server is not configured. Cannot send email with subject {Subject}", subject);
                    return false;
                }

                using var smtpClient = new SmtpClient(_settings.SmtpServer, _settings.Port)
                {
                    EnableSsl = _settings.EnableSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password)
                };

                // SmtpClient.SendMailAsync does not accept a CancellationToken in all frameworks; call the available overload.
                await smtpClient.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email sending failed for subject {Subject}", subject);
                return false;
            }
        }

        public async Task<bool> SendAppointmentConfirmationAsync(Users user, string appointmentDetails)
        {
            var adminBody = $"<p>New appointment from {user.FirstName} {user.LastName} ({user.Email})</p>{appointmentDetails}";
            var userBody = $"<p>Hi {user.FirstName}, your appointment request has been received.</p>{appointmentDetails}";

            var adminSent = await SendEmailAsync(new[] { _settings.SenderEmail }, "New Appointment Booking", adminBody);
            var userSent = await SendEmailAsync(new[] { user.Email }, "Consultation Appointment Confirmation", userBody);
            return adminSent && userSent;
        }
    }
}

