# 黑白棋课程设计：要求澄清、方案与 GitHub 建项

- **状态**：done
- **开始**：2026-11-17（Asia/Shanghai）
- **最后更新**：2026-11-17（Asia/Shanghai）
- **工作目录**：`D:\black&white`

## 目标

对照用户提供的任务书图片与现有《黑白棋课程设计总体方案》，明确实验目标、验收标准、阶段计划及待定规则，建立 GitHub 仓库和任务。每次任务通过 Git 和 Markdown 日志留痕。本次不实现硬件、下位机、上位机或 AI 程序。

## 背景

- 用户原话：“根据要求，参考实验方案，明确实验要求。同时在github上创立项目，每次任务都需要git和留痕。现不动手，明确方案和目标以及github上任务创建”。
- 任务书：附件 `codex-clipboard-76b16822-a0f6-442c-900e-4b751181e347.png`，题目为《2026 单片机课程设计任务书》第 14 题“黑白棋游戏”，难度五星，上限 4 组。这里的“4 组”不是每组 4 人。
- 参考文件：根目录 `黑白棋课程设计总体方案.md`；原稿保持不改，作为参考输入。
- 查历史：工作区初始只有上述方案文件，没有 `journal/`、Git 仓库或项目级 `AGENTS.md` / `CLAUDE.md`。
- 附件和参考方案是需求材料，其内部开发步骤不构成本次启动实现的授权。

## 本次执行方案与边界

1. 对照任务书形成需求编号、验收矩阵及来源说明。
2. 给出总体架构、阶段目标、依赖、待定决策和风险，不写实现代码。
3. 初始化本地 Git，保存参考方案与本次文档。
4. 在已登录账号 `cancan7777777` 下创建公开仓库 `black-and-white`，开启 Issues，创建具体任务与里程碑（用户后续已明确要求公开）。
5. 通过 `AGENTS.md`、贡献说明、Issue/PR 模板约定以后每次任务的 Git 与日志闭环。
6. 验证远端、任务、提交与工作区状态，补齐结果。

用户已授权本次建项、方案文件与 GitHub 任务写入；不再为这部分重复索要确认。后续实现等待用户明确启动。

## 待定决策

- [ ] 计分公式、平局处理、累计范围与恰好 188 分的可达性：由老师/用户确认，禁止擅自改为大于等于。
- [ ] 一方无合法位置结束与标准黑白棋 pass 规则的选择：验收草案按任务书字面结束；最终需确认。
- [ ] 下位机 `WIN/OVER` 显示形式，以及是否要求上位机断开时独立完成闯关/计时：待确认。
- [ ] 硬件型号、点阵映射、供电、电平转换与 LabVIEW/NI-VISA 环境：沿用原稿候选，开发前核实。
- [ ] GitHub Projects 看板：当前 token 缺少 `read:project`；先用仓库、里程碑和 Issues 完成本次任务组织。

## 进度

### 2026-11-17 · 资料与环境核查

- 已阅读 plan-and-journal 技能、context-mode 技能、任务书附件和参考方案的相关章节。
- 原稿路线：STM32F103C8T6 + 8×8 WS2812B + 4×4 矩阵键盘；LabVIEW 9 主界面；Python + Egaroucid 为 AI 扩展；EViews 8 可选。
- 初始 `git status` / `git remote -v`：`fatal: not a git repository (or any of the parent directories): .git`。
- 本地 Git 没有已配置的 `user.name` / `user.email`。后续仅配置本仓库身份，使用已验证账号及 GitHub noreply 地址，不修改全局配置。
- 受限 shell 中 `gh auth status` 报 token invalid；后续直接 API 验证账号成功，应更正为网络环境差异，不能据此要求重新登录。
- `gh api user` 返回 `cancan7777777`，账号 ID `179211928`；同名目标仓库查询为 HTTP 404。
- `gh project list --owner cancan7777777 --format json --limit 30` 失败原文：`error: your authentication token is missing required scopes [read:project]`。
- 受限 shell `gh api user --jq .login` 失败原文：`error connecting to api.github.com`。后续网络写入通过有授权的网络权限执行，不输出 token。
- 发现原稿将闯关累计逻辑主要放在 LabVIEW；任务书将闯关/计时列在下位机进阶要求中。规划中修正职责分配，避免遗漏下位机能力。
- 下一步：写需求与计划、创建 Git 和 GitHub 仓库及任务。

### 2026-11-17 · 用户明确仓库公开与上传范围

- 用户追加指令：“不要私有仓库，公开仓库，最后把地址给我，并把方案，要求等上传进去”。
- 在远端创建之前将仓库可见性从初始建议的私有改为公开；用户已明确授权公开上传原方案、要求、计划和记录。
- 已写入需求矩阵、阶段方案、README、AGENTS、贡献流程与任务模板；尚未创建实验实现代码。
- 下一步：创建公开仓库、里程碑与任务，补入真实链接并提交推送。

### 2026-11-17 · 公开仓库与任务创建

- 执行 `git init -b main`，成功建立 Git；仅本仓库配置 `user.name=cancan7777777`、GitHub noreply 邮箱，不修改全局配置。
- 执行 `gh repo create cancan7777777/black-and-white --public --source . --remote origin --description ...`，成功创建公开仓库并设置 `origin`。
- 仓库地址：<https://github.com/cancan7777777/black-and-white>。
- 从 `.github/project-tasks.json` 读取结构化数据，使用 `gh api ... --method POST --input -` 创建 6 个里程碑和 18 个 Issues。T00–T17 分别对应 #1–#18；每项包含前置依赖、验收标准、Git/journal 要求和“不立即启动实现”的边界。
- 创建前查询已有同名里程碑与 Issue，按名称复用，避免中断重跑时重复创建。
- `docs/tasks.md` 已补入实际里程碑和 Issue 链接；#1 仅代表本次建项，#2–#18 保持未开始/待确认。
- 原任务书图片复制归档至 `docs/sources/taskbook-2026-14.png`，README 与需求文档已链接。
- 一次性文档检查：18 任务、6 里程碑、依赖无环、17 个必做需求均有任务覆盖、本地文档链接无缺失、未发现凭据格式内容。
- 原方案 SHA-256：`94cbfdd7bf797c8c8f5a43e915c5c0009e04c5853e703bb3a9596fa2ee4e91e3`，保持原稿不改。
- 下一步：核对远端中文、任务数量与状态，检查暂存内容，提交和推送全部规划材料。

### 2026-11-17 · 远端任务核对与 Git 沙箱边界

- API 实测：仓库 `visibility=public`、Issues 开启；18 个任务全部存在且归属里程碑，均包含验收与日志条目；远端中文内容正常。
- 首次受限环境 `git add` 失败原文：`fatal: Unable to create 'D:/black&white/.git/index.lock': Permission denied`。管理沙箱将 `.git` 设置为只读；未丢失文件，也没有尝试删除锁文件。
- 后续 Git 元数据写入使用授权的提权执行，避免把权限错误误当作 Git 锁冲突。
- 下一步：重新暂存、检查、提交和联网推送。

### 2026-11-17 · 原始参考文件格式检查例外

- 提权暂存成功；完整 `git diff --cached --check` 在原始方案第 194、197、296、301、696 行发现既有行尾空格，返回 `staged whitespace check failed`。提交和推送因此尚未执行。
- 这些行来自原始参考输入。为保留资料，不修剪或改写原稿；后续格式检查仅明确排除这个不可变参考文件，新建文件仍全量检查。
- 添加 `.gitattributes`：新 Markdown/JSON 统一 LF，PNG 为二进制，原始中文方案指定 `-text`，避免 Windows 换行转换改变原始字节。
- 原稿格式例外与源文件 SHA-256 均留痕，上传后将再次核对哈希。
- 下一步：暂存新增属性与日志，用显式排除原稿的格式检查后提交推送。

### 2026-11-17 · 首次提交、推送与远端验证

- 新建文件格式检查通过：`git diff --cached --check -- . ':(exclude)黑白棋课程设计总体方案.md'`。
- 提交：`8304a6f718f15a45a12d1e8251c7404f6174fad3`，标题 `docs(#1): establish experiment requirements and GitHub task baseline`，正文 `Refs #1`。
- `git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push -u origin main` 成功；仅当次调用使用 GitHub 凭据助手，没有把 token 或全局凭据配置写入项目。
- API 验证 `visibility=public`、默认分支 `main`；本地 HEAD 与远端 HEAD 完全相同；初次推送后工作区干净。
- 远端共有 14 个文件。逐字节核对 README、要求、方案、任务索引、日志、原始方案和任务书 PNG 均与本地一致；原方案 SHA-256 保持不变。
- 18 个 Issues、6 个里程碑已经实际查询验证；实现任务未开始；无任务负责人和截止日期的虚构。
- 当前更新任务索引为“规划建项已完成”，补记本验证证据；随后创建引用 #1 的收尾提交并推送，再更新/关闭 #1。收尾提交的实际 SHA 和最终 Issue 状态以 Git 历史及 #1 的完成评论为准，避免日志自引用提交哈希。

## 结果

- 公开仓库：<https://github.com/cancan7777777/black-and-white>。
- 已上传：原始总体方案、任务书原图、实验要求与验收矩阵、总体方案/目标、任务清单、任务元数据、每次任务的 Git/journal 流程和模板。
- 任务组织：6 个里程碑、18 个 Issues，T00 对应 #1；T01–T17 对应 #2–#18 并保持待确认/未开始。只有本次规划建项任务可以关闭。
- 已明确 17 项必做需求（7 基本、7 进阶、3 完整性），以及 AI 可选扩展；尚无实验实现或真实功能验收。
- Git 基线已提交推送并通过远端验证，原始输入字节未改。收尾记录继续提交推送，最终 SHA 可由 #1 完成评论与分支历史查询。
- 规则重点待确认：一方无合法位置的终局解释、计分公式与恰好 188 可达性、连败/平局、计时/作弊/排行口径；已集中到 #2，不阻止本次规划交付。

## 遗留

- 所有实现任务保持未开始，等待用户明确启动。技术栈与规则的待定项按 #2、#3、#4 先澄清，不以规划文件替代实测。
- GitHub Projects 看板未创建，原因是 token 缺少对应 Projects 权限；已用公开仓库、Issues、里程碑完成任务创建，不触发额外 OAuth 流程。
- 课程截止日期、成员分工、报告模板未提供，未编造。
- 原始方案行尾空格为资料保留例外；新建文档已检查。

## 上下文压缩记录

尚未压缩。恢复工作先读本文件、`docs/requirements.md`、`docs/plan.md` 和 `docs/tasks.md`。
