namespace AIGameBuilder
{
    public enum BuilderStatus
    {
        Idle,
        Thinking,
        EditingScene,
        ModelingInBlender,
        Compiling
    }

    public static class BuilderStatusExtensions
    {
        public static string Label(this BuilderStatus status)
        {
            switch (status)
            {
                case BuilderStatus.Thinking: return "Thinking...";
                case BuilderStatus.EditingScene: return "Editing scene...";
                case BuilderStatus.ModelingInBlender: return "Modeling in Blender...";
                case BuilderStatus.Compiling: return "Compiling...";
                default: return "Idle";
            }
        }
    }
}
