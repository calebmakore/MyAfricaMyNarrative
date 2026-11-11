using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Web.Services;

/// <summary>Everyday story kinds offered to creators. Africa's stories are about far more than one subject.</summary>
public static class StoryTypes
{
    public static readonly IReadOnlyList<(StoryType Type, string Label)> All =
    [
        (StoryType.Creativity, "Craft & creativity"), (StoryType.Family, "Family & heritage"), (StoryType.Community, "Community life"),
        (StoryType.Business, "Business & hustle"), (StoryType.Humour, "Humour"), (StoryType.Love, "Love"), (StoryType.Sport, "Sport"),
        (StoryType.Faith, "Faith & ritual"), (StoryType.History, "History & memory"), (StoryType.Other, "Something else")
    ];
    public static string Label(StoryType type) => All.FirstOrDefault(x => x.Type == type).Label ?? "Not chosen yet";
}
