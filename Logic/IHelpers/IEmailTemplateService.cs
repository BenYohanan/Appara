using Core.Models;

namespace Logic.IHelpers
{
    public interface IEmailTemplateService
    {
        bool SendRegistrationEmail(ApplicationUser user, string baseUrl);
        bool SendPasswordResetEmail(ApplicationUser user, string resetUrl);

        /// <summary>
        /// The code that lets a sign-in on an unfamiliar device go ahead.
        ///
        /// Doubles as the alarm: a user who did not just try to sign in has
        /// been handed proof that someone else knows their password, which is
        /// why the mail says what to do about it rather than only carrying the
        /// number.
        /// </summary>
        bool SendDeviceVerificationEmail(
            ApplicationUser user, string code, string? deviceLabel, string? ipAddress);

    }
}
