using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AIGameBuilder
{
    public static class StreamJsonParser
    {
        public static List<StreamEvent> ParseLine(string jsonLine)
        {
            if (string.IsNullOrWhiteSpace(jsonLine)) return SingleUnknown();

            JObject root;
            try { root = JObject.Parse(jsonLine); }
            catch { return SingleUnknown(); }

            var type = (string)root["type"];
            switch (type)
            {
                case "system":
                    return new List<StreamEvent> { new StreamEvent { Kind = StreamEventKind.Init } };
                case "result":
                    return new List<StreamEvent> { new StreamEvent { Kind = StreamEventKind.Result } };
                case "assistant":
                    return ParseAssistant(root);
                default:
                    return SingleUnknown();
            }
        }

        private static List<StreamEvent> ParseAssistant(JObject root)
        {
            var content = root["message"]?["content"] as JArray;
            if (content == null) return SingleUnknown();

            var events = new List<StreamEvent>();
            foreach (var block in content)
            {
                var blockType = (string)block["type"];
                if (blockType == "text")
                {
                    events.Add(new StreamEvent
                    {
                        Kind = StreamEventKind.AssistantText,
                        Text = (string)block["text"]
                    });
                }
                else if (blockType == "tool_use")
                {
                    events.Add(new StreamEvent
                    {
                        Kind = StreamEventKind.ToolUse,
                        ToolName = (string)block["name"]
                    });
                }
            }

            if (events.Count == 0) return SingleUnknown();
            return events;
        }

        private static List<StreamEvent> SingleUnknown()
        {
            return new List<StreamEvent> { StreamEvent.Unknown };
        }
    }
}
