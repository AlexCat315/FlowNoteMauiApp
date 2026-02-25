using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace FlowNoteMauiApp.Helpers;

public static class IconHelper
{
    public static ImageSource GetOutlineIcon(string name) => ImageSource.FromFile($"icon_{name.Replace('-', '_')}.svg");
    public static ImageSource GetFilledIcon(string name) => ImageSource.FromFile($"icon_{name.Replace('-', '_')}.svg");

    public static readonly (string Name, string Outline, string Filled)[] DrawingTools = new[]
    {
        ("pencil", "icon_pencil.svg", "icon_pencil.svg"),
        ("highlighter", "icon_brush.svg", "icon_brush.svg"),
        ("eraser", "icon_eraser.svg", "icon_eraser.svg"),
    };

    public static readonly (string Name, string Icon)[] ToolBarIcons = new[]
    {
        ("menu", "icon_menu.svg"),
        ("prevPage", "icon_arrow_left.svg"),
        ("nextPage", "icon_arrow_right.svg"),
        ("draw", "icon_pencil.svg"),
        ("undo", "icon_arrow_back_up.svg"),
        ("redo", "icon_arrow_forward_up.svg"),
        ("clear", "icon_trash.svg"),
        ("layers", "icon_layers_selected.svg"),
        ("close", "icon_x.svg"),
        ("settings", "icon_settings.svg"),
        ("search", "icon_search.svg"),
        ("home", "icon_home.svg"),
        ("folder", "icon_folder.svg"),
        ("file", "icon_file.svg"),
        ("plus", "icon_plus.svg"),
        ("minus", "icon_minus.svg"),
    };
}
