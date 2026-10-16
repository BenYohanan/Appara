using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic.IHelpers
{
    public interface IEmailService
    {
        void CallHangfire(string toEmail, string subject, string message);
        void SendEmail(string toEmail, string subject, string message);
    }
}
