using Hangfire;
using Logic.IHelpers;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;

namespace Logic.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _emailConfiguration;
        private readonly string? address;
        private readonly string? server;
        private readonly int port;
        private readonly string? password;
        private readonly string? companyEmail = string.Empty;
        public EmailService(IConfiguration configuration)
        {
            _emailConfiguration = configuration;
            address = _emailConfiguration["EmailConfiguration:SmtpUsername"];
            server = _emailConfiguration["EmailConfiguration:SmtpServer"];
            // TryParse, not Parse: a missing or malformed SmtpPort threw a
            // FormatException while this service was being constructed, which
            // fails whatever request resolved it rather than the one email it
            // could not send. 587 is the STARTTLS default; 465 is implicit SSL.
            port = int.TryParse(_emailConfiguration["EmailConfiguration:SmtpPort"], out var configuredPort)
                ? configuredPort
                : 587;
            password = _emailConfiguration["EmailConfiguration:SmtpPassword"];
            companyEmail = _emailConfiguration["EmailConfiguration:CompanyEmail"];
        }
        
        public void SendEmail(string toEmail, string subject, string message)
        {
            var fromAddress = new EmailAddress
            {
                Name = "Apparcus",
                Address = address
            };

            List<EmailAddress> fromAddressList =
            [
                        fromAddress
            ];
            EmailAddress toAddress = new()
            {
                Address = toEmail
            };
            List<EmailAddress> toAddressList =
            [
                    toAddress
            ];

            EmailMessage emailMessage = new()
            {
                FromAddresses = fromAddressList,
                ToAddresses = toAddressList,
                Subject = subject,
                Content = message,
                CompanyEmail = companyEmail,
                CompanyName = "Apparcus"
            };

            CallHangfire(emailMessage);
        }

        public void CallHangfire(string toEmail, string subject, string message)
        {
            BackgroundJob.Enqueue(() => SendEmail(toEmail, subject, message));
        }

        public void CallHangfire(EmailMessage emailMessage)
        {
            BackgroundJob.Enqueue(() => Send(emailMessage));
        }

        public void Send(EmailMessage emailMessage)
        {
            var message = new MimeMessage();
            message.To.AddRange(emailMessage.ToAddresses.Select(x => new MailboxAddress(x.Name, x.Address)));
            message.From.AddRange(emailMessage.FromAddresses.Select(x => new MailboxAddress(x.Name, x.Address)));

            if (!string.IsNullOrEmpty(emailMessage.CompanyName) && !string.IsNullOrEmpty(emailMessage.CompanyEmail))
            {
                message.ReplyTo.Add(new MailboxAddress(emailMessage.CompanyName, emailMessage.CompanyEmail));
            }
            
            message.Subject = emailMessage.Subject;

            message.Body = new TextPart(TextFormat.Html)
            {
                Text = emailMessage.Content
            };
            // Said plainly, and before the connection, because the server's own
            // answer to an empty credential is the same 5.7.8 "Username and
            // Password not accepted" it gives to a wrong one — which sends
            // whoever is reading the log hunting for a bad password when the
            // real fault is a section that never loaded.
            if (string.IsNullOrWhiteSpace(server)
                || string.IsNullOrWhiteSpace(address)
                || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "EmailConfiguration is incomplete — SmtpServer, SmtpUsername and "
                    + "SmtpPassword must all be set. Nothing was sent.");
            }

            using var emailClient = new MailKit.Net.Smtp.SmtpClient();

            // The certificate is checked. It used to be waved through by a
            // callback that returned true for anything, which on a network
            // someone else controls means the password and every code and
            // reset link in these messages can be read by whoever answers.
            emailClient.Connect(server, port, SecureSocketOptions.Auto);
            emailClient.AuthenticationMechanisms.Remove("XOAUTH2");
            emailClient.Authenticate(address, password);
            emailClient.Send(message);
            emailClient.Disconnect(true);
        }
    }
    public class EmailAddress
    {
        public string? Name { get; set; }

        public string? Address { get; set; }
    }
    public class EmailMessage
    {
        public EmailMessage()
        {
            ToAddresses = [];
            FromAddresses = [];
        }

        public List<EmailAddress> ToAddresses { get; set; }

        public List<EmailAddress> FromAddresses { get; set; }

        public string? Subject { get; set; }

        public string? Content { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyEmail { get; set; }
    }
}
