using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Infrastructure.Ai;

public sealed class OpenAiAudioTranscriber(HttpClient http, IOptions<AiOptions> configured) : IAudioTranscriber
{
    public async Task<AudioTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken cancellationToken)
    {
        var options = configured.Value;
        if (options.Mode != "Live") throw new InvalidOperationException("Live AI mode is not enabled.");
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.TranscriptionModel)) throw new InvalidOperationException("Live transcription is not configured.");
        if (!audio.CanRead) throw new InvalidOperationException("The source audio cannot be read.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(audio);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", Path.GetFileName(fileName));
        form.Add(new StringContent(options.TranscriptionModel), "model");
        form.Add(new StringContent("json"), "response_format");
        request.Content = form;
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw AiServiceException.FromStatus("suggest the words", (int)response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var text = document.RootElement.TryGetProperty("text", out var value) ? value.GetString()?.Trim() : null;
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("OpenAI returned no transcription text.");
        var languages = document.RootElement.TryGetProperty("languages", out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(x => x.TryGetProperty("code", out var code) ? code.GetString() : null).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray()
            : [];
        return new AudioTranscriptionResult(text, languages, "OpenAI", options.TranscriptionModel);
    }
}
