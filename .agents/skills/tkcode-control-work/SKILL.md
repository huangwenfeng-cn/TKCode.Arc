---
name: tkcode-control-work
description: 按 TKCode.Arc 规范开发、修复或重构控件。用户要求新增组件、修改行为或整体优化控件时使用；不替代通用 PR 审查技能。
---

# 控件开发与修改

先读 [明文规范](../../AGENTS.md) 和 [实施指南](../../implementation-guide.md) 的相关章节。以下路径从核心仓库根计算，Gallery 为相邻的 `../ArcGallery`。

1. 读取目标源码、Token、主题、测试和同类实现；先检查已有控件或共享原语能否满足需求。
2. 对非简单改动列出公共属性与默认值、状态所有者、事件时机、模板部件、伪类、资源键、生命周期及 Gallery 影响。整体优化先形成简短事实清单，再按已授权范围实施；不重复索要已有授权。
3. 保持平坦命名空间 `TKCode.Arc`。本地新图表和工业展示控件暂时使用 `CustomCharts`，物理目录不生成新的命名空间或程序集。
4. 复用 Foundation、Core、Token、主题和动效机制。用主题表达模板状态；自绘或动态目标需要代码时说明理由。
5. 公共控件提供 `[ControlDesignToken]` Token；颜色、圆角、阴影、字体和字重跟随 ArcDesign。测量、布局与绘制采用同一几何令牌语义。
6. 检查边界输入、模板重建、重新挂载、缓存失效、语言、无障碍和 Reduced/Off。设备真实状态立即更新，视觉动画不能延迟真实告警。
7. 保留已发布公共 API 的兼容转发和弃用期。未发布新组件的初始 API 可收敛；不能借 preview 改动已发布契约。
8. 按问题类型读取同级 `tkcode-resource-lifecycle`、`tkcode-control-performance` 或 `tkcode-control-documentation` 技能。
9. 运行影响范围内的构建和定向测试，检查规范与差异；测试通过不能代替规范核对。

```powershell
dotnet build "src/TKCode.Arc" -c Debug
dotnet test "tests/TKCode.Arc.Tests" --filter "FullyQualifiedName~<Component>Tests"
git diff --check
```

先核对 `global.json` 与目标框架，按实际环境调整上述命令。只修改文档或简单可逆文字时，执行文档和格式检查即可。

不要为文件长度强制拆分 partial，也不要导入 AtomUI 的成员区域布局脚本。拆分必须有稳定职责，并保留易查找的公共契约和生命周期入口。新增辅助类、字段和同步标志应有明确所有者和用途。

交付时说明行为变化、兼容性、实际验证和未验证范围；仅在用户明确要求时提交或推送。
