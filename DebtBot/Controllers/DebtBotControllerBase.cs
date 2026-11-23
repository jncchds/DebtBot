using System.Security.Claims;
using DebtBot.Identity;
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

	protected IActionResult FromResult<T>(BillActionResult<T> result, Func<T, IActionResult> onOk) => result.Status switch
	{
		BillActionStatus.Ok => onOk(result.Value!),
		BillActionStatus.NotFound => ErrorResult(StatusCodes.Status404NotFound, "Not found."),
		BillActionStatus.Forbidden => ErrorResult(StatusCodes.Status403Forbidden, "Only the creator or an admin can do this."),
		_ => ErrorResult(StatusCodes.Status400BadRequest, result.Errors.ToArray()),
	};
}