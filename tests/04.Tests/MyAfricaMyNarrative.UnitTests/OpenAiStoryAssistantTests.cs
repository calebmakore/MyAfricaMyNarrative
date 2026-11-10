using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Infrastructure.Ai;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class OpenAiStoryAssistantTests
{
    [Fact]
    public async Task Sends_bounded_metadata_only_and_reads_structured_order()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        string? sent = null;
        var handler = new StubHandler(async request =>
        {
            sent = await request.Content!.ReadAsStringAsync();
            var proposal = $$"""{"scene_order":["{{second}}","{{first}}"],"summary":"A clearer opening."}""";
            var response = $$"""{"output":[{"content":[{"type":"output_text","text":{{System.Text.Json.JsonSerializer.Serialize(proposal)}}}]}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        });
        var assistant = Create(handler);

        var result = await assistant.ProposeOrderAsync(new StoryAssistantRequest(
            [new(first, 1, "Opening"), new(second, 2, "Home")],
            "Youth", "Belonging", "Warm", "Harare", "mbuya"), CancellationToken.None);

        Assert.Equal([second, first], result.SceneOrder);
        Assert.Equal("OpenAI", result.Provider);
        Assert.Contains("max_output_tokens\u0022:700", sent);
        Assert.DoesNotContain("transcript", sent, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    [Fact]
    public async Task Rejects_unbounded_scene_count_before_network_call()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Network must not be called."));
        var assistant = Create(handler);
        var request = new StoryAssistantRequest([new(Guid.NewGuid(), 1, "Only")], "A", "M", "T", "L", "P");

        await Assert.ThrowsAsync<InvalidOperationException>(() => assistant.ProposeOrderAsync(request, CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Provider_error_does_not_expose_response_body()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("secret provider diagnostic")
        }));
        var assistant = Create(handler);
        var request = new StoryAssistantRequest(
            [new(Guid.NewGuid(), 1, "One"), new(Guid.NewGuid(), 2, "Two")], "A", "M", "T", "L", "P");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => assistant.ProposeOrderAsync(request, CancellationToken.None));
        Assert.Contains("HTTP 400", error.Message);
        Assert.DoesNotContain("secret provider diagnostic", error.Message);
    }

    private static OpenAiStoryAssistant Create(StubHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        return new OpenAiStoryAssistant(client, Options.Create(new AiOptions
        {
            Mode = "Live", ApiKey = "test-key", StoryModel = "test-model", RequestTimeoutSeconds = 5
        }));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? AuthorizationScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return response(request);
        }
    }
}
