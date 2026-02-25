# ExampleMauiApp

`ExampleMauiApp` 用于演示 `Flow.PDFView` 的核心能力与交互。

## 当前演示内容

- PDF 加载
  - URL 加载
  - 本地文件加载
  - 重载
- 视图控制
  - 显示模式、滚动方向、适配策略
  - 缩放开关、滑动开关、链接开关
  - 缩放滑条
- 页面控制
  - 上一页 / 下一页
  - 页码与状态显示
- 搜索工具栏
  - 搜索、清除
  - 上一个 / 下一个搜索结果跳转
  - 高亮开关（Android 可见高亮；iOS/MacCatalyst 当前为 API 对齐）

## 交互说明

- 右下角按钮打开工具栏。
- 工具栏为半透明卡片，不会遮挡底部 PDF 背景可见性。
- 页码跳转按钮使用控件实时页码，避免事件同步延迟导致的“点击无响应”。

## 重点测试项

1. 输入关键词后点击 `搜索`，确认定位到首个结果。
2. 连续点击 `上一个` / `下一个`，确认在结果间循环跳转。
3. 点击 `下一页`，确认页码更新且文档跳转。
4. 在 Android 上打开/关闭高亮，确认搜索高亮效果。

## 构建与运行

```bash
dotnet build ExampleMauiApp/ExampleMauiApp.csproj -f net10.0-android
dotnet build ExampleMauiApp/ExampleMauiApp.csproj -f net10.0-ios
dotnet build ExampleMauiApp/ExampleMauiApp.csproj -f net10.0-maccatalyst
```

VS Code 运行 Android 时可使用你现有的 `dotnet build -t:Run ...` 参数模板。
