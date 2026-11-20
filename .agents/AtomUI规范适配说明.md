# AtomUI 技能规范适配说明

评估日期：2026-11-17。

结论：工作流程可以借鉴，框架约定和实现机制需要按 TKCode.Arc 重写。本次将参考目录中的 16 项技能归并为 11 项本地技能；Avalonia 11 → 12 专用迁移技能不迁入。规范正文已统一到本目录，旧路径保留入口。

## 来源与范围

- 用户指定来源：[AtomUI release/6.0 的技能目录](https://github.com/AtomUI/AtomUI/tree/release/6.0/.agents/skills)。
- 本次固定来源提交：`037129eaf36ab7f5a6761a7cb3cda2e15d2f808a`。
- [固定版本目录](https://github.com/AtomUI/AtomUI/tree/037129eaf36ab7f5a6761a7cb3cda2e15d2f808a/.agents/skills) 可用于后续对比。
- 评估读取了技能文档的用途、关键边界与相关流程；尤其核对了组件优化、性能、资源生命周期、文档、视觉验收、Gallery、升级、提交权限和发布约定。没有执行下载的脚本，没有导入 AtomUI 控件实现、反射扩展或辅助源码。
- 外部临时检出放在仓库外。此目录只保留本项目重写的规范，不是第三方技能原样安装，也不包含上游脚本副本。

这里的“参考来源”不表示 TKCode.Arc 可以依赖 AtomUI 实现。核心明文规则仍要求只使用 Avalonia 公开 API、禁止 AtomUI 包和实现源码；本次只借鉴文档中的开发流程。

## 规范位置和优先关系

| 文件 | 职责 |
| --- | --- |
| [AGENTS.md](AGENTS.md) | 原核心明文规则正文，保留已有契约并修订规范路径 |
| [implementation-guide.md](implementation-guide.md) | 本地源码重建版指南，保留明文规则、源码事实与开发建议的区分 |
| [仓库根入口](../AGENTS.md) | 保持工具自动发现 AGENTS.md，指向此目录 |
| [工作区旧指南入口](../../implementation-guide.md) | 保持既有链接有效，正文修订统一到此目录 |
| [Gallery 约定](../../ArcGallery/AGENTS.md) | 保留 Gallery 自身规则，改为引用此目录中的核心规范 |
| `skills/<name>/SKILL.md` | 在具体任务中按需读取的流程 |

技能不能覆盖用户授权、核心明文规则、所修改仓库的 AGENTS.md 或 `.editorconfig`。上游已有例外也不能豁免本地新增代码。

技能按用户指定保存在 `.agents/skills`，没有另行安装到全局目录或复制到 `.trae/skills`。核心 AGENTS.md 已列出读取入口，但保存文件不等于 IDE 已在技能路由目录中注册；是否自动出现在产品技能列表取决于该产品对 `.agents/skills` 的支持。当前可以由项目规范入口引导按需读取。

## 逐项适配

| AtomUI 原目录 | 本地处理 | 主要修改 |
| --- | --- | --- |
| `atomui-control-optimization` | [tkcode-control-work](skills/tkcode-control-work/SKILL.md) | 改为本地组件工作流程，使用平坦命名空间、Token/主题、共享原语、兼容性和定向测试；不导入成员排序脚本 |
| `atomui-control-performance` | [tkcode-control-performance](skills/tkcode-control-performance/SKILL.md) | 保留测量与正确性优先，删除外部硬阈值、未核实成本表和动态创建绝对禁令 |
| `atomui-resource-lifecycle` | [tkcode-resource-lifecycle](skills/tkcode-resource-lifecycle/SKILL.md) | 改为本地 ArcTokens、资源宿主和所有权检查，不移植 TokenResourceBinder 或特定泄漏结论 |
| `atomui-control-documentation` | [tkcode-control-documentation](skills/tkcode-control-documentation/SKILL.md) | 使用三语 Gallery Docs、XML summary、API XLF、FNV 源文哈希及实际目录结构 |
| `update-gallery-maintain-docs` | 合并到 `tkcode-control-documentation` | 移除 ReactiveUI 和 controlgallery 路径假设；按实际代码更新既有指南与文档 |
| `atomui-visual-acceptance` | [tkcode-visual-acceptance](skills/tkcode-visual-acceptance/SKILL.md) | 改为实际 Arc 宿主与 DocShots；明确 headless 渲染和原生截图的区别，不复制 macOS 采样流程 |
| `atomui-gallery-local-debug` | [tkcode-gallery-local-preview](skills/tkcode-gallery-local-preview/SKILL.md) | 使用相邻 ArcGallery、ARC_ROOT、现有 PowerShell 入口和实际程序集/PID；不复制 bash 启动器 |
| `full-regression` | [tkcode-full-regression](skills/tkcode-full-regression/SKILL.md) | 使用本项目真实测试集合与当前后台通知机制，不复制全量脚本或集成测试排除规则 |
| `create-skeleton-app` | [tkcode-skeleton-app](skills/tkcode-skeleton-app/SKILL.md) | 使用 TKCode.Arc 模板、UseArc 和实际独立包注册 |
| `upgrade-dependencies` | [tkcode-dependency-upgrade](skills/tkcode-dependency-upgrade/SKILL.md) | 核对 Arc 版本源与完整影响面；沿用已有实施授权，资料放仓库外 |
| `version-release` | [tkcode-release](skills/tkcode-release/SKILL.md) | 使用 ArcVersion、已有 CHANGELOG、preview→stable 和弃用期，不移植外部打包门禁 |
| `changelog-collect` | 合并到 `tkcode-release` | 日常日志与实际发布分开，按用户可见变化及现有格式整理 |
| `commit-msg` | [tkcode-git-delivery](skills/tkcode-git-delivery/SKILL.md) | 明确“写消息”不授权提交；核对暂存区、身份、个人仓库和目标分支 |
| `create-pr` | 合并到 `tkcode-git-delivery` | 核对整个分支范围，PR 描述只报告实际行为和验证；写草稿与创建区分 |
| `issue-reply` | 合并到 `tkcode-git-delivery` | 保留基于事实的回复流程，起草不自动发布评论或发送消息 |
| `migrate-to-avalonia12` | 不迁入 | 当前项目已使用 Avalonia 12.1.5；其反射、内部 API 提取和广泛关键词触发与本项目规则不兼容 |

## 不能直接套用的要求

1. **包与类型体系不同。** 本项目使用 Foundation → Core → 主控件及独立包，控件命名空间为 `TKCode.Arc`。不能通过字符串替换把 AtomUI.Desktop.Controls、其注册器或平台层变成本地 API。
2. **公开 API 与 AOT 边界不同。** 外部迁移技能包含 ReflectionExtensions 和内部成员访问；本地继续禁止反射及私有框架 API。所谓“AOT-safe reflection”不是本项目豁免。
3. **资源机制必须以本地实现为准。** `ArcTokens.Bind` 返回 IDisposable，但清理方式由真实所有权决定。不能将外部所有对象的强制接口、全局绑定器或 Gallery 对象数量结论套到本项目。
4. **模板策略需要事实与测量。** 保留主题优先和避免重复模板状态机的建议，不采用“所有模板元素禁止代码创建”“超过固定实例数才可优化”等外部绝对规则。本地 Gauge、自绘仪表和共享原语有各自职责。
5. **文档结构不同。** 不建立外部的 docs/controls 四文件体系或维护文档目录。采用当前三语页面、API 翻译、目录标记与完整性测试。
6. **平台和捕获模式不同。** 不导入 macOS `screencapture`、NSWindow、bundle ID 和外部 bash launcher；DocShots 实际为 headless 绘图，不能冒充操作系统截图。
7. **Git 权限更明确。** 上游 commit-msg 将消息生成与整个提交流程合并，本地拆开授权；仅向用户指定的个人仓库交付，不向原作者仓库推送，不默认修改 main。
8. **审批和全量验证不能机械继承。** 保留分析、证据和范围控制，移除“已授权后仍反复批准”的门槛。普通组件按定向测试；全量、tag、发布等依据明确任务与授权执行。

## 本次验证

本次属于规范文件整理与技能文档重写，没有修改控件、Token、主题、项目引用或业务代码。验证包括：

- 两份规范正文迁移后保留原有规则与指南章节，旧路径入口和 Gallery 引用一致。
- 11 项技能的目录名与 frontmatter name 对齐，description 为中文单行，正文非空。
- 核对本地 Markdown 链接、文件存在性、技能交叉引用和文件末尾换行。
- 检查 Git 忽略规则，规范正文、说明和 skills 可被跟踪，其他本地 `.agents` 文件仍忽略。
- 检查差异格式和文档中的旧外部 API/路径，外部名称只作为差异或禁止事项说明。

实际校验结果：原明文规则及 Verify/Gallery 契约仅发生指南路径修订，指南保留 15 章，11 项技能通过 YAML 格式与目录名检查，18 份相关文档的 93 个真实本地链接有效；两个仓库 `git diff --check` 通过。验证脚本放在系统临时目录，未加入项目。

没有新增或执行来源脚本，没有进行技能所描述的控件优化、依赖升级、全量测试、原生视觉验收、Git 提交或推送。此前的 152 项回归属于组件同步阶段，不计作本次规范迁移测试。
