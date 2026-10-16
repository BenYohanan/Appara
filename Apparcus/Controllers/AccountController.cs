using Apparcus.Models;
using Core.DbContext;
using Core.Models;
using Core.ViewModels;
using Logic.IHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Apparcus.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserHelper _userHelper;
        private readonly IEmailTemplateService _emailTemplateService;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IUserHelper userHelper, IEmailTemplateService emailTemplateService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userHelper = userHelper;
            _emailTemplateService = emailTemplateService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> Login(string email, string password)
        {
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return ResponseHelper.JsonError("Fill form correctly");
            }
            var user = await _userHelper.FindByEmailAsync(email).ConfigureAwait(false);
            if (user == null)
            {
                return ResponseHelper.JsonError("Invalid detail or account does not exist, contact your Admin");
            }
            var result = await _signInManager.PasswordSignInAsync(user, password, true, lockoutOnFailure: false).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return ResponseHelper.JsonError("Invalid user name or password");
            }
            user.Roles = (List<string?>)await _userManager.GetRolesAsync(user).ConfigureAwait(false);

            user.UserRole = user.Roles.Contains(Constants.SuperAdminRole) ? Constants.SuperAdminRole :
                            user.Roles.Contains(Constants.AdminRole) ? Constants.AdminRole :
                            Constants.UserRole;
            var url = _userHelper.GetValidatedUrl(user.Roles);
            var currentUser = JsonConvert.SerializeObject(user, settings);
            HttpContext.Session.SetString("loggedInUser", currentUser);
            return ResponseHelper.JsonSuccessWithReturnUrl(url);
        }

        [HttpPost]
        public async Task<JsonResult> Register(string userData)
        {
            if (string.IsNullOrEmpty(userData))
            {
                return ResponseHelper.ErrorMsg();
            }
            var applicationUser = JsonConvert.DeserializeObject<ApplicationUserViewModel>(userData);
            if (applicationUser == null)
            {
                return ResponseHelper.ErrorMsg();
            }
            var user = await _userHelper.FindByEmailAsync(applicationUser.Email).ConfigureAwait(false);
            if (user != null)
            {
                return ResponseHelper.JsonError("Email already exists, please use another email");
            }
            var createStaff = await _userHelper.RegisterUser(applicationUser).ConfigureAwait(false);
            if (createStaff == null)
            {
                return ResponseHelper.JsonError("Unable to add");
            }
            var request = HttpContext.Request;
            string baseUrl = $"{request.Scheme}://{request.Host}";

            // TEMPORARY: email sending disabled while the mail account is being fixed.
            // _emailTemplateService.SendRegistrationEmail(createStaff, baseUrl);
            return Json(new
            {
                isError = false,
                msg = "Registration successful! Redirecting to login...",
                redirectUrl = "/Account/Login"
            });
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> LogOut()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        public async Task<JsonResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { isError = true, msg = "Invalid email format." });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var request = HttpContext.Request;
                string baseUrl = $"{request.Scheme}://{request.Host}";
                string resetUrl = $"{baseUrl}/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(model.Email)}";

                // TEMPORARY: email sending disabled while the mail account is being fixed.
                // _emailTemplateService.SendPasswordResetEmail(user, resetUrl);
            }

            return Json(new
            {
                isError = false,
                msg = "If an account with that email exists, a password reset link has been sent.",
                returnUrl = "/Account/ForgotPasswordConfirmation"
            });
        }

        [HttpPost]
        public async Task<JsonResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    isError = true,
                    msg = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
                });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                return Json(new { isError = true, msg = "Invalid request." });

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                return Json(new
                {
                    isError = false,
                    msg = "Password reset successful!",
                    returnUrl = "/Account/Login"
                });
            }

            return Json(new
            {
                isError = true,
                msg = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }
    }
}