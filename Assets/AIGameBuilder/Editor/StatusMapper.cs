namespace AIGameBuilder
{
    public static class StatusMapper
    {
        public static BuilderStatus ForTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return BuilderStatus.Thinking;
            if (toolName.StartsWith("mcp__blender__")) return BuilderStatus.ModelingInBlender;
            if (toolName.StartsWith("mcp__unity__")) return BuilderStatus.EditingScene;
            return BuilderStatus.Thinking;
        }
    }
}
