using DNASoftech.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNASoftech.Application.Interface
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(IEnumerable<string> to, string subject, string htmlBody, CancellationToken cancellationToken = default);
        Task<bool> SendAppointmentConfirmationAsync(Users user, string appointmentDetails);
    }
}

