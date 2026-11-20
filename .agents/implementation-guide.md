# TKCode.Arc 实施指南：基于源码重建版

这份指南用于当前工作区的后续组件开发。它是根据 AGENTS.md、源码、生成器和测试整理的本地手册，**不是上游缺失的 `design/engineering/implementation-guide.md` 原文，也不能证明已经遵循原手册的全部条款**。

研究日期：2026-11-16，本地组件补充更新至 2026-11-18。核心仓库基线：`57e42d28dd56b0d91237b523271cabeb36eafed4`；ArcGallery 基线：`2e2f250f36f3476956bdff10d0c277d7bdef3fd5`。研究也包含工作区新增的 Thermometer、WindDirectionIndicator、StatusIndicator、LevelIndicator、LinearGauge 和配套文档修改；这些本地实现不会被视为上游设计要求。

2026-11-17 功能分支同步核心个人仓库 main 至 `5a2bb2aa92e83f66d1e5343faa787e018134d9ae`。分层事实及拆包迁移见第 3、15 节；以上基线保留为原始研究记录。

本文的章节顺序是重新组织的，不对应缺失手册的原始章节编号。本文件现位于 `TKCode.Arc/.agents/`。文中 `TKCode.Arc/` 与 `ArcGallery/` 路径均相对于工作区根目录（本文件向上两级）；构建命令仍从各自仓库根目录执行。

用户后续开发要求已记录到工作区根 AGENTS.md，并接入两个仓库的 AGENTS.md：所有新增和修改代码同时遵循本指南、核心仓库的明文规则及所修改仓库的现有代码风格。研究归纳不是豁免已有规范的理由。

## 1. 怎样使用这份指南

每节给出依据编号，源码链接见末尾“证据索引”。规则分为三类：

| 标记 | 含义 | 开发时怎样处理 |
| --- | --- | --- |
| 明文规则 | 当前 AGENTS.md 或构建配置直接规定 | 按规则执行；不能用某个旧实现来豁免 |
| 源码事实 | 生成器、运行时或测试中可以确认的行为 | 按当前实现使用；升级仓库后重新核对 |
| 开发建议 | 从已有模式归纳，或针对工业应用提出 | 按需求采用，不宣称是上游强制规定 |

明文规则与已有代码不一致时，分别记录“要求”和“现状”，不能把代码里的例外推广为新规范。本手册也不替代 Avalonia 的公开 API 文档和项目自身的 AGENTS.md。

## 2. 先复用，再决定放在哪里

**明文规则：** 主控件按 `<Category>/<Component>/` 组织，控件使用平坦命名空间 `TKCode.Arc`，类名不添加 Arc 等框架前缀。物理目录不是命名空间层级。[E01]

**开发建议：** 按控件职责分类，而不是把所有用于工业场景的控件都迁入 Industrial。

| 需求 | 首选做法 | 位置 |
| --- | --- | --- |
| 通用范围读数、按钮、开关、输入框、表格、趋势 | 使用已有控件 | 原有分类 |
| 仪表、读数、连接状态、报警操作组成一个设备面板 | 组合已有控件；业务代码提供数据 | 业务应用；满足通用区块条件后再放 Blocks |
| 新的通用数值可视化 | 先参考 Gauge、图表共享基础 | 本地新增图表用 CustomCharts，其他组件按职责分类 |
| 确有独立工业表达的新组件 | 新增工业专用控件 | 当前先放 `src/TKCode.Arc/CustomCharts/<Component>/` |

**本项目决策（用户已确认）：** 本地新增图表和工业展示控件暂时统一放在 `CustomCharts/<Component>/`，当前包括 Thermometer、WindDirectionIndicator、StatusIndicator、LevelIndicator 和 LinearGauge；上游已有 Charts 与 AdvancedCharts 保持原分类。Gallery 的 Demos、三语 Docs 和核心测试目录同步使用 CustomCharts，侧栏显示“自定义图表”。这是本工作区的维护分类，不是上游要求；命名空间仍为 `TKCode.Arc`，复用同一项目、Token、主题和共享原语，不复制共享代码。风玫瑰图属于分布统计，实时风向指示属于即时仪表。新增前仍先检查已有组件能否通过配置或组合满足需求。

新增目录不需要立即新增项目或 NuGet 包。独立发布、依赖隔离等需求出现后，再决定拆包。核心库的 `_Shared`、`_SharedB` 是实现共享目录，不应成为业务应用依赖的公共接口。[E14、E15]

**工业展示的逐步实施（本地决策）：** 第一项是 `StatusIndicator`：默认 Unknown，应用提供 Offline、Idle、Running、Warning 或 Fault；状态文字默认可见，支持自定义 Label 和只读 DisplayText。状态颜色与文字立即生效，可选闪烁仅在 Full 动效下作用于灯；Reduced/Off 常亮。主题模板复用现有资源机制，语言订阅随挂载解除与恢复，只有请求闪烁时才订阅布局变化以处理父容器隐藏。已有 Switch 继续作为操作输入。第二项补齐已有温度计和罗盘的数据质量，见下段。第三项为测量读数组合示例，见下段。第四项为液位控件，见下段。采集协议、数据库、时间有效性、报警判定和设备命令都留在业务层。

**测量读数组合（本地实现）：** Gallery 在原 `Cards/MetricCard` 页面增加 `Measurements`，组合展示风速、相对湿度和气压，未新增公共控件、Token 或包。演示数据源初始 NoData，保存可空有限值；有效快照绑定 Value/Suffix/Decimals，其他质量用不含数值属性的快照替换 ContentControl 内容，旧计数器从可视树与自动化树移除。合法零值正常显示，NaN/Infinity 降级 Error 并保存最后有限值，未定义质量枚举也降级 Error。数据源在 UI 线程完成字段与快照后通知绑定；恢复调用 SetSample(新值, Good)，同值也通知。局部 ArcMotion.Level=Off，真实读数立即显示。Change 的正负染色只表示统计方向，不表示报警。占位表面复用 MetricCard 几何与 ArcDesign 动态主题；中文演示文案由业务数据提供，不属于核心控件内置语言目录，实际应用应接入自己的语言资源。无计时器、外部订阅或协议服务。已有 MetricCard API、默认行为和 Cards 分类保持原契约，不受本地新控件放 CustomCharts 的决策影响。

**测量质量（本地实现）：** 两控件共享平坦命名空间下的 `MeasurementQuality`，`Quality` 默认 Good，保留原默认读数契约。NoData、Stale、Error 分别显示无数据、数据过期、采集异常；质量文字优先于温度阈值/量程错误或静风，过期使用主题 Warning，异常使用 Danger，无数据使用 TextMuted。非 Good 的温度计隐藏液柱并用破折号替代数字，罗盘隐藏指针并在圆心显示状态（与 ShowReading 无关）。保存 Value/Direction 不被清零；温度计 State 和旧数值范围无障碍接口仍表示保存值，帮助文字和新增只读文本接口报告质量。罗盘 HasData=false 始终覆盖为 NoData，恢复需应用同时恢复质量和标记。数据质量由业务层提供，无自动超时或采集服务；恢复时先更新数值、再恢复 Good，首个有效读数直接显示，连续有效采样才允许 Full 动效。Gallery 增加绑定质量源的交互按钮和四状态对照。

**液位控件（本地实现）：** `LevelIndicator` 复用 Thermometer 的绘制、Token、主题、语言和动效模式：在 220 × 360 参考坐标中绘制罐体，液体按 `Value` 映射的比例从底部向上填充，右侧为刻度，罐体上方为标题、下方为当前数字与状态。`LevelIndicatorState`（Low/Normal/High/InvalidRange）由真实 `Value` 和阈值决定并立即更新，只有液面使用 MotionValue 弹簧；液位、量程和阈值属于显示语义，与 `MeasurementQuality` 分开。非 Good 质量隐藏液面并把数字替换为破折号，但保留 `Value` 与阈值区间。圆角复用 ArcDesign 的 `RadiusControl`，不在组件 Token 中另存；`LevelIndicatorToken` 只保存 DialWidth/DialHeight/VesselWidth/OutlineThickness。量程无效时显示 InvalidRange 并清空罐体。无障碍为 `ProgressBar` 类型的只读数值范围加只读文本接口，且仅在帮助文字变化时通知。Gallery 提供绑定质量源的交互示例与四状态对照，不复制控件实现。

2026-11-17 液位边界修正：液面由圆角填充与矩形裁剪的交界直接表达，不再叠加横跨内框的同色描边；该描边会在满量程时越过顶部圆角，空罐时留下假液面。0%、1%、99%、100% 与越界读数均纳入定向回归。

**通用线性仪表（本地实现）：** `LinearGauge` 在同一 CustomCharts 分类中独立继承 Control，以窄轨道展示拉力、压力、速度等连续量程。默认量程 0～100、阈值 20/80、小数位 1，单位默认空；低/正常/高色调为 Warning/Accent/Danger。真实读数决定只读 `LinearGaugeState`，质量文字优先，恢复与首样本立即落位。共享 `GaugeScale` 负责有限范围映射、刻度插值及圆角填充裁剪；常见窄范围直接运算，正负极大范围缩小后运算，避免溢出与窄范围精度损失。轨道左侧短线表示量程内阈值；Token 仅包含 DialWidth/DialHeight/TrackWidth/OutlineThickness，圆角、字重与颜色跟随 ArcDesign。Gallery 并排展示拉力、压力和转速，交互仅作用于拉力。设备逻辑仍由应用负责。

## 3. 分层和包注册

**远程单元格编辑（本地实现，2026-11-18）：** AdvancedCharts 的 DataGrid 增加可选 `IDataGridEditableDataSource`，旧只读 `IDataGridDataSource` 无须修改。单元格解析、阻止式校验、CanEditCell、预提交事件与原编辑器共用；一次只保存一个单元格。服务端返回稳定键相同的独立权威行，成功事件使用其值，随后重新读取当前查询以确认排序、筛选和总计。`CommitEditAsync()` 等待保存确认，查询重读另行完成；`IsCellCommitPending` 通过 DirectProperty 通知绑定。失败保留草稿，取消、切换、列或模板变化及卸载隔离迟到结果。取消不保证服务端未写，不自动重试、不引入远程批量写入或本地撤销、HTTP/数据库依赖。权限、版本检查和持久化留在应用的数据源实现。

**源码事实：**

- Foundation 提供 Token、主题状态与发布、包注册、本地化，以及生成器依赖的运行时类型。
- Core 引用 Foundation，提供 ArcDesign、动效、基础主题和共享原语。
- 主控件库引用 Core，不再引用 StreamMark 或 TextMate。2026-11-17 合并个人仓库 main 的 `5a2bb2a` 后，`Markdown`、`ChatThread`、`ChatComposer` 及其 Token/主题位于 `TKCode.Arc.Markdown`；TextMate 高亮位于 `TKCode.Arc.Highlighting`。
- 使用 Markdown/聊天组件需引用新 Markdown 包并启用 `UseArcMarkdown()`；彩色代码需引用 Highlighting 包并启用 `UseArcHighlighting()`。Blocks、Special、A2UI 自动依赖和注册两包。Gallery 也显式引用和注册它们，XML 文档收集仍遍历全部 `TKCode.Arc*` 引用。
- `UseArcControls()` 通过生成的 `ArcGeneratedAssets.AddTo(...)` 注册资产，并声明 `UseArcCore()` 依赖。
- `ArcBuilder.UsePackage` 按依赖顺序注册，重复启用幂等，依赖循环会抛出异常。[E02、E07]

AGENTS.md 已明确上述拆包依赖例外。新组件默认复用已有基础，不据此随意增加第三方包；任何新增依赖应单独说明必要性和发布影响。

已有主项目设置 `ArcFoundationGenerator=true`。在其中新增正确标记的 Token、合规主题和语言目录，生成器会发现它们，通常无需手写注册清单。

新增独立包时，则需要自己的项目配置、生成器接入、包扩展方法及依赖声明。不要复制其他程序集的生成文件或直接引用其内部 `ArcGeneratedAssets`。[E02、E03、E07]

`AppBuilder.UseArc(...)` 的配置回调执行时应用尚未建立，不能在其中创建依赖平台初始化的对象；轻量平台初始化放在包的 `OnCommit`，第一次主题使用时的工作可放在 `OnFirstUse`。[E07]

## 4. 一个组件通常有哪些文件

**明文规则与源码事实：** 以下是新组件的常见布局，辅助文件按实际需要增加。[E01、E03、E05]

```text
src/TKCode.Arc/<Category>/<Component>/
  <Component>.cs
  <Component>Token.cs
  <Component>State.cs                    # 有状态枚举时
  Themes/
    <Component>Theme.axaml
  Localization/                         # 有内置用户文案时
    <Component>LangResourceKind.cs
    en-US.xlf
    zh-CN.xlf
    zh-TW.xlf

tests/TKCode.Arc.Tests/<Category>/
  <Component>Tests.cs

ArcGallery/gallery/TKCode.Arc.Gallery/
  Demos/<Category>/<Component>/<Demo>.axaml
  Demos/<Category>/<Component>/<Demo>.axaml.cs   # 只有需要逻辑时
  Docs/<Category>/<Component>/en-US.md
  Docs/<Category>/<Component>/zh-CN.md
  Docs/<Category>/<Component>/zh-TW.md
```

组件可以有多个主题文件，但每个资源键只能由一个文件定义；不是“每个组件只能有一个 ControlTheme”。例如 SegmentedControl 的同一个主题文件同时定义组控件和容器的主题。[E05、E12]

不要添加 `*Themes.axaml` 汇总文件。自绘控件也需要 Token 与主题：Gauge 的主题主要设置几何属性，没有模板。[E01、E05、E11]

## 5. Token：声明、计算与引用

### 5.1 声明规则

**明文规则：** 每个公共组件需要同命名空间的 `[ControlDesignToken] <Component>Token`，至少有一个令牌属性。Token 类可以是 internal；Gauge 就采用 `internal sealed`。[E01、E04、E11]

**源码事实：** 生成器要求 Token 是继承 `AbstractControlDesignToken` 的非抽象、非泛型、顶层类，具有可访问的无参构造。令牌属性是公开实例属性，必须有公开 get/set，不能是索引器、静态属性或仅 init 属性。[E04]

身份默认是类名去掉 `Token`，也可用 `Identity` 显式设置。显式身份改变覆盖配置中的名称，生成类型仍使用类名词干。身份要在同时启用的所有包中唯一，Mobile 也包括在内：生成器检查本程序集，运行时还检查跨包冲突。[E04、E07]

目前可解析类型：

```text
Color、double、int、bool、string、Thickness、CornerRadius、
BoxShadows、FontFamily、TimeSpan、Easing，以及枚举
```

当前 Avalonia 的 `FontWeight` 在生成器样例中也被支持；分类实现通过枚举分支处理这一类类型。不要因此推断任意字体或绘图类型都能作为 Token。`Point`、`Color?` 等明确出现在拒绝类型的诊断快照中。[E04、E24]

### 5.2 计算规则

**明文规则：** 颜色、圆角、阴影与字体来自 ArcDesign。组件 Token 主要存几何参数和不依赖强调色的派生颜色，不把设备状态或动态读数存成 Token。[E01]

**源码事实：** `CalculateTokenValues(bool isDarkMode)` 先调用 base，只依赖 `EffectiveGlobalToken` 和 `isDarkMode`。计算可能在工作线程执行，不能读取控件树、数据库、设备服务或 UI 线程状态。主题计算顺序为：组件计算 → 按顺序套用预设 → 应用自己的覆盖 → 发布资源。[E06]

几何常量可以作为设计坐标、算法参数或安全回退存在于代码中；需要应用可定制的外观参数才应进入 Token。已有控件仍有固定间距和默认值，不能据此宣称“所有数字都必须是 Token”，也不能用固定视觉颜色替代主题资源。

温度计采用固定 220 × 360 绘图参考坐标，DialWidth/DialHeight 作为共同约束最大显示尺寸的令牌。测量和布局使用同一适配函数，绘制使用布局结果；宽高比例不同的覆盖也维持等比例显示。字体字重通过主题绑定 WeightMedium/WeightRegular，覆盖后会使文字缓存失效，而不只检查默认字体或默认尺寸。

本地新增的 `WindDirectionIndicator` 同样使用统一适配函数，默认绘图参考为 320 × 320，开启 `ShowReading` 时为 320 × 352。`DialWidth`、`DialHeight` 默认 320、352，共同限制等比例显示尺寸；`NeedleLength`、`NeedleWidth`、`RingWidth`、`OutlineThickness` 使用参考坐标单位，尺寸令牌不会拉伸圆盘。双圈刻度带外侧显示完整十六角度，内侧显示八方位，四主方位使用较大主题字体，指针为实心凹尾几何。实时风向表示气象来向；风速由外部读数承载，`HasData` 和 `IsCalm` 由应用明确设置。它与统计风向分布的 `WindRoseChart` 职责不同。此组件尚未发布，初始 API 收敛不涉及已发布成员的兼容迁移。

### 5.3 引用规则

**源码事实：** 生成器为组件生成 `<Component>TokenKind`、`<Component>Tokens`、`<Component>TokenResourceExtension`、描述器和解析器。辅助类型位于 `TKCode.Arc.DesignTokens`；这不改变控件与手写 Token 的平坦命名空间。[E03、E04、E24]

XAML 中使用：

```xml
<Setter Property="Foreground" Value="{arc:ArcDesignTokenResource Foreground}" />
<Setter Property="CornerRadius" Value="{arc:ArcDesignTokenResource RadiusControl}" />
<Setter Property="DialMaxWidth" Value="{arc:GaugeTokenResource DialMaxWidth}" />
```

C# 构建的子控件优先使用 `ArcTokens.Bind`，使值跟随主题。即时读取使用 `ArcTokens.Get<T>` 或 `GetColor`，但一次读取本身不会建立动态跟踪。颜色 Token 发布为画刷，`Get<Color>` 可以解包。[E09]

`ArcTokenValueExtension` 是一次性读取，适用于不能接受动态资源的场合，不能把它当成动态资源绑定。自绘控件需要资源变更后的重绘与缓存失效机制；ChartsBPalette 展示了现有做法。[E09、E14]

## 6. 主题、模板和生命周期

### 6.1 主题文件

**明文规则与源码事实：** 文件放在 `Themes/<Name>Theme.axaml`，根为 ResourceDictionary，设置：

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                    x:Class="TKCode.Arc.ExampleTheme"
                    x:ClassModifier="internal">
    <!-- 在此定义有键的主题资源 -->
</ResourceDictionary>
```

上面的片段只说明文件外壳；不能把空字典作为完成的主题提交。生成器只识别直接位于名为 `Themes` 的目录下、以 `Theme.axaml` 结尾的文件；通常生成字典类并通过构造函数加载编译 XAML。顶层带键资源参与按需加载。[E03、E05]

新组件通常不需要 `.axaml.cs`。若项目已经手写对应字典类，生成器会使用它，不再生成同名类。不要同时维护两套注册与加载逻辑。

### 6.2 Avalonia 基类与属性

**明文规则：** 使用 Avalonia 基类和公开 API；不使用 AtomUI 包、代码或内部 API。[E01]

**开发建议：** 模板可定制的视觉组件选 TemplatedControl 或更适合的现有基类；选择列表参考 SelectingItemsControl；图形密集的仪表参考 Gauge 的 Control + Measure/Arrange/Render。基类按职责选择，不一律套同一个模板。

StyledProperty 用于可绑定、可样式化的输入；DirectProperty 可用于计算后只读状态，并通过 `SetAndRaise` 通知。采用基类已有属性时，检查是否应 AddOwner 或覆盖元数据，避免另造等价属性。[E11、E12、E16]

默认值、边界、单位、状态和事件含义写入 XML 文档。一个属性发生变化时，判断它影响的是测量、布局、绘制还是状态；用 `AffectsMeasure`、`AffectsRender` 或显式失效处理，而不是每次全部重建。

### 6.3 模板重建和卸载

**源码事实与开发建议：**

- `OnApplyTemplate` 可能多次执行；先解除旧模板部件的订阅，再寻找并绑定新部件。SegmentedControl 的 ScrollViewer 处理可直接参考。
- 观察业务层集合或对象时，退出可视树后停止订阅，重新进入时重新读取；Gauge + ObservedItems 有完整示例和回收测试。
- 对自身字段持有的资源绑定、外部长寿命事件和全局事件，要分别判断所有权，不能简单要求“每个订阅都在 detach 时永久 Dispose”。
- 需要重复挂载的 MotionValue 或其他对象，不能在 detach 后销毁却不在 attach 时重建。
- 异步操作结束后，确认操作版本、目标对象和挂载状态仍有效，避免旧结果覆盖新状态。[E11、E12、E13、E16]

自绘控件手动创建子控件时，应正确维护逻辑树与可视树，并完成测量和布局。Gauge 在构造函数中同时添加 LogicalChildren 与 VisualChildren。[E11]

### 6.4 内置控件主题

**明文规则与源码事实：** ScrollViewer、ScrollBar、ItemsControl、PathIcon、TextSelectionHandle 及相关窗口/弹出宿主由 Core 的基础主题按需补齐。已有 Fluent、Simple 或应用主题提供了某个类型的默认主题时，Arc 对该类型让位；ScrollViewer 与 ScrollBar 作为一组处理。[E01、E10]

基础主题目录是 `Theme/Base/`，故意不在普通 `Themes/*Theme.axaml` 自动发现模式内。不要把这个例外复制到普通组件。

纯 Arc 应用中，其他 Avalonia 内置控件未必有模板；输入框使用 Arc 的 Input 等控件。ScrollViewer 自动隐藏滚动条，需要常显时设置 `ScrollViewer.AllowAutoHide="False"`。

## 7. 动效的选择和降级

**明文规则：** 可由模板表达的状态变化，优先放在 ControlTheme 的 `^:motion-full` / `^:motion-reduced` 下；参与跟踪的控件设置 `arc:ArcMotion.Tracks="True"`。代码动效使用 MotionValue，并说明模板 Transition 无法表达的原因。[E01、E08、E12]

**源码事实：**

| 机制 | 适合的工作 | 需要注意 |
| --- | --- | --- |
| 主题 Transition | 颜色、透明度、变换等状态切换 | 参考 Button；避免首帧入场时不必要的动画 |
| MotionValue<T> | 可中断的数值、几何、颜色变化 | 弹簧重定向保留位置与速度 |
| LiveLoop | 持续装饰或按帧功能 | 受挂载、可见性、视口、等级和帧率策略约束 |
| ArcContentSwap / GlidingIndicator 等 | 已有内容切换或选择指示模式 | 优先复用，避免自己维护动画状态机 |

有效等级取继承等级与主题等级中的较低者。Off 不设置 motion 伪类；MotionValue 的 Off 立即跳变，Reduced 对弹簧立即跳变。Reduced 并不等价于“任何 Tween 都禁止”；需要保留的淡入淡出由组件明确选择。[E08、E15]

MotionScheduler 按 TopLevel 合并动画帧；离开可视树或不可见的动效对象在后续帧收敛并移出批次。不要在每个控件里另开一个高频定时器。[E15]

对自动采集读数，**开发建议是让真实数值和告警状态立即生效，动画只影响视觉过渡**。本地 Thermometer 按此实现；已有 Gauge 的动画状态文字会跟随运动中的计数，两者语义不同。不能照搬 Gauge 的动画状态作为设备真实告警。[E11、E16]

本地 `WindDirectionIndicator` 的文字立即显示实际风向，只有箭头使用 MotionValue。目标角度从当前视觉位置展开到最近的等价角度，例如 359° → 1° 的目标为 361°；不要直接对两个归一化角度插值。首次有效采样、静风或无数据恢复、Reduced/Off 与重新挂载均直接显示目标。

此组件的文字缓存按内容区分：普通方向、数据状态或自定义状态文案变化只替换读数布局；主题、字体字重或语言变化清空全部文字布局。指针长宽只使指针几何失效，环宽同时使指针与主方向三角标记失效；尺寸和动效变化不重建静态文字。这是本地实现事实，体现第 10 节的按影响范围刷新建议。

## 8. 选项模型、选中状态与事件

**明文规则：** 控件列出的选项、行、操作项等项目类应有默认 true 的 `IsEnabled` 和 `IsVisible`。用 SelectionChanged / ValueChanged 表达状态变化，用 ItemInvoked 表达人主动选用项目。[E01]

**源码事实：** SegmentedOption 使用 AvaloniaObject 与 StyledProperty，能跟随绑定后的选项属性变化。隐藏选项不占布局空间、不参与键盘导航；已有选中项被隐藏时仍保留选中值，只隐藏指示器。[E12]

Sidebar 明确区分：

- 代码、绑定、清空或用户选择引发 SelectedItem 改变：SelectionChanged。
- 用户再次选择已经选中的页面：仅 ItemInvoked。
- 单纯以代码设置选中值：不等于用户调用项目。[E17]

**开发建议：** 在设计新控件前写清该控件的事件触发矩阵，再实现和测试。不能仅依据事件名称推断现有所有控件都具有相同触发范围。只读仪表不需要虚构“用户选择”事件。

对于 SelectingItemsControl，检查选中值及索引的绑定校验需求。现有 SelectionDataValidation.Enable<T>() 开启 SelectedValue 和 SelectedIndex 校验，并保留索引未选中值 -1。[E12]

如果实现容器回收，要检查 Content、Accessory、IsEnabled、IsVisible、事件和样式状态是否正确更新或清除，避免复用旧项目状态。

## 9. 本地化与无障碍

### 9.1 本地化

**源码事实：** 运行时语言目录使用 `[LanguageCatalog]` 顶层枚举与 `Localization/<language>.xlf`，解析 XLIFF 2 命名空间。`file id` 是枚举全名，`unit id` 是枚举成员名。源文件没有 trgLang，译文文件带目标语言和 target；名称、声明语言与占位符都需要匹配。[E18]

当前组件默认内置 en-US、zh-CN、zh-TW。启动时 `UseLanguages` 声明应用支持的语言，每个启用目录必须完整覆盖它们。[E01、E07]

控件提供用户自定义文案覆盖时，推荐 null 表示跟随语言资源；语言变更后刷新文案。长寿命语言管理器的事件需要解除订阅，Thermometer 提供本地参考。[E16]

`Localization/*.xlf` 与 Gallery `Docs/Api/*.xlf` 是两套系统，不能互换：前者是用户文案，后者是 API 说明翻译。仅声明三语文档也不意味着控件的内部文字已经本地化。

### 9.2 无障碍

**源码事实与开发建议：** 明确控件是否可操作、是否需要焦点和键盘导航，以及适合的 AutomationPeer/Provider。Gauge 展示了只读 IRangeValueProvider、名称、帮助文字和状态通知；Avalonia 没有专门 meter 角色时，它使用 ProgressBar 类型并声明 meter 描述。[E11]

表达状态时，同时提供文字或其他明确线索；颜色不能是唯一信息。自绘内部文字也应体现在自动化信息中。应用显式设置的 AutomationProperties.Name 应优先于控件默认标签。

交互控件参考 SegmentedControl 的箭头、Home/End、TabStop 与组自动化实现。改变焦点时参考 FocusModality；自定义焦点环时抑制默认 FocusAdorner，避免双重焦点环。[E12、E19]

## 10. 性能、线程和 AOT

**源码可读性（用户要求）：** 新增和修改代码提供适合初学者阅读的中文注释，先说明阅读顺序、数据由谁提供，再解释属性系统、真实状态与视觉状态、关键分支、算法、绑定通知、缓存及挂载/卸载。必要处用具体数值说明，例如有效的 80 度变为过期、45° 方向恢复为 270°。解释“为什么这样做”和相关 C# / XAML 写法，不机械复述每行赋值。普通中文注释与公共英文 XML 文档分开：后者仍供 Gallery 三语 API 生成使用，不能为补中文说明而造成 API 翻译源文过期。

**明文规则：** 不使用反射；保持 AOT 和裁剪兼容；C# 控制流语句体始终使用花括号。[E01]

**构建事实：** 库启用 Nullable、文档生成、IsTrimmable 与 IsAotCompatible；Nullable 警告作为错误处理。CompiledBindings 默认开启。项目不因 CS1591 被全局抑制就免除公共 API 文档要求。[E02]

**从源码归纳的开发建议：**

- Render 中复用不变几何、文字布局和静态画刷；仅在读数、尺寸、字体或资源版本变化时重建对应缓存。
- 动画产生的大量不同颜色不要无限积累在画刷缓存中；ChartsBPalette 对静态颜色和动画颜色区分使用，并限制缓存大小。
- 大量节点场景尽量减少只为布局包装而增加的控件；Button 对 spinner、焦点环采取首次需要时创建。
- 可以用伪类表达固定状态，但是否优于属性选择器取决于场景；Button 的性能解释不是禁止所有属性选择器。
- 布局要处理无穷可用尺寸、小尺寸、隐藏文本和主题字体变化；不要只验证默认窗口尺寸。
- 设备读数的采集、计算和数据库访问放在业务层；通过 UI 线程更新绑定目标。不能在 Render 或 Token 计算中做 I/O。[E11、E14、E16、E19]

主题切换使用 `ArcTheme.Apply(...)` 或主题管理器 Apply。Apply 要在 UI 线程调用；ApplyAsync 的入口同样检查 UI 线程，再把计算放到工作线程，发布回到 UI 线程。必须 await，禁止在 UI 线程 `.Wait()`、`.Result` 或 `.GetAwaiter().GetResult()`。[E06]

项目声明 AOT 兼容、源代码不使用反射和 Debug 构建通过，都不等于 Native AOT 发布已经成功验证。只有实际执行的发布和运行结果才可写成已验证结论。

## 11. Gallery 是组件交付的一部分

### 11.1 目录与原版链接

**明文规则与源码事实：** 组件文档、演示、目录与截图在另一个仓库，使用 ArcRoot 指向本地核心源码。新增组件需要目录项与 `AddedOn: "yyyy-MM-dd"`；组件数量与分类统计测试也要相应更新。[E01、E20、E21]

Categories 来自 `GalleryCatalog.g.cs`，Extras、Special 等由 GalleryCatalog.cs 组织。原版链接由 IsArc 与 slug/ArcPath 决定；自研组件应使用 `IsArc: false`，避免链接到不存在的 uiarc.dev 页面。

本地 CustomCharts 在 GalleryCatalog.cs 的 Extras 登记，紧跟原有 Charts 显示，源码仍留在主控件项目中。组件原 AddedOn 和 slug 保留；Count 只计算原有分类的 129 项，加入 StatusIndicator 后侧栏 ComponentCount 为 166 项。三语完整性与 New 日期检查已显式包含 CustomCharts，组件测试同时检查演示代码页的新路径。**需要注意：** 当前“三语页面完整性”测试只枚举部分 Extras 分类，后续新增分类不能认为已自动被该测试覆盖；应扩展覆盖集合或增加有意义的分类完整性检查。[E20、E21]

### 11.2 文档页面

页面 front matter 包含 title、summary，译文另有 source-hash。正文包含演示引用与本控件 API 指令：

```markdown
<Demo name="Readings" />

<!-- api: Thermometer -->
```

标准英文二级标题依次为：

```text
When to use
When not to use
Usage
API reference
Accessibility
Motion
Responsive behavior
Related
```

这些顺序由测试检查，不是任意写作建议。翻译页面要保持对应章节、代码块数量和语言顺序、演示引用；代码块内容不要求逐字一致，可以本地化示例标签。[E21]

`source-hash` 的准确算法：将英文整份文件 CRLF/CR 统一为 LF，执行 TrimEnd，UTF-8 编码，以 FNV-1a 64 位计算，取 16 位小写十六进制字符串的前 12 位。哈希包括英文 front matter。不能用文件 SHA256 替代，也不能只更新哈希而不审核译文。[E22]

API 说明由公共类型及其 Arc 基类的 XML summary 生成。更新公共 API 的英文说明后，同步 Docs/Api 中简体、繁体的 source 与 target；避免通过屏蔽 GDOC005/006 来消除过期或缺译警告。[E23]

### 11.3 演示、调节面板和截图

演示文件为 `Demos/<Category>/<Component>/<Name>.axaml`，三语文档以相同 Component.Name 引用；没有代码逻辑的演示由 Gallery 生成器提供视图类。

调节面板是可选项。`.knobs` 中一行对应一个属性，目标控件需名为 `Target`，可编辑类型为可写 bool、enum、int、double、string；数字需指定范围，例如 `Value 0..100 step 1`。[E23]

截图也是按实际需要制作。现有工具以 Markdown 的 `![说明](images/Name.png)` 和随后 xml 代码块识别截图，实际输出为 `Name.webp`、`Name.dark.webp`；Markdown 中的 png 名称是截图标识，不应机械改成 webp。[E25]

## 12. 新组件的最小骨架

下面的 StatusLamp 是**教学骨架**，用于说明 Token 与主题如何连接，不是已经加入框架的组件，也不代表需要另造一个指示灯。实际新增前仍先检查现有组件。

示意布局：`<Category>/StatusLamp/{StatusLamp.cs,StatusLampToken.cs,Themes/StatusLampTheme.axaml}`。类别根据实际用途决定。

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>显示一个由应用提供状态的只读指示灯。</summary>
[PseudoClasses(":active")]
public class StatusLamp : TemplatedControl
{
    /// <summary>定义指示灯是否处于活动状态的属性。</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<StatusLamp, bool>(nameof(IsActive));

    /// <summary>是否处于活动状态；默认 false，状态由应用提供。</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(":active", IsActive);
        }
    }
}
```

```csharp
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class StatusLampToken : AbstractControlDesignToken
{
    /// <summary>指示灯的默认直径。</summary>
    public double Diameter { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Diameter = 16;
    }
}
```

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                    x:Class="TKCode.Arc.StatusLampTheme"
                    x:ClassModifier="internal">
    <ControlTheme x:Key="{x:Type arc:StatusLamp}" TargetType="arc:StatusLamp">
        <Setter Property="Width" Value="{arc:StatusLampTokenResource Diameter}" />
        <Setter Property="Height" Value="{arc:StatusLampTokenResource Diameter}" />
        <Setter Property="Focusable" Value="False" />
        <Setter Property="Template">
            <ControlTemplate TargetType="arc:StatusLamp">
                <Border Name="PART_Lamp"
                        CornerRadius="{arc:ArcDesignTokenResource RadiusPill}"
                        Background="{arc:ArcDesignTokenResource TextMuted}" />
            </ControlTemplate>
        </Setter>
        <Style Selector="^:active /template/ Border#PART_Lamp">
            <Setter Property="Background" Value="{arc:ArcDesignTokenResource Success}" />
        </Style>
    </ControlTheme>
</ResourceDictionary>
```

这个骨架没有动效，因此不需要 Motion.Tracks。正式交付还需明确状态名称、无障碍语义、是否提供文本和内置文案，并补齐测试、Gallery、三语 API 说明与更新日志；不能只凭这些文件就认为组件完成。

本轮已将上述 C# 和 XAML 提取到临时项目，复用当前 Core 与 Foundation 生成器进行 net10.0 编译：0 个警告、0 个错误。该检查确认示例的语法、XAML 与生成器集成，不等同于正式组件的运行时验收。指南中的 53 个本地源码链接及行号范围均已检查有效。

## 13. 开发顺序与针对性验收

1. 确认已有控件能否满足需求，写清公共属性、默认值、边界、状态和事件触发范围。
2. 选目录与基类，检查跨包 Token 身份，决定哪些参数进入 Token。
3. 实现控件、Token 与主题，处理资源变化、模板重复应用和挂载生命周期。
4. 为真实行为补针对性测试，再添加 Gallery 目录、演示、三语文档和 API 翻译。
5. 只构建受影响项目，运行相关测试；保存真实结果，审核差异与快照。

建议测试内容按组件需要选择，不机械套用全部项目：

| 范围 | 有价值的检查 |
| --- | --- |
| 数值仪表 | 正常值、边界、越界、无效量程、非有限值、阈值精确边界 |
| 状态与动效 | 真实状态是否立即更新、Full/Reduced/Off、连续改目标 |
| 外观资源 | 深浅色、强调色、字体、主题切换；几何是否随 Token 更新 |
| 生命周期 | 重挂载读取新值；外部集合仍存活时控件能否回收 |
| 项目与选择 | 禁用/隐藏、键盘导航、重复点击、代码与绑定改变选中值 |
| 无障碍与布局 | Provider 语义、名称、帮助文字、小尺寸与无穷测量尺寸 |
| Gallery | 三语文档、演示引用、XAML 片段、API 翻译、新组件日期与目录 |

GaugeTests、ThermometerTests 和 GalleryDocsTests 都有上述真实行为的参考。动效可用 ManualFrameClock，避免靠长时间睡眠等待动画。[E11、E15、E16、E21]

字重回归可以记录 DrawingGroup 中的实际 GlyphRun，检查主题覆盖后重建的文字是否采用目标字重。测试要使用测试字体提供的字形；不支持的 Light 等字重可能被字体系统替代，不能把这种替代直接判断为资源绑定失效。温度计测试使用常规与粗体交叉覆盖标题/读数和状态/刻度，且先绘制一次以覆盖已有缓存的场景。

### 13.1 仓库标准入口

下面命令假定安装了 global.json 对应 SDK；从两个仓库根分别执行。`<Component>` 替换成实际组件名，多个检查用 OR 组合。

```powershell
dotnet build "src/TKCode.Arc" -c Debug
dotnet test "tests/TKCode.Arc.Tests" --filter "FullyQualifiedName~<Component>Tests"
```

修改 Token 时，还应运行对应主题令牌快照检查。快照更新使用 `ARC_UPDATE_BASELINES=1`，更新完应清除该环境变量；逐项核对差异，不能把任意差异自动当成预期。基础生成器快照采用另一个变量 `ARC_UPDATE_SNAPSHOTS=1`。[E24、E26]

Gallery：

```powershell
$env:ARC_ROOT = "D:\Local\工控系统开发\界面\Tkcode\TKCode.Arc"
dotnet build "gallery/TKCode.Arc.Gallery.Desktop" -c Debug
dotnet test "tests/TKCode.Arc.Gallery.Tests" --filter "FullyQualifiedName~GalleryDocsTests|FullyQualifiedName~GalleryDemosTests|FullyQualifiedName~GalleryNewBadgeTests|FullyQualifiedName~GallerySmokeTests.Catalog_Lists_All_Components"
```

增加组件自己的 Gallery 测试时，在上述过滤器中补上它。若标准入口在发现阶段挂住，可按 Gallery AGENTS.md 直接运行已构建测试程序，以 `-class` 或 `-method` 选同一组检查；不要混用导致意外求交集、少跑测试。

**明文规则：** 普通组件改动不跑全套；跨 Foundation、Core、生成器、基础主题的变更需要按影响面扩大验证，或在明确要求时运行全套。[E01]

### 13.2 当前工作区的构建设置

这部分是本地环境事实，不是上游通用规范：

- SDK 位于 `%TEMP%/TKCode-Arc-SDK/dotnet.exe`，版本为 `11.0.100-rc.2.26504.105`；默认库目标是 net8.0/net10.0/net11.0，测试默认 net10.0。
- 已验证的本地构建使用 `-p:ArcTargetFrameworks=net10.0`，不能因此声称完成所有目标框架验证。
- 缓存用 `--artifacts-path "$env:TEMP\TKCode-Gallery-artifacts"`；`-p:EmitCompilerGeneratedFiles=false` 只关闭生成代码的磁盘副本，不关闭生成器。
- 顺序复用依赖构建有助于避免两套缓存同时编译全部库。`-m:1` 是本地资源控制选项，不是组件正确性规则。
- 外置测试输出时，部分测试从程序集上级目录查找 `.slnx` 与源码。当前临时输出通过源码目录连接满足该约定；没有这些路径时，源码读取测试可能失败，不能简单归因为组件错误。[E02、E26]

**编辑器依赖配置（2026-11-17）：** 系统默认 dotnet 只有 .NET 9 SDK，而核心 global.json 请求上述 .NET 11 SDK。工作区 `.vscode/settings.json` 使用已安装 C# 扩展支持的 `dotnet.dotnetPath` 指定语言服务宿主目录、`omnisharp.sdkPath` 指定 SDK 子目录，允许预览 SDK；新建集成终端通过 DOTNET_ROOT/PATH 使用相同 SDK，并设置 ARC_ROOT。此前 `--artifacts-path` 的还原结果位于临时输出，不能代替编辑器默认 `obj/project.assets.json`；已单独为主控件库及 Foundation/Core/生成器引用链执行默认路径还原。遇到 using Avalonia 无法解析时先核对 SDK、资产文件和项目加载，而非重复添加已有的 Avalonia 引用。旧语言服务仍保留诊断时，执行 OmniSharp 重启以重新读取设置。配置中的绝对路径属于当前机器，不是发布依赖。

该次修复已在默认 bin/obj 路径执行主控件库 Debug 构建，net8.0、net10.0、net11.0 均通过，总计零警告、零错误；包含 Foundation、Core 和生成器引用链。此结果确认当前源码及 Avalonia 引用可编译，不等同于浏览器、移动端、全套测试或 Native AOT 发布验证，也不证明所有旧编辑器会话的诊断缓存已经刷新。

不要在自动化中修改系统 `$HOME`，也不要把本地临时 SDK 路径写进库的发布依赖。

## 14. 常见诊断与易错点

| 信号或现象 | 当前含义与处理 |
| --- | --- |
| ARCF001 | Token 属性类型不可解析；检查属性类型和可空值类型 |
| ARCF002/006 | Token 继承或类结构不符合要求 |
| ARCF003 | 空 Token 的生成器警告；AGENTS 对公共组件的要求比仅“不报编译错误”更严格 |
| ARCF004/005 | 身份冲突或多个全局身份；还要检查同时启用的其他包 |
| ARCF010/011 | 主题缺 x:Class、XML 或根结构错误 |
| ARCF012/013/014 | 类型键不可解析、资源键重复、没有带键资源 |
| ARCF020–027 | 本地化格式、缺文字、未知项、重复项、无源文件、占位符或目录问题 |
| GDOC003 | 文档译文与英文 source-hash 不一致 |
| GDOC004/008 | XML summary 缺失或没有 XML 文档到达生成器；先区分内容问题与构建输入问题 |
| GDOC005/006/007 | API 翻译过期、缺失或对应成员已经不存在 |
| GDOC010–012 | 演示引用找不到、文件不合规或英文页面未引用它 |
| GDOC030–034 | knobs 属性、类型、格式、Target 或隐藏/弃用成员不合规 |
| 主题未加载 | 检查包是否启用、Theme 文件发现模式、x:Class、资源键与 xmlns |
| 重挂载后读数或文案过期 | 检查 attach 时是否重新读取；外部订阅是否正确恢复 |
| 修改字体或主题后图形不更新 | 检查动态资源与文字/几何缓存失效 |
| 温度计满量程时圆顶留白 | 液面裁剪需覆盖完整内部轮廓，不能只计算直管长度；刻度与液面共用标定高度 |
| 页面存在却无检查覆盖 | 检查分类是否在当前测试的枚举范围内 |

已有控件的所有细节不能自动变成规范。例如 Gauge 的无效量程采用安全上限，Thermometer 则显示 InvalidRange；状态跟随动画的策略也不同。新增组件必须自己明确这些行为，而不是从一个参考控件复制全部默认语义。

## 15. 兼容性、发布与仍无法还原的部分

**明文规则：** 遵循 SemVer；已发布公共成员不能一步删除或重命名。旧成员保留、标记 Obsolete 并转发，CHANGELOG 记录迁移；只有新主版本且至少经历两次稳定发布的弃用期后才能删除。默认值、参数与事件意义改变也属于 API 兼容性范围。[E01、E12]

新增组件在 CHANGELOG 的合适未发布段落记录。版本源位于 `build/Versions.props`，不要为日常组件开发随意更改版本。发布先经历同编号 preview，再发布 stable。未经明确请求不创建 Git 提交或推送。

**上游差异记录（2026-11-17）：** main 的未发布拆包说明明确表示这是有意发生在 1.x 内的破坏性变更：程序集归属改变，需要重新编译，代码高亮还需要新包注册。这与 AGENTS.md 的已发布 API 兼容性明文规则存在冲突。本次同步保留上游实现和迁移说明，为 Gallery 适配新包；此例外不是后续本地组件的兼容性豁免，也不能把重新编译通过称为二进制兼容。

本指南可以可靠重建目录、生成器、主题、动效、生命周期、文档与测试流程，但不能还原：

- 原手册的原始措辞、完整章节和设计决策的来龙去脉。
- 缺失 design 文档中的完整 Token 保留名称表、视觉规范和未来路线图。
- 上游未在源码或测试中表达的性能预算、跨平台验收矩阵与组件特有交互要求。
- Native AOT、浏览器、移动端和全部目标框架的发布成功状态。

研发时以当前指南启动；若以后拿到上游手册，逐条比对并修订，保留差异记录。需要新的业务规则时，将其标为本项目决策，不能伪装成“原手册要求”。

## 证据索引

以下链接指向本地源码。行号以本次研究工作区为准，未来修改后可能移动；查找同名类或方法即可重新定位。

| 编号 | 主要依据 |
| --- | --- |
| E01 | [核心明文规范](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/.agents/AGENTS.md)；[核心规范入口](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/AGENTS.md)；[Gallery AGENTS.md](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/AGENTS.md) |
| E02 | [ProjectDefaults.props](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/build/ProjectDefaults.props)；[Arc.Repository.targets](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/build/Arc.Repository.targets)；[主控件项目](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/TKCode.Arc.csproj) |
| E03 | [ArcFoundationGenerator](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation.Generators/ArcFoundationGenerator.cs#L25)；[Foundation 资源接入 targets](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation/buildTransitive/TKCode.Arc.Foundation.targets) |
| E04 | [TokenModel 声明与分类](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation.Generators/TokenModel.cs#L114)；[ARCF 诊断](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation.Generators/Diagnostics.cs) |
| E05 | [ThemeFile 发现与解析](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation.Generators/ThemeModel.cs#L82) |
| E06 | [Token 计算约束](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation/Tokens/AbstractControlDesignToken.cs)；[计算与覆盖顺序](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation/Theme/ArcTokenCalculator.cs)；[Apply/ApplyAsync](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation/Theme/ArcThemeManager.cs#L102-L142) |
| E07 | [ArcBuilder 与回调](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation/Registration/ArcBuilder.cs)；[ArcRuntime 跨包检查](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation/Registration/ArcRuntime.cs#L220)；[UseArcControls](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/ThemeManagerBuilderExtensions.cs) |
| E08 | [ArcMotion 策略](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Motion/ArcMotion.cs#L11-L104) |
| E09 | [ArcTokens 动态绑定与读取](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Theme/ArcTokens.cs) |
| E10 | [ArcBaseTheme 按类型让位](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Theme/Base/ArcBaseTheme.cs#L14-L123) |
| E11 | [Gauge 实现](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Charts/Gauge/Gauge.cs)；[Gauge Token](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Charts/Gauge/GaugeToken.cs)；[GaugeTests](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/tests/TKCode.Arc.Tests/Charts/GaugeTests.cs) |
| E12 | [SegmentedOption](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Toggles/SegmentedControl/SegmentedOption.cs)；[模板重建](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Toggles/SegmentedControl/SegmentedControl.cs#L138)；[主题](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Toggles/SegmentedControl/Themes/SegmentedControlTheme.axaml)；[选择绑定校验](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Forms/_Shared/FormFieldHost.cs#L125) |
| E13 | [ObservedItems](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Services/ObservedItems.cs)；[Core 内部可见程序集](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/TKCode.Arc.Core.csproj) |
| E14 | [ChartsBPalette 资源跟踪和缓存](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Charts/_SharedB/ChartsBPalette.cs) |
| E15 | [MotionValue](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Motion/MotionValue.cs#L101)；[MotionScheduler 与测试时钟](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Motion/MotionValue.cs#L362)；[LiveLoop](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Motion/LiveLoop.cs) |
| E16 | [本地新增 Thermometer](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/CustomCharts/Thermometer/Thermometer.cs)；[温度计行为测试](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/tests/TKCode.Arc.Tests/CustomCharts/ThermometerTests.cs) |
| E17 | [Sidebar 事件语义](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Blocks/Blocks/AppShell/Sidebar/Sidebar.cs#L144-L154) |
| E18 | [LocalizationModel](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation.Generators/LocalizationModel.cs)；[本地化诊断](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Foundation.Generators/LocalizationDiagnostics.cs)；[温度计语言目录](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/CustomCharts/Thermometer/Localization/ThermometerLangResourceKind.cs) |
| E19 | [Button 模板与性能说明](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc/Buttons/Button/Themes/ButtonTheme.axaml)；[FocusModality](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/src/TKCode.Arc.Core/Primitives/FocusModality.cs) |
| E20 | [GalleryCatalog](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/gallery/TKCode.Arc.Gallery/Catalog/GalleryCatalog.cs)；[AddedOn 检查](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/tests/TKCode.Arc.Gallery.Tests/Gallery/GalleryNewBadgeTests.cs) |
| E21 | [GalleryDocsTests](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/tests/TKCode.Arc.Gallery.Tests/Gallery/GalleryDocsTests.cs#L107-L181)；[GalleryDemosTests](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/tests/TKCode.Arc.Gallery.Tests/Gallery/GalleryDemosTests.cs) |
| E22 | [DocPage 解析及哈希](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/gallery/TKCode.Arc.Gallery.Generators/DocPage.cs#L206-L219) |
| E23 | [GDOC 诊断](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/gallery/TKCode.Arc.Gallery.Generators/GalleryDocsDiagnostics.cs)；[KnobFile 语法](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/gallery/TKCode.Arc.Gallery.Generators/KnobFile.cs#L40-L64) |
| E24 | [生成器样例与快照测试](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/tests/TKCode.Arc.Foundation.Tests/GeneratorTests.cs#L27-L92)；[生成令牌类型快照](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/tests/TKCode.Arc.Foundation.Tests/Snapshots/DemoButtonToken.g.cs.txt)；[诊断快照](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/tests/TKCode.Arc.Foundation.Tests/Snapshots/Diagnostics.txt) |
| E25 | [DocShots 使用约定](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/tools/docshots/Program.cs#L10-L21) |
| E26 | [TokenBaselineTests](file:///D:/Local/工控系统开发/界面/Tkcode/TKCode.Arc/tests/TKCode.Arc.Tests/Theme/TokenBaseline/TokenBaselineTests.cs#L22-L98)；[外置输出源码定位](file:///D:/Local/工控系统开发/界面/Tkcode/ArcGallery/tests/TKCode.Arc.Gallery.Tests/Gallery/RepositoryPaths.cs) |
