using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

public class DebtBotControllerBase : ControllerBase
{
	protected Guid? UserId
	{
		get
		{
			var identifier = User?.FindFirstValue(ClaimTypes.NameIdentifier);

			if (identifier is null)
			{
				return null;
			}

			return new Guid(identifier);
		}
	}
}