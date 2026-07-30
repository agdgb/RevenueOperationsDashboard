using Microsoft.AspNetCore.Mvc;  
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;

        public OperationsDashboardController(DashboardRepository repo, IConfiguration config, IMemoryCache cache)  
        {  
            _repo = repo;  
            _config = config;
            _cache = cache;
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
        {
            string cacheKey = $"tax_type_perf_{filters.FiscalYearId}_{filters.ParentId}_{filters.PlanItemId}";
            var data = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await _repo.GetTaxTypePerformanceAsync(filters);
            });
            return Ok(data);
        }

        [HttpGet("monthly-trend")]  
        public async Task<IActionResult> GetMonthlyTrend([FromQuery] DashboardFilterDto filters)  
        {
            string cacheKey = $"monthly_trend_{filters.FiscalYearId}_{filters.ParentId}_{filters.PlanItemId}";
            var data = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await _repo.GetMonthlyTrendAsync(filters);
            });
            return Ok(data);
        }

        [HttpGet("branch-ranking")]  
        public async Task<IActionResult> GetBranchRanking([FromQuery] DashboardFilterDto filters)  
        {
            string cacheKey = $"branch_ranking_{filters.FiscalYearId}";
            var data = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await _repo.GetBranchRankingAsync(filters);
            });
            return Ok(data);
        }

        [HttpGet("branch-volumes")]  
        public async Task<IActionResult> GetBranchVolumes([FromQuery] DashboardFilterDto filters)  
        {
            string cacheKey = $"branch_volumes_{filters.FiscalYearId}_{filters.PlanItemId}";
            var data = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await _repo.GetBranchVolumesAsync(filters);
            });
            return Ok(data);
        }

        [HttpGet("filter-options")]  
        public async Task<IActionResult> GetFilterOptions()  
        {
            const string cacheKey = "filter_options";
            var data = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _repo.GetFilterOptionsAsync();
            });
            return Ok(data);
        }

        [HttpGet("top-cards")]
        public async Task<IActionResult> GetTopCards([FromQuery] DashboardFilterDto filters)
        {
            string cacheKey = $"top_cards_{filters.FiscalYearId}_{filters.ParentId}";
            var data = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await _repo.GetTopCardsAsync(filters);
            });
            return Ok(data);
        }
    }  
}
