﻿using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DebtBot.Crutch;

public class RawTextRequestOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        RawTextRequestAttribute rawTextRequestAttribute = context.MethodInfo.GetCustomAttributes(true)
           .SingleOrDefault((attribute) => attribute is RawTextRequestAttribute) as RawTextRequestAttribute;
        if (rawTextRequestAttribute != null)
        {
            operation.RequestBody = new OpenApiRequestBody();
            operation.RequestBody.Content.Add(rawTextRequestAttribute.MediaType, new OpenApiMediaType()
            {
                Schema = new OpenApiSchema()
                {
                    Type = "string"
                }
            });
        }
    }
}
