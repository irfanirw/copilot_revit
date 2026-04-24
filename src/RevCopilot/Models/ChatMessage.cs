namespace RevCopilot.Models;

public enum MessageRole { User, Assistant, System }

public class ChatMessage
{
    public MessageRole Role { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }

    public ChatMessage(MessageRole role, string content)
    {
        Role = role;
        Content = content;
        Timestamp = DateTime.Now;
    }
}
