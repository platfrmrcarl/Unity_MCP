namespace AIGameBuilder
{
    public enum StreamEventKind { Init, AssistantText, ToolUse, Result, Unknown }

    public struct StreamEvent
    {
        public StreamEventKind Kind;
        public string Text;
        public string ToolName;

        public static StreamEvent Unknown => new StreamEvent { Kind = StreamEventKind.Unknown };
    }
}
