# 基础 main.c 与后续扩展隔离

- 状态：done
- 日期：2026-11-18（Asia/Shanghai）
- Issue：#22
- 分支：task/22-basic-main
- 独立工作区：本文件所在仓库；基线 f977338。

## 用户授权与边界

用户要求参考“基础功能1”中的 main.c 编写基础 main.c，并避免后续创新功能冲突。允许本次主程序、必要适配模块、离线测试与 Git 留痕；不接线、上电、烧录或实现进阶/AI。父工作区正在做 LabVIEW，使用独立 worktree，不切换父分支、不提交或覆盖其未提交文件。

## 历史与输入

- 已回查上一任务的 CubeMX 工程、现有 OtGame/ot_* 规则和协议、HAL 初始化及 AGENTS。
- 实际参考文件：D:/black&white/基础功能部分1/main.c_黑白棋基础功能.c，199 行。其 OT_Game、OT_Init、Keypad_*、WS2812_*、OT_Protocol_* 与现有 ot_* 接口不一致，依赖模块也不存在，不能直接替换后声称可编译。
- 沿用用户指定的 C8、HSE 8 MHz/72 MHz、USART1 115200 8N1、TIM1_CH1/DMA1_CH2、既有键盘 GPIO；未知硬件映射/供电仍须实测。
- 初始父目录有 LabVIEW、压缩包和参考 main.c 等未跟踪文件，均不修改。普通 shell GitHub 读取受网络限制，后续经授权网络操作。

## 方案

1. main 只初始化、调度和统一转发 HAL 回调，保留 CubeMX USER CODE。
2. app_basic 私有持有棋盘，复用既有规则/协议；向扩展提供只读快照、多观察者和带 revision 的落子请求，拒绝回调重入。
3. app_platform 负责有界 UART 队列/拼帧、矩阵键盘扫描消抖、异步 PWM/DMA 点阵刷新；中断不执行规则或阻塞发送。
4. 明确保留基础 GET/MOVE/STATE/ERROR 协议，新增功能不能静默改语义或注册重复 HAL 回调。
5. 主机测试业务/背压/扩展隔离，Keil 编译，必要时验证 CubeMX 再生成保留应用代码与源文件，提交推送 PR。

## 进度

### 2026-11-18 · 建立隔离工作区

- 已创建 GitHub #22 与 task/22-basic-main，基线 f977338。
- 使用独立 worktree，未修改父目录 Git 分支或父任务文件。
- 下一步：编写基础主程序及其必需模块。

### 2026-11-18 · 模块实现与首次验证

- main 已接入初始化、完整帧暂存、实体键盘、状态刷新和 WFI 调度；统一转发 UART RX/TX/Error 和 TIM PWM/Error 回调。
- 规则与协议文件从既有输入原样复用；新 AppBasic 私有棋盘、快照值拷贝、4 观察者独立 slot、带 revision 落子接口和回调重入保护。
- AppPlatform 使用 256 字节 RX 环形缓冲、3 个有效 TX 帧槽、1 ms/行扫描和 20 ms 消抖、1793 半字的独立 DMA 波形缓冲；只在主循环处理规则和启停 DMA。
- Keil 实际构建通过：0 errors / 0 warnings；Code=13072、RO=416、RW=64、ZI=6872，符合 C8 存储范围。
- 主机业务测试 PASS：开局、快照隔离、背压、重复/错误/未知协议、重开 revision、多观察者/重入拒绝、完整对局、时间回绕。
- HAL 模拟测试 PASS：分段接收、超长/溢出重同步、有界 TX、消抖/长按/多键释放、DMA 缓冲在传输中不可变、尾部复位零周期、超时和 UART 错误恢复。
- 以上是主机模拟与目标编译，未当作实物验收。下一步验证 CubeMX 重生成，补齐 Git/PR 与最终证据。

### 2026-11-18 · 重生成检查与结果边界

- 两次本独立 worktree 的 CubeMX 6.3 project generate 返回 KO，提示 Error in Project Generation；load/save 均返回 OK。没有将之前的成功构建日志当作本轮重生成构建成功。
- 曾怀疑新增自定义 MDK 分组，但恢复原项目/供应库后仍复现，原因尚未确定；撤销该猜测对应的预删除分组策略，不宣称修复了重生成器。
- 生成后 main 的 USER CODE 仍保留本次应用代码，MDK 文件有生成器临时路径/重复分组变化；恢复本任务生成的项目/供应库变动，手动按 application_sources.txt 同步并重新编译。只操作本独立 worktree，不动父任务。
- 后续成功生成时，生成脚本会在既有项目修正之后调用清单同步；失败时仍保留失败状态，不隐藏 KO。重生成能力保留为明确限制，当前基础代码与扩展 API 不因此伪报实物/工具验收。
- 参考 main.c 的 SHA-256：43f13971d247e09ac37b30ab486a096004dffa6723bd8c55508d1078490085f7，原文件未改。

### 2026-11-18 · 最终交付版本复核

- 恢复失败生成器产生的无关项目/库改动后，重新按清单登记应用源；最终 Keil 构建 0 errors / 0 warnings，Code=13072、RO-data=416、RW-data=64、ZI-data=6872。
- 两套主机测试再次全部 PASS：应用/协议/观察者隔离；UART 队列、键盘消抖和 DMA 模拟。没有以旧日志替代本轮结果。
- 独立工作区仅保留 main、应用模块、原样核心副本、清单/源登记工具、测试与说明日志的必要变更。O: 已撤销，父 LabVIEW 工作区仍不被本任务修改。
- 收尾将本任务提交推送并创建基于 task/20-basic-audit-cubemx 的 PR，避免混入父任务未提交的 LabVIEW 文件；实际 SHA 与 PR 地址写入 #22 完成评论。

## 最终结果

- main.c 已从空循环变为基础初始化/统一调度/回调转发入口；参考 main 的键位保留，接口按现有 OtGame/ot_* 修正。
- 配套代码包含初始棋盘、实体/串口合法落子、状态回复与刷新、键盘消抖、异步 LED DMA、过载与错误恢复；目标构建和主机模拟通过，未实物测试。
- 未来扩展通过 API 版本、值快照、独立观察者 slot、revision 校验和唯一回调/资源归属减少冲突。接入约定见 firmware/Othello_Basic/BASIC_MAIN.md，不能无条件保证任意未来代码无冲突。
- CubeMX 6.3 本独立环境再生成返回 KO，未确认根因；保留为限制，现有项目手动同步清单后可构建。没有隐瞒生成失败。
- 没有接线、上电、烧录，也未实现 AI 或进阶模式；实际硬件映射/供电/时序和 LabVIEW 串口联调仍需验证。

### 2026-11-18 · 提交与 PR 留痕

- 实现提交：39a9f2cd690f045481e70ace000cd8e12728c67b，Refs #22。
- 已推送 task/22-basic-main，创建 PR #23：https://github.com/cancan7777777/black-and-white/pull/23；基于 task/20-basic-audit-cubemx，未自动合并。
- 提交前检查 21 个变更文件，无编译产物/凭据格式，源码清单 27 个引用均存在且不重复，规则/协议副本与原核心一致。
- 本收尾记录再次提交推送；最终 HEAD、工作区与远端同步在 #22 完成评论中记录，避免日志自引用自身提交 SHA。

### 2026-11-18 · 用户要求更新 GitHub

- 用户明确要求“更新到github”。核对现有实现提交 5cdea98c1c986924bafd378a9a3de203b76ff1a4 已在远端 task/22-basic-main，PR #23 为 OPEN，GitHub 可读取基础 main.c。
- 本地无未提交的产品改动；本次只补记上传确认并推送日志，不制造产品代码变化，不自动合并 PR。
- 代码地址：https://github.com/cancan7777777/black-and-white/blob/task/22-basic-main/firmware/Othello_Basic/Core/Src/main.c
- 本次没有操作父 LabVIEW 工作区、硬件或系统配置。

### 2026-11-18 · 用户授权安装到本地 firmware

- 用户新指令：“替换firware里的main.c”。目标确定为 D:/black&white/firmware/Othello_Basic/Core/Src/main.c。
- 已检查父目录当前分支 task/9-labview-basic，相关 firmware 和源清单工具没有既有未提交改动；LabVIEW 文件仍由父任务管理，不覆盖、不切换分支。
- 新 main 依赖 app_basic/app_platform/app_config 及 ot_* 核心；只复制 main 将无法编译，因此同步必需的 9 个配套 .c/.h、应用源登记与登记工具/生成钩子，再在目标工程验证 Keil。
- 替换前将所有实际覆盖文件备份到父目录忽略的 build/backups 下；不复制 .ioc、不烧录、不更改串口参数或实际接线。
- 本地安装不把 firmware 改动混入父 LabVIEW 分支的提交；代码版本和本次安装记录继续在 #22 独立分支追踪。

### 2026-11-18 · 本地替换完成与构建

- 已覆盖 D:/black&white/firmware/Othello_Basic/Core/Src/main.c，及其必需的应用/核心 .c/.h、application_sources.txt、源登记工具与生成钩子，共 13 个同步文件；逐文件 SHA-256 与独立分支源文件一致。
- 已使用登记工具在目标 MDK 工程加入 4 个应用源，保持原 .ioc 和外设参数。补正文档中“main 循环为空”的过时描述。
- 覆盖前的文件备份在 D:/black&white/build/backups/basic-main-27a892ea/，包含旧 main、MDK 工程、README 和原生成脚本；未删除旧证据。
- 在 D:/black&white 目标目录实际执行 tools/build-keil.ps1：KEIL_EXIT=0，0 errors / 0 warnings，Code=13072、RO=416、RW=64、ZI=6872。
- 未运行 CubeMX 再生成，既有 KO 限制没有被本次编译结果掩盖；没有实机上电、烧录。
- LabVIEW 文件与父分支保持原状；父目录的本次 firmware 替换为未暂存工作区变更，避免擅自向其进行中的 LabVIEW 分支提交。软件版本、README 和安装留痕在 task/22-basic-main 提交推送。

## 结果与遗留

执行中。软件接口隔离可以减少集成冲突，不能保证未知创新实现无需遵守接口或测试。
