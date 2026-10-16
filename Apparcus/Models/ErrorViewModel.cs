using Microsoft.AspNetCore.Mvc;

namespace Apparcus.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
    public static class ResponseHelper
    {
        public static JsonResult ErrorMsg()
        {
            return new JsonResult(new { isError = true, msg = "An error has occurred, try again. Please contact support if the error persists." });
        }
        /// <summary>
        /// Return Json message for error
        /// </summary>
        /// <param name="message"></param>
        /// <returns><see cref="message"/></returns>
        public static JsonResult JsonError(string message)
        {
            return new JsonResult(new { isError = true, msg = message });
        }
        /// <summary>
        /// Return Json message for success
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static JsonResult JsonSuccess(string message)
        {
            return new JsonResult(new { isError = false, msg = message });
        }
        public static JsonResult JsonErrorWithRedirect(string message, string url)
        {
            return new JsonResult(new { isError = true, msg = message, redirectUrl = url });
        }
        public static JsonResult JsonSuccessWithReturnData(object data)
        {
            return new JsonResult(new { isError = false, data = data });
        }

        public static JsonResult JsonSuccessWithReturnUrl(string url)
        {
            return new JsonResult(new { isError = false, returnUrl = url });
        }
        public static JsonResult JsonSuccessWithNoData()
        {
            return new JsonResult(new { isError = false });
        }
        public static JsonResult JsonErrorWithNoData()
        {
            return new JsonResult(new { isError = true });
        }
        public static JsonResult JsonData(object data)
        {
            return new JsonResult(data);
        }
        public static JsonResult JsonSuccessWithObject(string message, object data)
        {
            return new JsonResult(new { isError = false, msg = message, data = data });
        }
    }
}
