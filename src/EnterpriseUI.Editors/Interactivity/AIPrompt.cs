using System.Text;
using System.Text.RegularExpressions;

namespace EnterpriseUI.Editors.Interactivity;

public enum AIPromptState { Idle, Loading, Completed, Faulted, Cancelled }

/// <summary>Estimated or reported token counts of one exchange.</summary>
public readonly record struct AIPromptTokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>Rough estimate (4 characters per token) for providers that do not report usage.</summary>
    public static int Estimate(string? text) => string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    public static AIPromptTokenUsage operator +(AIPromptTokenUsage a, AIPromptTokenUsage b) => new(a.PromptTokens + b.PromptTokens, a.CompletionTokens + b.CompletionTokens);
}

/// <summary>A reusable action template; <c>{input}</c> and <c>{context}</c> are replaced when the prompt is built.</summary>
public sealed record AIPromptCommand(string Id, string Title, string Template, string? Description = null, string? Glyph = null, string? Category = null)
{
    public string BuildPrompt(string input, string? context = null)
    {
        var template = Template.Contains("{input}", StringComparison.Ordinal) ? Template : Template + "\n\n{input}";
        return template.Replace("{input}", input, StringComparison.Ordinal).Replace("{context}", context ?? string.Empty, StringComparison.Ordinal);
    }
}

public sealed record AIPromptRequest(string Prompt, string Input, string? CommandId, string? Context, IReadOnlyList<AIPromptHistoryEntry> History);

public sealed record AIPromptResponse(string Text, AIPromptTokenUsage? Usage = null, string? Model = null);

/// <summary>Source of AI responses; <paramref name="partial"/> receives the text produced so far when the provider streams.</summary>
public interface IAIPromptProvider
{
    Task<AIPromptResponse> GetResponseAsync(AIPromptRequest request, IProgress<string>? partial, CancellationToken cancellationToken);
}

public sealed class AIPromptHistoryEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Prompt { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public string? CommandId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public AIPromptState State { get; internal set; } = AIPromptState.Loading;
    public string? Response { get; internal set; }
    public string? Error { get; internal set; }
    public AIPromptTokenUsage Usage { get; internal set; }
    public TimeSpan Duration { get; internal set; }

    /// <summary>-1 not helpful, 0 unrated, 1 helpful.</summary>
    public int Rating { get; internal set; }
}

public sealed class AIPromptCommandRegistry
{
    private readonly List<AIPromptCommand> _commands = new();

    public IReadOnlyList<AIPromptCommand> All => _commands;

    public event EventHandler? Changed;

    /// <summary>Adds the command or replaces the one with the same id.</summary>
    public void Register(AIPromptCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var index = _commands.FindIndex(c => string.Equals(c.Id, command.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _commands[index] = command; else _commands.Add(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Unregister(string id)
    {
        var removed = _commands.RemoveAll(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed) Changed?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    public AIPromptCommand? Find(string? id) => id == null ? null : _commands.Find(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Commands whose title, id or description contains the text (used by the "/" command suggestions).</summary>
    public IReadOnlyList<AIPromptCommand> Search(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return _commands.ToArray();
        return _commands.Where(c => c.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(text, StringComparison.OrdinalIgnoreCase)
            || (c.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)).ToArray();
    }
}

/// <summary>State machine behind the AI prompt control: history, loading state, commands, streaming output and token totals.</summary>
public sealed class AIPromptSession
{
    private readonly List<AIPromptHistoryEntry> _history = new();
    private CancellationTokenSource? _cts;
    private AIPromptState _state = AIPromptState.Idle;
    private string _output = string.Empty;

    public AIPromptSession(IAIPromptProvider? provider = null) => Provider = provider;

    public IAIPromptProvider? Provider { get; set; }
    public AIPromptCommandRegistry Commands { get; } = new();
    public IReadOnlyList<AIPromptHistoryEntry> History => _history;
    public int MaxHistory { get; set; } = 100;
    public string? Context { get; set; }
    public AIPromptTokenUsage TotalUsage { get; private set; }
    public AIPromptHistoryEntry? Current { get; private set; }

    public AIPromptState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Output
    {
        get => _output;
        private set
        {
            _output = value;
            OutputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? StateChanged;
    public event EventHandler? OutputChanged;
    public event EventHandler? HistoryChanged;

    public async Task<AIPromptHistoryEntry> SubmitAsync(string input, string? commandId = null)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Prompt text is required.", nameof(input));
        if (State == AIPromptState.Loading) throw new InvalidOperationException("A prompt is already running.");
        var command = Commands.Find(commandId);
        if (commandId != null && command == null) throw new ArgumentException($"Unknown command '{commandId}'.", nameof(commandId));

        var prompt = command?.BuildPrompt(input, Context) ?? input;
        var entry = new AIPromptHistoryEntry { Prompt = prompt, Input = input, CommandId = command?.Id };
        _history.Add(entry);
        while (_history.Count > Math.Max(1, MaxHistory)) _history.RemoveAt(0);
        Current = entry;
        Output = string.Empty;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        State = AIPromptState.Loading;
        HistoryChanged?.Invoke(this, EventArgs.Empty);

        var started = DateTimeOffset.Now;
        try
        {
            if (Provider == null) throw new InvalidOperationException("No AI provider is configured.");
            var progress = new Progress<string>(text =>
            {
                if (State == AIPromptState.Loading && ReferenceEquals(Current, entry)) Output = text;
            });
            var response = await Provider.GetResponseAsync(new AIPromptRequest(prompt, input, command?.Id, Context, _history.Where(h => h != entry).ToArray()), progress, _cts.Token).ConfigureAwait(true);
            entry.Response = response.Text;
            entry.Usage = response.Usage ?? new AIPromptTokenUsage(AIPromptTokenUsage.Estimate(prompt), AIPromptTokenUsage.Estimate(response.Text));
            entry.State = AIPromptState.Completed;
            TotalUsage += entry.Usage;
            Output = response.Text;
        }
        catch (OperationCanceledException)
        {
            entry.State = AIPromptState.Cancelled;
            entry.Response = Output;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            entry.State = AIPromptState.Faulted;
            entry.Error = ex.Message;
        }

        entry.Duration = DateTimeOffset.Now - started;
        State = entry.State;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    /// <summary>Runs the latest prompt again; the previous attempt stays in the history.</summary>
    public Task<AIPromptHistoryEntry> RetryAsync()
    {
        var last = _history.Count > 0 ? _history[^1] : throw new InvalidOperationException("There is nothing to retry.");
        return SubmitAsync(last.Input, last.CommandId);
    }

    public void Cancel()
    {
        if (State == AIPromptState.Loading) _cts?.Cancel();
    }

    public void Rate(Guid entryId, int rating)
    {
        var entry = _history.Find(h => h.Id == entryId);
        if (entry == null) return;
        entry.Rating = Math.Clamp(rating, -1, 1);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearHistory()
    {
        Cancel();
        _history.Clear();
        Current = null;
        Output = string.Empty;
        TotalUsage = default;
        State = AIPromptState.Idle;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Deterministic offline provider for demos, tests and design time; real providers implement <see cref="IAIPromptProvider"/>.</summary>
public sealed class SampleAIPromptProvider : IAIPromptProvider
{
    public const string Summarize = "summarize", Rewrite = "rewrite", Expand = "expand", Formal = "tone-formal", Friendly = "tone-friendly", Concise = "tone-concise", Confident = "tone-confident";

    private static readonly Regex SentenceSplit = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
    private static readonly Regex Words = new(@"[\p{L}\p{N}']+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase) { "the", "a", "an", "and", "or", "of", "to", "in", "on", "for", "is", "are", "was", "were", "it", "this", "that", "with", "as", "by", "at", "be" };

    private static readonly (string Pattern, string Replacement)[] Rewrites =
    {
        (@"\bin order to\b", "to"), (@"\ba lot of\b", "many"), (@"\butili[sz]e\b", "use"), (@"\bat this point in time\b", "now"),
        (@"\bdue to the fact that\b", "because"), (@"\bis able to\b", "can"), (@"\bin the event that\b", "if")
    };

    private static readonly (string Pattern, string Replacement)[] Contractions =
    {
        (@"\bdon't\b", "do not"), (@"\bcan't\b", "cannot"), (@"\bwon't\b", "will not"), (@"\bit's\b", "it is"), (@"\bwe're\b", "we are"),
        (@"\bI'm\b", "I am"), (@"\bisn't\b", "is not"), (@"\baren't\b", "are not"), (@"\bdidn't\b", "did not"), (@"\bthey're\b", "they are")
    };

    public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(350);

    /// <summary>Words per streamed update; 0 disables streaming.</summary>
    public int StreamWordsPerStep { get; set; } = 4;

    public async Task<AIPromptResponse> GetResponseAsync(AIPromptRequest request, IProgress<string>? partial, CancellationToken cancellationToken)
    {
        if (Latency > TimeSpan.Zero) await Task.Delay(Latency, cancellationToken).ConfigureAwait(false);
        var text = Transform(request.CommandId, request.Input);

        if (partial != null && StreamWordsPerStep > 0)
        {
            var parts = text.Split(' ');
            var builder = new StringBuilder();
            for (var i = 0; i < parts.Length; i += StreamWordsPerStep)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(string.Join(' ', parts.Skip(i).Take(StreamWordsPerStep))).Append(' ');
                partial.Report(builder.ToString().TrimEnd());
                await Task.Delay(Math.Min(60, (int)Latency.TotalMilliseconds / 4 + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        return new AIPromptResponse(text, new AIPromptTokenUsage(AIPromptTokenUsage.Estimate(request.Prompt), AIPromptTokenUsage.Estimate(text)), "sample-offline");
    }

    public static string Transform(string? commandId, string input) => commandId?.ToLowerInvariant() switch
    {
        Summarize => SummarizeText(input),
        Rewrite => RewriteText(input),
        Expand => ExpandText(input),
        Formal => Apply(RewriteText(input), Contractions),
        Friendly => MakeFriendly(input),
        Concise => Concise_(input),
        Confident => RemoveHedges(RewriteText(input)),
        _ => $"You asked: {input.Trim()}\n\nNo AI provider is connected; connect an IAIPromptProvider to get real answers."
    };

    private static string[] Sentences(string text) => SentenceSplit.Split(text.Trim()).Where(s => s.Length > 0).ToArray();

    private static string SummarizeText(string input)
    {
        var sentences = Sentences(input);
        if (sentences.Length <= 1) return input.Trim();
        var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Words.Matches(input))
        {
            if (m.Value.Length > 2 && !StopWords.Contains(m.Value)) frequency[m.Value] = frequency.GetValueOrDefault(m.Value) + 1;
        }

        var keep = Math.Max(1, (int)Math.Ceiling(sentences.Length / 3.0));
        var chosen = sentences.Select((s, i) => (s, i, score: Words.Matches(s).Sum(m => frequency.GetValueOrDefault(m.Value)) / Math.Sqrt(Math.Max(1, Words.Matches(s).Count))))
            .OrderByDescending(x => x.score).Take(keep).OrderBy(x => x.i).Select(x => x.s);
        return string.Join(' ', chosen);
    }

    private static string Apply(string text, (string Pattern, string Replacement)[] rules)
    {
        foreach (var (pattern, replacement) in rules) text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
        return text;
    }

    private static string RewriteText(string input)
    {
        var text = Regex.Replace(input.Trim(), @"\s+", " ");
        text = Apply(text, Rewrites);
        var sentences = Sentences(text).Select(s =>
        {
            s = char.ToUpperInvariant(s[0]) + s[1..];
            return char.IsPunctuation(s[^1]) ? s : s + ".";
        });
        return string.Join(' ', sentences);
    }

    private static string ExpandText(string input)
    {
        var builder = new StringBuilder("Expanded outline:");
        foreach (var sentence in Sentences(input)) builder.Append("\n- ").Append(sentence.Trim()).Append("\n  Consider adding: who is affected, why it matters, and a concrete example.");
        return builder.ToString();
    }

    private static string MakeFriendly(string input)
    {
        var text = RewriteText(input);
        text = Regex.Replace(text, @"\bdo not\b", "don't", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bcannot\b", "can't", RegexOptions.IgnoreCase);
        return "Hi there! " + text.TrimEnd('.', '!') + "!";
    }

    private static string Concise_(string input) =>
        Regex.Replace(RewriteText(input), @"\b(really|very|just|actually|basically|quite|simply)\s+", string.Empty, RegexOptions.IgnoreCase);

    private static string RemoveHedges(string text)
    {
        text = Regex.Replace(text, @"\b(I think|I believe|maybe|perhaps|kind of|sort of|probably)\b,?\s*", string.Empty, RegexOptions.IgnoreCase);
        return Regex.Replace(text, @"(^|(?<=[.!?]\s))([a-z])", m => m.Value.ToUpperInvariant());
    }

    /// <summary>The summarize, rewrite, expand and tone commands handled by this provider.</summary>
    public static IReadOnlyList<AIPromptCommand> DefaultCommands { get; } = new[]
    {
        new AIPromptCommand(Summarize, "Summarize", "Summarize the following text:\n\n{input}", "Shorten to the key sentences", "≡", "Transform"),
        new AIPromptCommand(Rewrite, "Rewrite", "Rewrite the following text so it reads clearly:\n\n{input}", "Improve clarity and grammar", "✎", "Transform"),
        new AIPromptCommand(Expand, "Expand", "Expand the following text with more detail:\n\n{input}", "Turn the text into an outline to elaborate", "⊕", "Transform"),
        new AIPromptCommand(Formal, "Tone: Formal", "Rewrite in a formal tone:\n\n{input}", "Professional wording", "◆", "Tone"),
        new AIPromptCommand(Friendly, "Tone: Friendly", "Rewrite in a friendly tone:\n\n{input}", "Warm and approachable", "☺", "Tone"),
        new AIPromptCommand(Concise, "Tone: Concise", "Rewrite concisely:\n\n{input}", "Cut filler words", "—", "Tone"),
        new AIPromptCommand(Confident, "Tone: Confident", "Rewrite in a confident tone:\n\n{input}", "Remove hedging", "▲", "Tone")
    };
}
