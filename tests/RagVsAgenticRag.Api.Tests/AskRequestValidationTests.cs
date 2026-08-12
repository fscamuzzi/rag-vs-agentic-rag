using System.ComponentModel.DataAnnotations;
using RagVsAgenticRag.Api.Models;
using Xunit;

namespace RagVsAgenticRag.Api.Tests;

public class AskRequestValidationTests
{
    /// <summary>
    /// Verifies that retrieval sizes outside the supported range are rejected before reaching Qdrant.
    /// </summary>
    /// <param name="topK">The invalid retrieval size submitted by an API consumer.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(51)]
    public void Validate_RejectsOutOfRangeTopK(int topK)
    {
        // Exercise the same DataAnnotations validation used by the endpoint filter.
        var request = new AskRequest("return policy", topK);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        // The request must fail specifically on TopK so invalid values never reach Qdrant.
        Assert.False(isValid);
        Assert.Contains(validationResults,
            result => result.MemberNames.Contains(nameof(AskRequest.TopK)));
    }
}
