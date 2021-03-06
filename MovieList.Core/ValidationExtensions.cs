using System;
using System.Linq.Expressions;

using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace MovieList
{
    public static class ValidationExtensions
    {
        public static bool IsUrl(this string? str)
            => String.IsNullOrEmpty(str) ||
                str.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                str.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                str.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);

        public static ValidationHelper ValidationRule<TViewModel>(
            this TViewModel viewModel,
            Expression<Func<TViewModel, string>> viewModelProperty,
            int minValue,
            int maxValue,
            string propertyName)
            where TViewModel : ReactiveObject, IValidatableViewModel
            => viewModel.ValidationRule(
                viewModelProperty,
                value => !String.IsNullOrWhiteSpace(value) &&
                        Int32.TryParse(value, out int number) &&
                        number >= minValue &&
                        number <= maxValue,
                value => String.IsNullOrWhiteSpace(value) ? $"{propertyName}Empty" : $"{propertyName}Invalid");
    }
}
