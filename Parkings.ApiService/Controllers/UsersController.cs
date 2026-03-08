using Microsoft.AspNetCore.Mvc;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController(AppDbContext db) : ControllerBase
    {
        private readonly AppDbContext _db = db;
    }
}
