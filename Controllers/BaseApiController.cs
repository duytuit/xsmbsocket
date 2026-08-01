using System.Reflection.Metadata;
using Microsoft.AspNetCore.Mvc;
namespace xsmbsocket.Controllers
{
    [ApiController]
    public class BaseApiController : ControllerBase
    {
        protected ObjectResult ApiResponseResult<T>(bool status, string message, T data = default)
        {
            return new ObjectResult(new ApiResponse<T>(status, message, data));
        }
    }
    public class ApiResponse<T>
    {
        public bool status { get; set; }
        public string message { get; set; }
        public T data { get; set; }

        public ApiResponse(bool _status, string _message, T _data = default)
        {
            status = _status;
            message = _message;
            data = _data;
        }
    }
}