---
name: tkcode-gallery-local-preview
description: 构建并确认本地 TKCode.Arc 对应的 ArcGallery 预览。用户要求启动 Gallery、查看控件或核对运行程序集时使用。
---

# 本地 Gallery 预览

先读 [明文规范](../../AGENTS.md) 的 Gallery 部分、[实施指南](../../implementation-guide.md) 第 13 节和 `../ArcGallery/AGENTS.md`。

1. 解析两个仓库的绝对路径、`global.json`、SDK 和 `ArcRoot`。环境变量 `ARC_ROOT` 指向当前 TKCode.Arc 源码，不假定旁边任意同名目录就是当前版本。
2. 构建 `../ArcGallery/gallery/TKCode.Arc.Gallery.Desktop/TKCode.Arc.Gallery.Desktop.csproj`。依赖或拆包变动时先构建完整引用；只复用已经核验的依赖，避免旧 DLL。
3. 优先使用工作区已有 `启动框架预览.ps1`；启动前核对它仍指向当前仓库和实际输出。也可用当前 SDK 直接启动本次生成的 Desktop DLL。
4. 记录启动命令、命令工具返回的会话标识、PID、入口路径、配置及核心/Gallery 程序集哈希。窗口标题只作辅助，不能识别程序集版本。
5. 确认窗口响应、指定控件内容及当前主题。使用自动化时先读取页面或窗口结构，再操作明确目标。
6. 重建前需要关闭预览时，仅停止本次创建或已核验路径的进程，不按同名进程批量结束。
7. 明确用户要求的是打开预览还是临时验收；需要保留的窗口不要在结束时自动关闭。

不使用 AtomUIGallery 的目录、macOS bundle 标识、`open` 或其启动脚本。不假定输出一定是 `.artifacts/bin/Debug/net10.0`；标准构建、外置 artifacts 与 `dotnet run` 的布局分别核对。

只有实际窗口及对应程序集核验完成，才报告预览已启动；构建成功与视觉验收分别汇报。
