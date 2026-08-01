using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using xsmbsocket.Lotterys.Dtos;
using xsmbsocket.Lotterys.Models;
using xsmbsocket.Lotterys.Repositories;
using xsmbsocket.Controllers;
using xsmbsocket.Shares.BaseRepository;

namespace xsmbsocket.Lotterys.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LotteryController : BaseApiController
    {
        private readonly ILotteryRepositories _repoLottery;
        private readonly ILogger<LotteryController> _logger;
        private readonly XoSoDBContext _context;

        public LotteryController(ILogger<LotteryController> logger, ILotteryRepositories repoLottery, XoSoDBContext context)
        {
            _logger = logger;
            _repoLottery = repoLottery;
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> GetTask(CancellationToken cancellationToken, [FromQuery] int page = 1, int pageSize = 50, [FromQuery] LotteryDto LotteryDto = null)
        {
            // test
            var result = await _repoLottery.GetObjectTaskAsync(LotteryDto, page, pageSize, cancellationToken);
            if (result == null)
            {
                return ApiResponseResult<object>(false, "Không tìm thấy dữ liệu", null);
            }
            return ApiResponseResult(true, "Lấy dữ liệu thành công", result);
        }
        [HttpPost]
        [Route("create")]
        public async Task<IActionResult> Create([FromBody] LotteryDto lotteryDto)
        {
            var now = DateTime.Now;
            if (lotteryDto == null)
            {
                return ApiResponseResult<object>(false, "Không có dữ liệu lottery", null);
            }
            var lottery = new Lottery
            {
               
            };
            return ApiResponseResult(true, "Thêm thành công", lottery);
        }
    }
}
