using NUnit.Framework;
using AIGameBuilder;

public class StreamJsonParserTests
{
    [Test]
    public void Init_line_parses_as_Init()
    {
        var e = StreamJsonParser.ParseLine("{\"type\":\"system\",\"subtype\":\"init\"}")[0];
        Assert.AreEqual(StreamEventKind.Init, e.Kind);
    }

    [Test]
    public void Assistant_text_is_extracted()
    {
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Hello\"}]}}";
        var e = StreamJsonParser.ParseLine(line)[0];
        Assert.AreEqual(StreamEventKind.AssistantText, e.Kind);
        Assert.AreEqual("Hello", e.Text);
    }

    [Test]
    public void Tool_use_name_is_extracted()
    {
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"mcp__blender__generate_hyper3d_model_via_text\",\"input\":{}}]}}";
        var e = StreamJsonParser.ParseLine(line)[0];
        Assert.AreEqual(StreamEventKind.ToolUse, e.Kind);
        Assert.AreEqual("mcp__blender__generate_hyper3d_model_via_text", e.ToolName);
    }

    [Test]
    public void Result_line_parses_as_Result()
    {
        var e = StreamJsonParser.ParseLine("{\"type\":\"result\",\"subtype\":\"success\"}")[0];
        Assert.AreEqual(StreamEventKind.Result, e.Kind);
    }

    [Test]
    public void Garbage_line_parses_as_Unknown_without_throwing()
    {
        var e = StreamJsonParser.ParseLine("not json")[0];
        Assert.AreEqual(StreamEventKind.Unknown, e.Kind);
    }

    [Test]
    public void Assistant_message_with_text_and_tool_use_yields_two_events()
    {
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Creating it\"},{\"type\":\"tool_use\",\"name\":\"mcp__unity__manage_gameobject\",\"input\":{}}]}}";
        var events = StreamJsonParser.ParseLine(line);
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(StreamEventKind.AssistantText, events[0].Kind);
        Assert.AreEqual("Creating it", events[0].Text);
        Assert.AreEqual(StreamEventKind.ToolUse, events[1].Kind);
        Assert.AreEqual("mcp__unity__manage_gameobject", events[1].ToolName);
    }
}
