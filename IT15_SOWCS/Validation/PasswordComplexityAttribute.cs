using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT15_SOWCS.Validation
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PasswordComplexityAttribute : ValidationAttribute, IClientModelValidator
    {
        private static readonly string[] AllRequirements =
        {
            "at least 12 characters",
            "an uppercase letter",
            "a lowercase letter",
            "a number",
            "a special character"
        };

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var password = value as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(password))
            {
                return ValidationResult.Success;
            }

            var unmetRequirements = GetUnmetRequirements(password);
            return unmetRequirements.Count == 0
                ? ValidationResult.Success
                : new ValidationResult(BuildErrorMessage(unmetRequirements));
        }

        public void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-passwordcomplexity", BuildErrorMessage(AllRequirements));
        }

        public static IReadOnlyList<string> GetUnmetRequirements(string? password)
        {
            var value = password ?? string.Empty;
            var unmetRequirements = new List<string>();

            if (value.Length < 12)
            {
                unmetRequirements.Add(AllRequirements[0]);
            }

            if (!value.Any(char.IsUpper))
            {
                unmetRequirements.Add(AllRequirements[1]);
            }

            if (!value.Any(char.IsLower))
            {
                unmetRequirements.Add(AllRequirements[2]);
            }

            if (!value.Any(char.IsDigit))
            {
                unmetRequirements.Add(AllRequirements[3]);
            }

            if (!value.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                unmetRequirements.Add(AllRequirements[4]);
            }

            return unmetRequirements;
        }

        public static string BuildErrorMessage(IEnumerable<string> unmetRequirements)
        {
            var items = unmetRequirements
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            if (items.Count == 0)
            {
                return string.Empty;
            }

            if (items.Count == 1)
            {
                return $"Password must contain {items[0]}.";
            }

            if (items.Count == 2)
            {
                return $"Password must contain {items[0]} and {items[1]}.";
            }

            return $"Password must contain {string.Join(", ", items.Take(items.Count - 1))}, and {items[^1]}.";
        }

        private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
        {
            if (attributes.ContainsKey(key))
            {
                return;
            }

            attributes.Add(key, value);
        }
    }
}
