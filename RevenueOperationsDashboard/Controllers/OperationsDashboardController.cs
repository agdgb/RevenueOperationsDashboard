using Microsoft.AspNetCore.Mvc;  
using RevenueOperationsDashboard.Models;  
using RevenueOperationsDashboard.Services;

namespace RevenueOperationsDashboard.Controllers  
{  
    [ApiController]  
    [Route("api/dashboard")]  
    public class OperationsDashboardController : ControllerBase  
    {  
        private readonly DashboardRepository _repo;
        private readonly IConfiguration _config;

        public OperationsDashboardController(DashboardRepository repo, IConfiguration config)  
        {  
            _repo = repo;  
            _config = config;
        }

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            var config = _config.GetSection("DashboardSettings").Get<DashboardConfigDto>();
            if (config == null) return NotFound("DashboardSettings not found in configuration.");
            return Ok(config);
        }

        [HttpGet("tax-type-performance")]  
        public async Task<IActionResult> GetTaxTypePerformance([FromQuery] DashboardFilterDto filters)  
            => Ok(await _repo.GetTaxTypePerformanceAsync(filters));

        [HttpGet("monthly-trend")]  
        public async Task<IActionResult> GetMonthlyTrend([FromQuery] DashboardFilterDto filters)  
            => Ok(await _repo.GetMonthlyTrendAsync(filters));

        [HttpGet("branch-ranking")]  
        public async Task<IActionResult> GetBranchRanking([FromQuery] DashboardFilterDto filters)  
            => Ok(await _repo.GetBranchRankingAsync(filters));

        [HttpGet("branch-volumes")]  
        public async Task<IActionResult> GetBranchVolumes([FromQuery] DashboardFilterDto filters)  
            => Ok(await _repo.GetBranchVolumesAsync(filters));

        [HttpGet("filter-options")]  
        public async Task<IActionResult> GetFilterOptions()  
            => Ok(await _repo.GetFilterOptionsAsync());  
    }  
}
