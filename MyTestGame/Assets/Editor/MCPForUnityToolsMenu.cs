using MCPForUnity.Editor.Windows;
using UnityEditor;

namespace Flower.Editor
{
    /// <summary>
    /// Keeps a project-local entry for MCP for Unity in the Tools menu.
    /// The package also exposes its own entry under Window/MCP for Unity.
    /// </summary>
    internal static class MCPForUnityToolsMenu
    {
        [MenuItem("Tools/MCP for Unity/Open MCP Window", priority = 1000)]
        private static void OpenMCPWindow()
        {
            MCPForUnityEditorWindow.ShowWindow();
        }
    }
}
