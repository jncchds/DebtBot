using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DebtBot.Crutch;

/// <summary>For Swagger: the action reads a multipart form with one file field, <see cref="FieldName"/>, itself.</summary>
[AttributeUsage(AttributeTargets.Method)]
public class ImageUploadRequestAttribute : Attribute
{
    public string FieldName { get; set; } = "image";
}

public class ImageUploadRequestOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttributes(true).OfType<ImageUploadRequestAttribute>().FirstOrDefault() is not { } upload)
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Required = new HashSet<string> { upload.FieldName },
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            [upload.FieldName] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                        },
                    },
                },
            },
        };
    }
}
