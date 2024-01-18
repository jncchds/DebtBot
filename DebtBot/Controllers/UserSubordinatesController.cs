using DebtBot.Models;
using DebtBot.ServiceInterfaces;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class UserSubordinatesController : ControllerBase
{
    private readonly IUserSubordinateService _userSubordinatesService;

    public UserSubordinatesController(IUserSubordinateService userSubordinatesService)
    {
        _userSubordinatesService = userSubordinatesService;
    }

    [HttpGet()]
    public IActionResult Get()
    {
        return Ok(_userSubordinatesService.Get());
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var subordinates = _userSubordinatesService.Get(id);
        if (subordinates?.Any() ?? false)
        {
            return Ok(subordinates);
        }

        return NotFound();
    }

    [HttpPost("{id}")]
    public IActionResult Post(Guid id, UserModel subordinate)
    {
        _userSubordinatesService.AddSubordinate(id, subordinate);

        return Ok();
    }
}