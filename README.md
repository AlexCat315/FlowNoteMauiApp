[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
# FlowNoteMauiApp

FlowNoteMauiApp 是一个基于 .NET MAUI 的跨平台 PDF 阅读与手写笔记应用，面向 Android / iOS / MacCatalyst。
![app](./showapp.jpg "showapp")
## 1. 核心能力

- PDF 文档打开、分页浏览、缩放与翻页
- 多笔工具手写（圆珠笔 / 钢笔 / 铅笔 / 记号笔 / 橡皮）
- 压感书写与压感灵敏度调节
- 图层管理（可见/隐藏、锁定、删除、新增）
- 缩略图面板（支持“叠加笔记层”）
- 主页文档管理（筛选、排序、搜索、标签页）
- 中英双语（`AppResources.resx` / `AppResources.zh-Hans.resx`）
- 可持久化设置（主题、显示、交互、书写行为）

## 2. 目录结构

```text
FlowNoteMauiApp/
├─ Core/Services/              # 业务契约接口（IWorkspaceService 等）
├─ Data/Persistence/           # 本地持久化实现（WorkspaceStore / DrawingStateStore）
├─ Controls/                   # 自定义控件（含 DrawingCanvas）
├─ Models/                     # 业务模型（笔画/图层/工作区）
├─ Pages/                      # 页面逻辑（MainPage 按 Editor/Home/... 拆分）
├─ Views/                      # 页面组合视图（Editor/Home/Settings/Shared）
├─ Resources/
│  ├─ AppResources*.resx       # 多语言资源
│  ├─ Images/                  # 图标与图片资源
│  └─ Styles/                  # 全局样式
├─ Platforms/Android/          # Android 平台配置
└─ docs/                       # 文档
```

## 3. 环境要求

- .NET SDK 10（项目使用 `net10.0-*`）
- MAUI Workload（`dotnet workload install maui`）
- Android SDK + JDK（Android 开发）
- Xcode（iOS / MacCatalyst）

## 4. 构建与运行

在仓库根目录执行（推荐）：

```bash
dotnet FlowNoteMauiApp/FlowNoteMauiApp.csproj -f net10.0-android -v minimal
```

运行到设备（示例）：

```bash
dotnet build -t:Run FlowNoteMauiApp/FlowNoteMauiApp.csproj -f net10.0-android
```

## 5. 关键设置说明

### 5.1 两侧书写开关

位置：设置 -> 页面设置 -> `允许在 PDF 两侧书写`

- 开启：页面内、页间距、左右两侧都可落笔（提交后仍会按页裁剪）
- 关闭：只允许“页面内 + 页间距”落笔，左右两侧拦截

### 5.2 缩略图叠加

位置：编辑页 -> 缩略图面板 -> `Show Notes Layer`

- 开启后会把手写层叠加到 PDF 缩略图
- 当前版本已优化压感线宽一致性与后台渲染并发

## 6. 性能策略（已落地）

- 缩略图渲染并发限制（Semaphore）避免 UI 线程争抢
- 缩略图笔迹快照使用预计算包围盒，减少重复点遍历
- 缩略图叠加只处理当前页相交笔画
- 叠加开启时限制一次显示缩略图数量，降低峰值压力
- 压感分段渲染与主画布保持一致，减少视觉偏差


## 7. 相关文档

- 功能使用说明（详细）：`docs/功能使用说明.md`
- 结构与职责说明：`docs/项目结构与代码职责.md`
