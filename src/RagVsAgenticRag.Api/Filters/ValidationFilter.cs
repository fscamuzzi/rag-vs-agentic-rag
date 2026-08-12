using System.ComponentModel.DataAnnotations;

namespace RagVsAgenticRag.Api.Filters;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return Results.BadRequest($"Missing request body of type {typeof(T).Name}.");

        var validationContext = new ValidationContext(argument);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(argument, validationContext, results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty),
                    (r, member) => new { member, r.ErrorMessage })
                .GroupBy(x => x.member)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "Invalid value.").ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
