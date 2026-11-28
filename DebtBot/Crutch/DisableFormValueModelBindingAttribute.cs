using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DebtBot.Crutch;

/// <summary>
/// Stops MVC from reading a form body before the action runs, so the action reads it itself and answers a body over
/// the limit with its own error. Without it, a multipart body over <c>MultipartBodyLengthLimit</c> becomes a model
/// state error, and <c>[ApiController]</c> answers 400 before the action runs.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
