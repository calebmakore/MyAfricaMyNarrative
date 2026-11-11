using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Infrastructure.Ai;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class OpenAiAudioTranscriberTests
{
    [Fact]
    public async Task ReturnsTextAsUnreviewedProviderResult()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.IsType<MultipartFormDataContent>(request.Content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"text\":\"Mhoro — Africa\",\"languages\":[{\"code\":\"sn\"}]}", Encoding.UTF8, "application/json") });
        });
        var service = Create(handler);

        var result = await service.TranscribeAsync(new MemoryStream([1, 2, 3]), "voice.weba", "audio/webm", CancellationToken.None);

        Assert.Equal("Mhoro — Africa", result.Text);
        Assert.Equal(["sn"], result.Languages);
        Assert.Equal("gpt-transcribe", result.Model);
    }

    [Fact]
    public async Task FailureDoesNotExposeProviderBodyOrSecret()
    {
        var service = Create(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        { Content = new StringContent("secret-provider-detail") })));

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.TranscribeAsync(
            new MemoryStream([1]), "voice.weba", "audio/webm", CancellationToken.None));

        Assert.Contains("HTTP 400", error.Message);
        Assert.DoesNotContain("secret-provider-detail", error.Message);
        Assert.DoesNotContain("test-key", error.Message);
    }

    private static OpenAiAudioTranscriber Create(HttpMessageHandler handler) => new(new HttpClient(handler)
    { BaseAddress = new Uri("https://api.openai.com/"), Timeout = TimeSpan.FromSeconds(2) }, Options.Create(new AiOptions
    { Mode = "Live", ApiKey = "test-key", TranscriptionModel = "gpt-transcribe" }));

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request); }
}
