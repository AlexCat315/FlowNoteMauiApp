using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace FlowNoteMauiApp.Helpers;

public static class IconHelper
{
    public static ImageSource GetOutlineIcon(string name) => ImageSource.FromFile($"icons/outline/{name}.svg");
    public static ImageSource GetFilledIcon(string name) => ImageSource.FromFile($"icons/filled/{name}.svg");

    public static readonly (string Name, string Outline, string Filled)[] DrawingTools = new[]
    {
        ("pencil", "pencil.svg", "pencil.svg"),
        ("highlighter", "brush.svg", "brush.svg"),
        ("eraser", "eraser.svg", "eraser.svg"),
    };

    public static readonly (string Name, string Icon)[] ToolBarIcons = new[]
    {
        ("menu", "menu.svg"),
        ("prevPage", "arrow-left.svg"),
        ("nextPage", "arrow-right.svg"),
        ("draw", "pencil.svg"),
        ("undo", "arrow-back-up.svg"),
        ("redo", "arrow-forward-up.svg"),
        ("clear", "trash.svg"),
        ("layers", "layers-selected.svg"),
        ("close", "x.svg"),
        ("settings", "settings.svg"),
        ("search", "search.svg"),
        ("home", "home.svg"),
        ("folder", "folder.svg"),
        ("file", "file.svg"),
        ("plus", "plus.svg"),
        ("minus", "minus.svg"),
    };
}
