using DebtBot.Models;
using DebtBot.ServiceInterfaces;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class UserContactsController : ControllerBase
{
    private readonly IUserContactService _userContactService;

    public UserContactsController(IUserContactService userContactService)
    {
        _userContactService = userContactService;
    }

    [HttpGet()]
    public IActionResult Get()
    {
        return Ok(_userContactService.Get());
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var contactLinks = _userContactService.Get(id);
        if (contactLinks?.Any() ?? false)
        {
            return Ok(contactLinks);
        }

        return NotFound();
    }

    [HttpPost("{id}")]
    public IActionResult Post(Guid id, UserModel contact)
    {
        _userContactService.AddContact(id, contact);

        return Ok();
    }
}