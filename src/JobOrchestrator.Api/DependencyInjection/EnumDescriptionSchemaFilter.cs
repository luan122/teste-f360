using System.ComponentModel;
using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace JobOrchestrator.Api.DependencyInjection;

public sealed class EnumDescriptionSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Description += "<p>Membros:</p><ul>";

            foreach (var enumName in Enum.GetNames(context.Type))
            {
                var memberInfo = context.Type.GetMember(enumName).FirstOrDefault();
                if (memberInfo != null)
                {
                    var descriptionAttribute = memberInfo.GetCustomAttribute<DescriptionAttribute>();
                    var description = descriptionAttribute?.Description ?? enumName;
                    var enumValue = Convert.ChangeType(Enum.Parse(context.Type, enumName), Enum.GetUnderlyingType(context.Type));

                    schema.Description += $"<li><i>{enumValue}</i> - {description}</li>";
                }
            }

            schema.Description += "</ul>";
        }
    }
}
