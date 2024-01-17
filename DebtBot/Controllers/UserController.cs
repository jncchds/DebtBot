using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.ServiceInterfaces;
using DebtBot.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class UserController : Controller
    {
        private IUserService userService;

        public UserController(IUserService userService)
        {
            this.userService = userService;
        }

        [HttpGet("{id}")]
        public IActionResult Get(Guid id)
        {
            var user = userService.GetUserById(id);

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpGet()]
        public IActionResult Get()
        {
            var users = userService.GetUsers();

            if (users?.Any() ?? false)
                return Ok(users);

            return NotFound();
        }

        [HttpPost()]
        public ActionResult Post(UserModel user)
        {
            userService.AddUser(user);

            return Ok();
        }

        [HttpDelete("{id}")]
        public ActionResult Delete(Guid id)
        {
            userService.DeleteUser(id);

            return Ok();
        }
    }
}
