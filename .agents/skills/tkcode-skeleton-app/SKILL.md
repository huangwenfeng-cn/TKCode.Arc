---
name: tkcode-skeleton-app
description: 创建使用 TKCode.Arc 的最小应用或复现工程。用户要求可运行样例、骨架应用或独立控件复现时使用。
---

# 最小应用与复现工程

先读 [明文规范](../../AGENTS.md)、[实施指南](../../implementation-guide.md) 第 3、6、13 节及仓库 `templates/TKCode.Arc.Templates` 中的同类模板。

1. 从任务确定应用目的、平台、目标框架和交付位置。临时探针在仓库外；用户要求保存的样例放其指定路径或工作区交付目录。
2. 复用当前项目配置和入口模式，包含应用、窗口或视图、Arc 注册及一到两个相关控件；不无故加入服务、路由或 MVVM 框架。
3. 测试本地修改时 ProjectReference 指向当前核心仓库；复现已发布包时记录准确包版本，不能混用两套程序集。
4. 在 `UseArc(...)` 中配置相应包和语言，不在应用平台初始化前创建需要平台服务的对象。
5. 控件引用保持 `TKCode.Arc` 和既有 XAML xmlns。Markdown、聊天、高亮及其他独立包按实际需要注册。
6. 带 popup 的复现选择当前支持的宿主并验证资源与弹出行为；不继承 AtomUI 的专用窗口要求。
7. 保留原问题的尺寸、状态、数据和默认动效，避免通过简化条件掩盖问题。
8. 构建目标项目并实际运行需要运行验证的场景，记录入口和所用程序集；不能只给代码片段而称为可运行交付。

不安装 AtomUI 包，不复制其 ApplicationStartup、IAtomUIBuilder、ReflectionExtensions 或内部 API。清理临时工程时保留用户需要的证据，只停止已核对身份的本次进程。
