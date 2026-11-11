namespace MyAfricaMyNarrative.Application;

/// <summary>
/// A failed call to the AI service, explained in plain words for the storyteller. It is an InvalidOperationException so
/// existing safe-failure handling (record the failed attempt, change nothing) keeps working.
/// </summary>
public sealed class AiServiceException(string message, int? statusCode = null) : InvalidOperationException(message)
{
    public int? StatusCode { get; } = statusCode;

    /// <param name="task">What was being asked for, e.g. "suggest the words".</param>
    public static AiServiceException FromStatus(string task, int status) => new(status switch
    {
        401 or 403 => $"The AI service refused the key, so it couldn’t {task}. The studio owner needs to check the AI key in settings. (HTTP {status})",
        429 => $"The AI service is out of credit or busy right now, so it couldn’t {task}. Try again later, or use the free option without AI. (HTTP 429)",
        >= 500 => $"The AI service had a problem on its side, so it couldn’t {task}. Try again in a few minutes. (HTTP {status})",
        _ => $"The AI service couldn’t {task}. (HTTP {status})"
    }, status);

    /// <summary>Plain words for any failure from an AI call: timeouts, lost connections and service errors.</summary>
    public static string Describe(Exception exception) => exception switch
    {
        AiServiceException ai => ai.Message,
        TaskCanceledException => "The AI service took too long to answer. Try again, or use the free option without AI.",
        HttpRequestException => "The studio couldn’t reach the AI service. Check the internet connection and try again.",
        System.Text.Json.JsonException => "The AI service sent back something the studio couldn’t read, so it was ignored.",
        _ => exception.Message
    };
}
