namespace Core.Constants;

/// <summary>
/// Standard System Message descriptor per SRS specification (Section 5.3 Message Dictionary)
/// </summary>
public record SystemMessage(
    string Code,
    string MessageType,
    string Context,
    string Content
);
