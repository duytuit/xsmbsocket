using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xsmbsocket.Models;
using xsmbsocket.Services;

namespace xsmbsocket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly WebSocketManager _manager;

        public StatusController(WebSocketManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();

            var result = new SystemStatus
            {
                Connections = _manager.TotalConnections,
                Memory = process.WorkingSet64 / 1024 / 1024,
                Time = DateTime.Now,
                Uptime = DateTime.Now - process.StartTime
            };

            return Ok(result);
        }
    }
}
