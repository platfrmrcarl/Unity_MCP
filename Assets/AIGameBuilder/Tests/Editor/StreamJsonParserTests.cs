using NUnit.Framework;
using AIGameBuilder;

public class StreamJsonParserTests
{
    [Test]
    public void Init_line_parses_as_Init()
    {
        var e = StreamJsonParser.ParseLine("{\"type\":\"system\",\"subtype\":\"init\"}");
        Assert.AreEqual(StreamEventKind.Init, e.Kind);
    }

    [Test]
    public void Assistant_text_is_extracted()
    {
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Hello\"}]}}";
        var e = StreamJsonParser.ParseLine(line);
        Assert.AreEqual(StreamEventKind.AssistantText, e.Kind);
        Assert.AreEqual("Hello", e.Text);
    }

    [Test]
    public void Tool_use_name_is_extracted()
    {
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"mcp__blender__generate_hyper3d_model_via_text\",\"input\":{}}]}}";
        var e = StreamJsonParser.ParseLine(line);
        Assert.AreEqual(StreamEventKind.ToolUse, e.Kind);
        Assert.AreEqual("mcp__blender__generate_hyper3d_model_via_text", e.ToolName);
    }

    [Test]
    public void Result_line_parses_as_Result()
    {
        var e = StreamJsonParser.ParseLine("{\"type\":\"result\",\"subtype\":\"success\"}");
        Assert.AreEqual(StreamEventKind.Result, e.Kind);
    }

    [Test]
    public void Garbage_line_parses_as_Unknown_without_throwing()
    {
        var e = StreamJsonParser.ParseLine("not json");
        Assert.AreEqual(StreamEventKind.Unknown, e.Kind);
    }
}
