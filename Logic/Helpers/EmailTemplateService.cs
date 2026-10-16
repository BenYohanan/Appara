using Core.Models;
using Logic.IHelpers;

namespace Logic.Helpers
{
    public class EmailTemplateService(IEmailService emailService) : IEmailTemplateService
    {
        private readonly IEmailService _emailService = emailService;

        public bool SendRegistrationEmail(ApplicationUser user, string baseUrl)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }
            string loginLink = $"{baseUrl}/Account/Login";
            string subject = "Welcome to Apparcus!";
            string message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f4f4f4;'>
                    <div style='background-color: #ffffff; padding: 20px; border: 1px solid #e0e0e0; text-align: center;'>
                        <h1 style='color: #9333ea; font-size: 24px;'>Welcome!</h1>
                        <p style='color: #333333; font-size: 16px;'>
                            Dear {user.FullName},<br/>
                            Your account has been successfully created,<br/>
                            and you now have full access to Apparcus platform. <br/>
                            Please click the button below to log in and access your dashboard.
                        </p>
                        <p style='color: #333333;'>
                            Login Link: <a href='{loginLink}' style='color: #9333ea;'>Login</a>.
                        </p>
                        <p style='color: #333333;'>
                            Need help? Contact us at <a href='mailto:support@apparcus.com' style='color: #9333ea;'>support@apparcus.com</a>.
                        </p>
                        <p><b>Kind regards,</b><br/>Apparcus Team</p>
                    </div>
                </div>";

            try
            {
                _emailService.CallHangfire(user.Email, subject, message);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool SendPasswordResetEmail(ApplicationUser user, string resetLink)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            string subject = "Password Reset Request";
            string message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f4f4f4;'>
                    <div style='background-color: #ffffff; padding: 20px; border: 1px solid #e0e0e0; text-align: center;'>
                        <h1 style='color: #9333ea; font-size: 24px;'>Reset Your Password</h1>
                        <p style='color: #333333; font-size: 16px;'>
                            Hello {user.FullName},<br/><br/>
                            We received a request to reset your password. 
                            If you made this request, click the button below. 
                            If not, please ignore this email.
                        </p>

                        <a href='{resetLink}' 
                           style='display: inline-block; background-color: #9333ea; color: white; padding: 12px 20px; 
                                  text-decoration: none; border-radius: 5px; margin-top: 20px;'>
                            Reset Password
                        </a>

                        <p style='color: #333333; margin-top: 20px;'>
                            This link will expire in 30 minutes for your security.
                        </p>

                        <p style='color: #333333;'>
                            Need help? Contact <a href='mailto:support@apparcus.com' style='color: #9333ea;'>support@apparcus.com</a>.
                        </p>

                        <p><b>Kind regards,</b><br/>Apparcus Team</p>
                    </div>
                </div>";

            try
            {
                _emailService.CallHangfire(user.Email, subject, message);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SendDeviceVerificationEmail(
            ApplicationUser user, string code, string? deviceLabel, string? ipAddress)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            var device = string.IsNullOrWhiteSpace(deviceLabel) ? "a new device" : deviceLabel;
            var fromWhere = string.IsNullOrWhiteSpace(ipAddress)
                ? string.Empty
                : $"<br/>IP address: {ipAddress}";

            string subject = "Your Apparcus sign-in code";

            // The warning is the point of the mail as much as the code is. A
            // user who did not just try to sign in is reading proof that
            // someone else has their password, and they can act on it: while
            // the code stays in this inbox, the other device gets nowhere.
            string message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f4f4f4;'>
                    <div style='background-color: #ffffff; padding: 20px; border: 1px solid #e0e0e0; text-align: center;'>
                        <h1 style='color: #9333ea; font-size: 24px;'>Confirm it's you</h1>
                        <p style='color: #333333; font-size: 16px;'>
                            Hello {user.FullName},<br/><br/>
                            Someone signed in to your Apparcus account from {device}.
                            Enter this code on that device to continue.{fromWhere}
                        </p>

                        <p style='font-size: 34px; letter-spacing: 10px; font-weight: bold; color: #9333ea; margin: 24px 0;'>
                            {code}
                        </p>

                        <p style='color: #333333;'>
                            This code expires in 10 minutes and can only be used on the device that asked for it.
                        </p>

                        <p style='color: #b91c1c; font-size: 15px; margin-top: 24px;'>
                            <b>If this wasn't you, do not share this code.</b><br/>
                            Someone else knows your password. Your account stays signed in
                            where you are now — nothing changes unless this code is entered.
                            Please change your password straight away.
                        </p>

                        <p style='color: #333333;'>
                            Need help? Contact <a href='mailto:support@apparcus.com' style='color: #9333ea;'>support@apparcus.com</a>.
                        </p>

                        <p><b>Kind regards,</b><br/>Apparcus Team</p>
                    </div>
                </div>";

            try
            {
                _emailService.CallHangfire(user.Email, subject, message);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
