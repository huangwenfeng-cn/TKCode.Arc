---
name: tkcode-release
description: 整理 TKCode.Arc 更新日志并评估发布准备。用户要求收集 changelog、准备版本或检查发布就绪状态时使用；不自动发布。
---

# 更新日志与发布准备

先读 [明文规范](../../AGENTS.md) 的版本及兼容性规则和 [实施指南](../../implementation-guide.md) 第 15 节。

1. 区分日常更新日志整理与版本发布。日常组件修改只补当前未发布段，不自行改版本。
2. 发布准备确定版本与提交范围，读取已有 tag、完整范围内的历史和差异，不能只看最后一条提交。
3. 核对 `build/Versions.props` 的 `ArcVersion` 以及 SDK、Avalonia、各包、模板、README 和 Gallery 的实际版本消费方式。
4. 检查公共/受保护 API、默认值、事件、Token、主题资源、包归属、目标框架、MSBuild 和运行模式等消费者契约。`!` 提交是检查线索，未发现删除 public 行不证明兼容。
5. 按实际用户影响整理 Added、Changed、Fixed 与 Migration。保持 Unreleased 结构清晰；不要生成没有实现的能力。
6. 已发布成员保留 Obsolete 转发及兼容测试；删改受主版本和至少两次稳定发布弃用期约束。遵循同编号 preview 到 stable 的发布顺序。
7. 使用当前仓库实际存在的 CI、构建、打包或验证入口。不能引用 AtomUI 的 PackageValidation、布局 allowlist 或脚本，除非本项目另外实现并验证。
8. 发布检查覆盖声明的包、框架和平台；记录未执行范围。需要全量回归时读取同级 `tkcode-full-regression`，普通 changelog 整理不触发全量。
9. 如检查发现破坏性差异，给出证据、影响和迁移要求，不能仅用抑制项把它写成兼容。

交付包括版本范围、主要变化、兼容性结论和实际验证。提交、推送、tag、GitHub Release 和 NuGet 发布分别需要相应授权；准备发布不等于授权实际发布。
