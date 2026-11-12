using System.Globalization;

namespace EnterpriseUI.Editors.Interactivity;

public enum ChatMessageStatus { Sending, Sent, Delivered, Read, Failed }

public sealed class ChatAuthor
{
    public ChatAuthor(string id, string name, bool isLocal = false, int? accentArgb = null)
    {
        Id = id;
        Name = name;
        IsLocal = isLocal;
        AccentArgb = accentArgb;
    }

    public string Id { get; }
    public string Name { get; set; }
    public bool IsLocal { get; }

    /// <summary>Avatar color as ARGB; null picks a stable color from the id.</summary>
    public int? AccentArgb { get; set; }

    public string Initials
    {
        get
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }
}

public sealed class ChatAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string? Path { get; set; }
    public long SizeBytes { get; set; }

    public string SizeText
    {
        get
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = SizeBytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return unit == 0 ? $"{size.ToString("0", CultureInfo.InvariantCulture)} {units[unit]}" : $"{size.ToString("0.#", CultureInfo.InvariantCulture)} {units[unit]}";
        }
    }

    public bool IsImage => System.IO.Path.GetExtension(FileName).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp";
}

public sealed class ChatMessage
{
    public Guid Id { get; } = Guid.NewGuid();
    public ChatAuthor Author { get; init; } = new("system", "System");
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public ChatMessageStatus Status { get; set; } = ChatMessageStatus.Sent;
    public List<ChatAttachment> Attachments { get; } = new();

    /// <summary>Chips offered under the message; shown only while this is the latest message.</summary>
    public List<string> QuickReplies { get; } = new();

    /// <summary>Centered informational line (joined, date changed, and so on).</summary>
    public bool IsSystem { get; init; }
    public object? Tag { get; set; }
}

public sealed class ChatMessageEventArgs : EventArgs
{
    public ChatMessageEventArgs(ChatMessage message) => Message = message;
    public ChatMessage Message { get; }
}

public sealed class ChatConversation
{
    private readonly List<ChatMessage> _messages = new();
    private readonly List<ChatAuthor> _typing = new();

    public ChatConversation(ChatAuthor local) => LocalAuthor = local;

    public ChatAuthor LocalAuthor { get; }
    public IReadOnlyList<ChatMessage> Messages => _messages;
    public IReadOnlyList<ChatAuthor> TypingAuthors => _typing;
    public int MaxMessages { get; set; } = 5000;

    public event EventHandler<ChatMessageEventArgs>? MessageAdded;
    public event EventHandler<ChatMessageEventArgs>? MessageChanged;
    public event EventHandler? TypingChanged;
    public event EventHandler? Cleared;

    public ChatMessage Add(ChatMessage message)
    {
        _messages.Add(message);
        while (_messages.Count > Math.Max(1, MaxMessages)) _messages.RemoveAt(0);
        if (!message.Author.IsLocal) SetTyping(message.Author, false);
        MessageAdded?.Invoke(this, new ChatMessageEventArgs(message));
        return message;
    }

    public ChatMessage Send(string text, IEnumerable<ChatAttachment>? attachments = null)
    {
        var message = new ChatMessage { Author = LocalAuthor, Text = text.Trim(), Status = ChatMessageStatus.Sending };
        if (attachments != null) message.Attachments.AddRange(attachments);
        return Add(message);
    }

    public ChatMessage Receive(ChatAuthor author, string text, IEnumerable<string>? quickReplies = null)
    {
        var message = new ChatMessage { Author = author, Text = text };
        if (quickReplies != null) message.QuickReplies.AddRange(quickReplies);
        return Add(message);
    }

    public ChatMessage AddSystem(string text) => Add(new ChatMessage { Author = new ChatAuthor("system", "System"), Text = text, IsSystem = true });

    public void SetStatus(ChatMessage message, ChatMessageStatus status)
    {
        if (message.Status == status) return;
        message.Status = status;
        MessageChanged?.Invoke(this, new ChatMessageEventArgs(message));
    }

    public void SetTyping(ChatAuthor author, bool typing)
    {
        var changed = typing ? !_typing.Contains(author) && AddTyping(author) : _typing.Remove(author);
        if (changed) TypingChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool AddTyping(ChatAuthor author)
    {
        _typing.Add(author);
        return true;
    }

    public void Clear()
    {
        _messages.Clear();
        _typing.Clear();
        Cleared?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>How a message is presented relative to its neighbors.</summary>
public readonly record struct ChatMessageLayoutInfo(bool ShowDateSeparator, bool StartsGroup, bool EndsGroup);

public static class ChatGrouping
{
    public static readonly TimeSpan GroupWindow = TimeSpan.FromMinutes(5);

    public static ChatMessageLayoutInfo Describe(IReadOnlyList<ChatMessage> messages, int index)
    {
        var current = messages[index];
        var previous = index > 0 ? messages[index - 1] : null;
        var next = index < messages.Count - 1 ? messages[index + 1] : null;
        var dateBreak = previous == null || previous.Timestamp.Date != current.Timestamp.Date;
        return new ChatMessageLayoutInfo(dateBreak, dateBreak || !SameRun(previous!, current), next == null || !SameRun(current, next) || next.Timestamp.Date != current.Timestamp.Date);
    }

    private static bool SameRun(ChatMessage a, ChatMessage b) =>
        !a.IsSystem && !b.IsSystem && a.Author.Id == b.Author.Id && (b.Timestamp - a.Timestamp).Duration() <= GroupWindow;

    public static string DateLabel(DateTimeOffset value, DateTimeOffset now)
    {
        if (value.Date == now.Date) return "Today";
        if (value.Date == now.Date.AddDays(-1)) return "Yesterday";
        return value.Year == now.Year ? value.ToString("dddd, MMMM d") : value.ToString("MMMM d, yyyy");
    }
}
