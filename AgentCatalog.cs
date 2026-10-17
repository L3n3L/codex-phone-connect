namespace CodexPhoneConnect;

public enum AgentId
{
    Gpt,
    Claude,
}

public sealed record AgentDefinition(
    AgentId Id,
    string WireId,
    string DisplayName,
    string ProductName,
    IReadOnlyList<string> ProcessNames,
    string IconSvgAsset,
    string IconPngAsset)
{
    public bool MatchesProcess(string processName)
    {
        return ProcessNames.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
    }
}

public static class AgentCatalog
{
    private static readonly IReadOnlyDictionary<AgentId, AgentDefinition> Definitions =
        new Dictionary<AgentId, AgentDefinition>
        {
            [AgentId.Gpt] = new(
                AgentId.Gpt,
                "gpt",
                "GPT",
                "Codex / ChatGPT Desktop",
                new[] { "ChatGPT", "Codex" },
                "openai-symbol.svg",
                "openai-symbol.png"),
            [AgentId.Claude] = new(
                AgentId.Claude,
                "claude",
                "Claude",
                "Claude Desktop",
                new[] { "Claude" },
                "claude.svg",
                "claude.png"),
        };

    public static IReadOnlyList<AgentDefinition> All { get; } =
        new[] { Definitions[AgentId.Gpt], Definitions[AgentId.Claude] };

    public static AgentDefinition Get(AgentId id)
    {
        return Definitions.TryGetValue(id, out var definition)
            ? definition
            : Definitions[AgentId.Gpt];
    }

    public static string ToWireValue(AgentId id) => Get(id).WireId;

    public static bool TryParse(string? value, out AgentId agent)
    {
        if (string.Equals(value, "gpt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "codex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "chatgpt", StringComparison.OrdinalIgnoreCase))
        {
            agent = AgentId.Gpt;
            return true;
        }

        if (string.Equals(value, "claude", StringComparison.OrdinalIgnoreCase))
        {
            agent = AgentId.Claude;
            return true;
        }

        agent = default;
        return false;
    }
}
