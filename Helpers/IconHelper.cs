using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace FlowNoteMauiApp.Helpers;

public static class IconHelper
{
    public static ImageSource GetOutlineIcon(string name) => ImageSource.FromFile($"icon_{name.Replace('-', '_')}.png");
    public static ImageSource GetFilledIcon(string name) => ImageSource.FromFile($"icon_{name.Replace('-', '_')}.png");

    public static readonly (string Name, string Outline, string Filled)[] DrawingTools = new[]
    {
        ("pencil", "icon_pencil.png", "icon_pencil.png"),
        ("highlighter", "icon_brush.png", "icon_brush.png"),
        ("eraser", "icon_eraser.png", "icon_eraser.png"),
    };

    public static readonly (string Name, string Icon)[] ToolBarIcons = new[]
    {
        ("menu", "icon_menu.png"),
        ("prevPage", "icon_arrow_left.png"),
        ("nextPage", "icon_arrow_right.png"),
        ("draw", "icon_pencil.png"),
        ("undo", "icon_arrow_back_up.png"),
        ("redo", "icon_arrow_forward_up.png"),
        ("clear", "icon_trash.png"),
        ("layers", "icon_layers_selected.png"),
        ("close", "icon_x.png"),
        ("settings", "icon_settings.png"),
        ("search", "icon_search.png"),
        ("home", "icon_home.png"),
        ("folder", "icon_folder.png"),
        ("file", "icon_file.png"),
        ("plus", "icon_plus.png"),
        ("minus", "icon_minus.png"),
    };
}
