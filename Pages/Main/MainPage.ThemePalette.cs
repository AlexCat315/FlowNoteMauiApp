namespace FlowNoteMauiApp;

public partial class MainPage
{
    private sealed class ThemePalette
    {
        public static readonly ThemePalette Light = new(
            modeButtonExpandedBackground: Color.FromArgb("#E8F4FD"),
            modeButtonCollapsedBackground: Color.FromArgb("#FFFFFF"),
            modeButtonExpandedBorder: Color.FromArgb("#4A90E2"),
            modeButtonCollapsedBorder: Color.FromArgb("#D1D1D6"),
            modeSelectionBackground: Color.FromArgb("#E8F4FD"),
            modeSelectionText: Color.FromArgb("#1C1C1E"),
            toolSelectedBackground: Color.FromArgb("#E8F4FD"),
            toolNormalBackground: Color.FromArgb("#FFFFFF"),
            toolSelectedBorder: Color.FromArgb("#4A90E2"),
            toolNormalBorder: Color.FromArgb("#D1D1D6"),
            layerSelectedBackground: Color.FromArgb("#E8F4FD"),
            layerSelectedBorder: Color.FromArgb("#4A90E2"),
            layerNormalBorder: Color.FromArgb("#D8E4F5"),
            colorSwatchNormalBorder: Colors.Transparent,
            colorSwatchWhiteBorder: Color.FromArgb("#CBD5E1"),
            segmentInactiveBackground: Colors.White,
            segmentInactiveBorder: Color.FromArgb("#CDD7E6"),
            segmentInactiveText: Color.FromArgb("#4B5566"),
            tabActiveBackground: Color.FromArgb("#4A90E2"),
            tabActiveBorder: Color.FromArgb("#2F74D0"),
            tabActiveText: Colors.White,
            tabInactiveBackground: Colors.White,
            tabInactiveBorder: Color.FromArgb("#CBD5E4"),
            tabInactiveText: Color.FromArgb("#1C1C1E"));

        public static readonly ThemePalette Dark = new(
            modeButtonExpandedBackground: Color.FromArgb("#D7E7FA"),
            modeButtonCollapsedBackground: Color.FromArgb("#E6EEF9"),
            modeButtonExpandedBorder: Color.FromArgb("#4A90E2"),
            modeButtonCollapsedBorder: Color.FromArgb("#9CB2CD"),
            modeSelectionBackground: Color.FromArgb("#D7E7FA"),
            modeSelectionText: Color.FromArgb("#0F172A"),
            toolSelectedBackground: Color.FromArgb("#4A90E2"),
            toolNormalBackground: Color.FromArgb("#E6EEF9"),
            toolSelectedBorder: Color.FromArgb("#2F74D0"),
            toolNormalBorder: Color.FromArgb("#B6C7DD"),
            layerSelectedBackground: Color.FromArgb("#D7E7FA"),
            layerSelectedBorder: Color.FromArgb("#4A90E2"),
            layerNormalBorder: Color.FromArgb("#A8BCD6"),
            colorSwatchNormalBorder: Color.FromArgb("#A1B5D1"),
            colorSwatchWhiteBorder: Color.FromArgb("#A1B5D1"),
            segmentInactiveBackground: Color.FromArgb("#DFEAF9"),
            segmentInactiveBorder: Color.FromArgb("#B5C9E0"),
            segmentInactiveText: Color.FromArgb("#1A2638"),
            tabActiveBackground: Color.FromArgb("#4A90E2"),
            tabActiveBorder: Color.FromArgb("#2F74D0"),
            tabActiveText: Colors.White,
            tabInactiveBackground: Colors.White,
            tabInactiveBorder: Color.FromArgb("#C7D6EA"),
            tabInactiveText: Color.FromArgb("#1C1C1E"));

        private ThemePalette(
            Color modeButtonExpandedBackground,
            Color modeButtonCollapsedBackground,
            Color modeButtonExpandedBorder,
            Color modeButtonCollapsedBorder,
            Color modeSelectionBackground,
            Color modeSelectionText,
            Color toolSelectedBackground,
            Color toolNormalBackground,
            Color toolSelectedBorder,
            Color toolNormalBorder,
            Color layerSelectedBackground,
            Color layerSelectedBorder,
            Color layerNormalBorder,
            Color colorSwatchNormalBorder,
            Color colorSwatchWhiteBorder,
            Color segmentInactiveBackground,
            Color segmentInactiveBorder,
            Color segmentInactiveText,
            Color tabActiveBackground,
            Color tabActiveBorder,
            Color tabActiveText,
            Color tabInactiveBackground,
            Color tabInactiveBorder,
            Color tabInactiveText)
        {
            ModeButtonExpandedBackground = modeButtonExpandedBackground;
            ModeButtonCollapsedBackground = modeButtonCollapsedBackground;
            ModeButtonExpandedBorder = modeButtonExpandedBorder;
            ModeButtonCollapsedBorder = modeButtonCollapsedBorder;
            ModeSelectionBackground = modeSelectionBackground;
            ModeSelectionText = modeSelectionText;
            ToolSelectedBackground = toolSelectedBackground;
            ToolNormalBackground = toolNormalBackground;
            ToolSelectedBorder = toolSelectedBorder;
            ToolNormalBorder = toolNormalBorder;
            LayerSelectedBackground = layerSelectedBackground;
            LayerSelectedBorder = layerSelectedBorder;
            LayerNormalBorder = layerNormalBorder;
            ColorSwatchNormalBorder = colorSwatchNormalBorder;
            ColorSwatchWhiteBorder = colorSwatchWhiteBorder;
            SegmentInactiveBackground = segmentInactiveBackground;
            SegmentInactiveBorder = segmentInactiveBorder;
            SegmentInactiveText = segmentInactiveText;
            TabActiveBackground = tabActiveBackground;
            TabActiveBorder = tabActiveBorder;
            TabActiveText = tabActiveText;
            TabInactiveBackground = tabInactiveBackground;
            TabInactiveBorder = tabInactiveBorder;
            TabInactiveText = tabInactiveText;
        }

        public Color ModeButtonExpandedBackground { get; }
        public Color ModeButtonCollapsedBackground { get; }
        public Color ModeButtonExpandedBorder { get; }
        public Color ModeButtonCollapsedBorder { get; }
        public Color ModeSelectionBackground { get; }
        public Color ModeSelectionText { get; }
        public Color ToolSelectedBackground { get; }
        public Color ToolNormalBackground { get; }
        public Color ToolSelectedBorder { get; }
        public Color ToolNormalBorder { get; }
        public Color LayerSelectedBackground { get; }
        public Color LayerSelectedBorder { get; }
        public Color LayerNormalBorder { get; }
        public Color ColorSwatchNormalBorder { get; }
        public Color ColorSwatchWhiteBorder { get; }
        public Color SegmentInactiveBackground { get; }
        public Color SegmentInactiveBorder { get; }
        public Color SegmentInactiveText { get; }
        public Color TabActiveBackground { get; }
        public Color TabActiveBorder { get; }
        public Color TabActiveText { get; }
        public Color TabInactiveBackground { get; }
        public Color TabInactiveBorder { get; }
        public Color TabInactiveText { get; }
    }

    private ThemePalette Palette => IsDarkTheme ? ThemePalette.Dark : ThemePalette.Light;
}
