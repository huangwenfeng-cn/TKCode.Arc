---
name: tkcode-git-delivery
description: 按个人仓库约定准备提交信息、PR 或问题回复。用户明确要求相关 Git 交付时使用；只写提交信息或回复草稿不授权执行提交或发送。
---

# Git 与协作交付

按当前工具的 Git 安全规程操作。本技能补充项目目的地与语义边界，不替代工具要求。

| 项目 | 用户指定的个人仓库 |
| --- | --- |
| TKCode.Arc | https://github.com/huangwenfeng-cn/TKCode.Arc |
| ArcGallery | https://github.com/huangwenfeng-cn/ArcGallery |

- 用户明确禁止向 AIDotNet 原作者仓库推送。每次写远端前核对目的 URL 和目标分支，不能直接信任 origin 名称；不默认写 main。
- 本工作区提交身份为 `Vinceng Huang`、`huangwenfeng-cn@163.com`。需要时用进程环境变量指定，不修改 git config。
- 写提交信息时读取全部 staged diff、status 和最近提交风格；没有暂存内容时说明范围，不能擅自暂存或提交。
- 明确要求提交时按文件名暂存并核对全部暂存内容，不纳入凭据或临时证据。消息说明目的，使用当前 shell 支持的 HEREDOC；新建提交，不擅自 amend。
- 不跳 hooks、签名或检查。失败后修复并创建新提交；不自动使用强推、硬重置或删除分支。
- 推送必须已获授权；两个仓库分别交付，核对远端引用与本地提交。创建 PR 时检查整个分支相对 base 的变化和验证，遵循当前 GitHub 工具要求。
- 用户只要求 PR 描述或问题回复时生成草稿。实际创建 PR、发布评论、发送消息或邮件依据各自授权，不能把“msg”或“写回复”解释为自动执行。
- 问题回复只包含已证实的行为、修复、验证和可用 workaround；不声称已发布或已部署尚未交付的内容。

引用上游问题、PR 或提交只作事实依据，不把原作者仓库变成写入目的地。遇到远端或身份冲突，先完成可审核的本地准备，再明确报告所缺决策。
