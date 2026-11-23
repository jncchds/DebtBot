using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DebtBot.Crutch;

public class RawTextRequestOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        RawTextRequestAttribute? rawTextRequestAttribute = context.MethodInfo.GetCustomAttributes(true)
           .SingleOrDefault((attribute) => attribute is RawTextRequestAttribute) as RawTextRequestAttribute;
        if (rawTextRequestAttribute != null)
        {
            var requestBody = new OpenApiRequestBody { Content = new Dictionary<string, IOpenApiMediaType>() };
            operation.RequestBody = requestBody;
            requestBody.Content.Add(rawTextRequestAttribute.MediaType, new OpenApiMediaType()
            {
                Schema = new OpenApiSchema()
                {
                    Type = JsonSchemaType.String
                }
            });
        }
    }
}
