using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : Controller
    {
        public IUserService _userService { get; }

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("{id}")]
        public IActionResult Get(Guid id)
        {
            var user = _userService.GetUserById(id);

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpGet()]
        public IActionResult Get()
        {
            var users = _userService.GetUsers();

            if (users?.Any() ?? false)
                return Ok(users);

            return NotFound();
        }

        [HttpPost()]
        public ActionResult Post(User user)
        {
            _userService.AddUser(user);

            return Ok();
        }

        [HttpDelete("{id}")]
        public ActionResult Delete(Guid id)
        {
            _userService.DeleteUser(id);

            return Ok();
        }
    }
}
