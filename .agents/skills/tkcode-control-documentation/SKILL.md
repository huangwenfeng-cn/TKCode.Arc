---
name: tkcode-control-documentation
description: 创建或同步 TKCode.Arc 组件与 ArcGallery 文档。新增控件、修改公共 API、Token、主题、演示或维护指南时使用。
---

# 组件与 Gallery 文档

先读 [明文规范](../../AGENTS.md)、[实施指南](../../implementation-guide.md) 第 9、11、15 节和 `../ArcGallery/AGENTS.md`。路径从核心仓库根计算。

1. 建立事实清单：所属包、平坦命名空间、公共 API、默认值、Token 单位、主题、状态、生命周期、动效、无障碍、演示和已有测试。规则与源码不一致时明确记录，不把旧例外提升为规范。
2. 面向使用者的文档放在 `../ArcGallery/gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/{en-US,zh-CN,zh-TW}.md`。更新已有页面优先；不复制 AtomUI 的 overview/implementation/token/changelog 四文件体系。
3. 英文页面按仓库测试要求保留八个二级章节；三语保持章节对应、代码块类型和数量及演示引用一致。
4. 公共属性与事件在核心源码提供 XML summary。更新简体、繁体 `Docs/Api/*.xlf` 的 source 和 target，不抑制 GDOC005/006 来隐藏过期或缺译。
5. 英文源文变动后实际审核译文，再按指南中的 LF 归一化、TrimEnd 和 FNV-1a 64 位算法更新 source-hash。
6. 更新 Demos、knobs、目录、包归属和 AddedOn。CustomCharts 采用本地 Extras 登记方式，不能给自研组件生成不存在的原版链接。
7. Markdown/聊天组件与高亮分别注册 `UseArcMarkdown()`、`UseArcHighlighting()`；按当前程序集确认安装包名，避免旧拆包前说明。
8. 截图引用保持 png 标识、实际 WebP 资源约定；截图缺失和视口外未加载要分别核对。使用 `tools/docshots` 时记录捕获模式和范围。
9. 架构事实更新实施指南；历史和迁移写 CHANGELOG 或已有交付说明，不把计划和未执行验证写成当前事实。

文档改动只查格式、链接、三语一致性和源文哈希。Gallery 代码、API 或主题变动再构建 Desktop 并运行约定的文档、演示、New 标记与目录定向测试；不默认全量。

```powershell
git diff --check
```

交付列出实际修改及验证。无反射声明不等于 Native AOT 已发布验证，桌面构建也不等于浏览器或移动端已验收。
