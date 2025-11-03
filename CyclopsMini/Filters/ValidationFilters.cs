using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CyclopsMini.Filters
{
    // Single DTO validator for Minimal APIs
    public class ValidationFilter<T> : IEndpointFilter where T : class
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var dto = context.Arguments.OfType<T>().FirstOrDefault();
            if (dto is null) return await next(context);

            var results = new List<ValidationResult>();
            var vc = new ValidationContext(dto, serviceProvider: null, items: null);
            var isValid = Validator.TryValidateObject(dto, vc, results, validateAllProperties: true);
            if (!isValid)
            {
                var errors = results
                    .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? string.Empty).ToArray());
                return Results.ValidationProblem(errors);
            }

            return await next(context);
        }
    }

    // List validator (e.g., List<SaleDto>)
    public class ValidationListFilter<T> : IEndpointFilter where T : class
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var list = context.Arguments.OfType<IEnumerable<T>>().FirstOrDefault();
            if (list is null) return await next(context);

            var allErrors = new Dictionary<string, string[]>();
            int index = 0;
            foreach (var item in list)
            {
                var results = new List<ValidationResult>();
                var vc = new ValidationContext(item, serviceProvider: null, items: null);
                var isValid = Validator.TryValidateObject(item, vc, results, validateAllProperties: true);
                if (!isValid)
                {
                    foreach (var r in results)
                    {
                        var member = r.MemberNames.FirstOrDefault() ?? string.Empty;
                        var key = $"[{index}].{member}";
                        if (!allErrors.TryGetValue(key, out var arr))
                        {
                            allErrors[key] = new[] { r.ErrorMessage ?? string.Empty };
                        }
                        else
                        {
                            allErrors[key] = arr.Concat(new[] { r.ErrorMessage ?? string.Empty }).ToArray();
                        }
                    }
                }
                index++;
            }

            if (allErrors.Any())
                return Results.ValidationProblem(allErrors);

            return await next(context);
        }
    }
}
