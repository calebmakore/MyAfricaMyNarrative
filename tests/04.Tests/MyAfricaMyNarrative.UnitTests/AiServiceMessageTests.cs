using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>When the AI service fails, the storyteller gets plain words and a way forward, never a bare status code.</summary>
public sealed class AiServiceMessageTests
{
    [Theory]
    [InlineData(429, "out of credit or busy")]
    [InlineData(401, "refused the key")]
    [InlineData(503, "problem on its side")]
    [InlineData(400, "couldn’t suggest the words")]
    public void Status_codes_become_plain_advice_that_still_names_the_code(int status, string expected)
    {
        var exception = AiServiceException.FromStatus("suggest the words", status);
        Assert.Contains(expected, exception.Message);
        Assert.Contains($"HTTP {status}", exception.Message);
        Assert.IsAssignableFrom<InvalidOperationException>(exception);   // existing safe-failure handling still applies
    }

    [Fact]
    public void Timeouts_and_lost_connections_are_described_without_jargon()
    {
        Assert.Contains("took too long", AiServiceException.Describe(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")));
        Assert.Contains("couldn’t reach", AiServiceException.Describe(new HttpRequestException("No such host is known")));
        Assert.Equal("Live AI mode is not enabled.", AiServiceException.Describe(new InvalidOperationException("Live AI mode is not enabled.")));
    }
}
