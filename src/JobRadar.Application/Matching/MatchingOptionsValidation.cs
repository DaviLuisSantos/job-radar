using Microsoft.Extensions.Options;

namespace JobRadar.Application.Matching;

/// <summary>
/// Builds every market's rules at start: the site and the worker refuse to start with rules that would not score,
/// instead of failing at the first listing.
/// </summary>
internal sealed class MatchingOptionsValidation : IValidateOptions<MatchingOptions>
{
    public ValidateOptionsResult Validate(string? name, MatchingOptions options)
    {
        try
        {
            ProfileCatalog.BuildMarkets(options);
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
