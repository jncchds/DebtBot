using DebtBot.Models;
using DebtBot.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class UserSubordinatesController : ControllerBase
    {
        private IUserSubordinateService userSubbordinatesService;

        public UserSubordinatesController(IUserSubordinateService userSubbordinatesService)
        {
            this.userSubbordinatesService = userSubbordinatesService;
        }

        [HttpGet()]
        public IActionResult Get()
        {
            return Ok(userSubbordinatesService.Get());
        }

        [HttpGet("{id}")]
        public IActionResult Get(Guid id)
        {
            var subordinates = userSubbordinatesService.Get(id);
            if (subordinates?.Any() ?? false)
            {
                return Ok(subordinates);
            }

            return NotFound();
        }

        [HttpPost("{id}")]
        public IActionResult Post(Guid id, UserModel subordinate)
        {
            userSubbordinatesService.AddSubordinate(id, subordinate);

            return Ok();
        }
    }
}
