using NUnit.Framework;
using AIGameBuilder;

public class StatusMapperTests
{
    [Test]
    public void Blender_tools_map_to_ModelingInBlender()
    {
        Assert.AreEqual(BuilderStatus.ModelingInBlender, StatusMapper.ForTool("mcp__blender__generate_hyper3d_model_via_text"));
    }

    [Test]
    public void Unity_tools_map_to_EditingScene()
    {
        Assert.AreEqual(BuilderStatus.EditingScene, StatusMapper.ForTool("mcp__unity__manage_gameobject"));
    }

    [Test]
    public void Unknown_tool_maps_to_Thinking()
    {
        Assert.AreEqual(BuilderStatus.Thinking, StatusMapper.ForTool("Bash"));
    }
}
