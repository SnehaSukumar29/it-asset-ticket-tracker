using AssetTracker.Core.Services;

namespace AssetTracker.Cli;

/// <summary>Minimal parser: positional words, "--name value" options and "--flag" switches.</summary>
public sealed class CommandArgs
{
    public List<string> Positionals { get; } = new();
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public CommandArgs(IEnumerable<string> args)
    {
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var token = list[i];
            if (token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2)
            {
                var name = token[2..];
                string? value = null;
                if (i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal))
                    value = list[++i];
                _options[name] = value;
            }
            else
            {
                Positionals.Add(token);
            }
        }
    }

    public string? Get(string name) => _options.TryGetValue(name, out var v) ? v : null;

    public bool Has(string name) => _options.ContainsKey(name);

    public string Require(string name) =>
        Get(name) ?? throw new TrackerException($"Missing required option --{name}.");

    public string Positional(int index, string description) =>
        index < Positionals.Count ? Positionals[index] : throw new TrackerException($"Missing {description}.");
}
