using System.Security.Claims;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Services;
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

	protected bool IsAdmin => User?.Claims.Any(c => c.Type == IdentityData.AdminUserClaimName) ?? false;

	/// <summary>Errors go out as <c>{ message, errors }</c>, with <c>message</c> joining the errors.</summary>
	protected IActionResult ErrorResult(int statusCode, params string[] errors) =>
		StatusCode(statusCode, new { message = string.Join("; ", errors), errors });

	/// <summary>
	/// Whose list an <c>ofUserId</c> query asks for: yourself when it's left out or is you, or a user you have a
	/// confirmed subscription to. Anything else sets <paramref name="denied"/> to a 403. Admins aren't special here.
	/// </summary>
	protected Guid ListOwner(IUserContactService contacts, Guid? ofUserId, out IActionResult? denied)
	{
		denied = null;
		var me = UserId!.Value;
		if (ofUserId is null || ofUserId == me)
		{
			return me;
		}

		if (!contacts.HasConfirmedSubscription(me, ofUserId.Value))
		{
			denied = ErrorResult(StatusCodes.Status403Forbidden, "You can only see the lists of people whose bills and debts you're subscribed to.");
		}

		return ofUserId.Value;
	}

	protected IActionResult FromResult<T>(BillActionResult<T> result, Func<T, IActionResult> onOk) => result.Status switch
	{
		BillActionStatus.Ok => onOk(result.Value!),
		BillActionStatus.NotFound => ErrorResult(StatusCodes.Status404NotFound, "Not found."),
		BillActionStatus.Forbidden => ErrorResult(StatusCodes.Status403Forbidden, "Only the creator or an admin can do this."),
		BillActionStatus.Conflict => ErrorResult(StatusCodes.Status409Conflict, result.Errors.ToArray()),
		_ => ErrorResult(StatusCodes.Status400BadRequest, result.Errors.ToArray()),
	};
}