using African_Nations_league.Data;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace African_Nations_league.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly DbSeeder _seeder;
        public AdminController(DbSeeder seeder) => _seeder = seeder;

        [HttpPost("seed")]
        public async Task<IActionResult> Seed()
        {
            await _seeder.SeedTeamsAsync();
            return Ok(new { seeded = true });
        }
    }
}
