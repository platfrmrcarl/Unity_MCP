using Newtonsoft.Json.Linq;

namespace AIGameBuilder
{
    public static class StreamJsonParser
    {
        public static StreamEvent ParseLine(string jsonLine)
        {
            if (string.IsNullOrWhiteSpace(jsonLine)) return StreamEvent.Unknown;

            JObject root;
            try { root = JObject.Parse(jsonLine); }
            catch { return StreamEvent.Unknown; }

            var type = (string)root["type"];
            switch (type)
            {
                case "system":
                    return new StreamEvent { Kind = StreamEventKind.Init };
                case "result":
                    return new StreamEvent { Kind = StreamEventKind.Result };
                case "assistant":
                    return ParseAssistant(root);
                default:
                    return StreamEvent.Unknown;
            }
        }

        private static StreamEvent ParseAssistant(JObject root)
        {
            var content = root["message"]?["content"] as JArray;
            if (content == null) return StreamEvent.Unknown;

            foreach (var block in content)
            {
                var blockType = (string)block["type"];
                if (blockType == "tool_use")
                {
                    return new StreamEvent
                    {
                        Kind = StreamEventKind.ToolUse,
                        ToolName = (string)block["name"]
                    };
                }
                if (blockType == "text")
                {
                    return new StreamEvent
                    {
                        Kind = StreamEventKind.AssistantText,
                        Text = (string)block["text"]
                    };
                }
            }
            return StreamEvent.Unknown;
        }
    }
}
