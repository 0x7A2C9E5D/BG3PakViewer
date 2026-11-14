# BG3PakViewer 缺陷评估

- 评估对象：`C:\Users\ROG\source\repos\BG3PakViewer`（master，`2da6e09`，13 个工程 / 153 个手写 .cs / 约 1.0 万行）
- 方式：全量只读代码审查 + `dotnet build BG3PakViewer.slnx -m:1` + `LSLib.dll` 反编译 + 真实 pak 头部取证 + 最小 IoC 探针
- 未修改任何源码。**编号 1–50 与聊天里逐条讲解的编号一致**，按严重度从高到低排。

## 总体结论

组织质量高于健壮性：分层清楚、VM 拆分合理、Dispose 纪律大体到位、无 `.Result`/`.Wait()` 死锁写法。缺陷集中在四个方向：

1. 不可信输入直通分配与寻址（虚拟纹理页文件）；
2. 并发与生命周期交叉（并行导出、取消后换包、Behavior 卸载）；
3. 错误被吞后仍报告成功（导出、全局异常处理器）；
4. 构建不可复现（绝对路径引用 LSLib、被 gitignore 的 license、未解决的依赖冲突）。

| 维度 | 判断 |
| --- | --- |
| 架构 / 可读性 | 较好 |
| 健壮性 | 较差（38 处 `catch (Exception)`，多为吞掉返回 null/false） |
| 并发正确性 | 较差 |
| 可复现性 | 差（本机 0 错误，换机器必失败；作者已接受该单机前提，见 #2/#3） |
| 可测试性 | 差（0 测试、0 CI、无静态检查门禁） |

## 评估证据

| 证据 | 结果 |
| --- | --- |
| `dotnet build -m:1`（SDK 11.0.100） | 0 错误 / 8 警告：7×`MSB3277`（`System.IO.Hashing`）+ 1×`CS0108`（ZeroQL 生成代码） |
| 输出目录依赖比对 | `LSLib.dll` 引用 `System.IO.Hashing 9.0.0.2`，实发 `8.0.23.53103` |
| `LSLib.dll` 反编译 | solid 条目 `SolidStream.Seek` 后返回共享流子流；`UnpackSolidSegment` 证明所有 solid 条目共用一个 `MemoryStream` |
| 真实 pak 头 | `LowTex.pak`：`LSPK` v18，`flags=0x06 = AllowMemoryMapping\|Solid`，`fileListOffset + size == 文件长度` |
| IoC 探针 | 未 `ConfigureServices` 时 `Ioc.Default.GetRequiredService<T>()` 抛 `InvalidOperationException` |
| `InvokeCommandAction.Invoke` 反编译 | 先 `CanExecute`，再 `Execute` |

## 上游缺陷（#1：归属 LSLib，等上游修，本仓库不改）

**1.（原 P0）solid 包的文件流不独立：并发读取会读到对方的数据**
上游位置（`LSLib.dll` 反编译，应用侧只是调用方）：
- `PackageReader.UnpackSolidSegment`：整个 solid 段解压进**一个** `MemoryStream`，然后对每个条目 `MakeSolid(num6, solidStream)`——所有条目共用同一个流实例。
- `PackagedFileInfo.CreateContentReader()`：`if (Solid) { SolidStream.Seek((long)SolidOffset, SeekOrigin.Begin); return new ReadOnlySubstream(SolidStream, (long)SolidOffset, (long)UncompressedSize); }`——返回的流只是共享流上的一个窗口。
- `ReadOnlySubstream.Read/ReadAsync`：每次读取前都 `SourceStream.Seek(FileOffset + CurPosition, SeekOrigin.Begin)`——Seek 与 Read 是两步、非原子。
  并发读取时两个线程的 Seek/Read 交错即读到对方偏移的数据，长度由 `Size` 决定、无校验无异常 ⇒ **静默数据损坏**。
  附带一个同类型的独立缺陷：`ReadAsync` 在返回读取任务**之前**就执行 `CurPosition += num2`（`num2` 是请求字节数而非实际读到的字节数），实际读取异常或短读时位置已经偏移。

应用侧调用点（无需改代码，若要兜底才动）：`BG3PakViewer/Services/ExportService.Folder.cs:69-79`（`Parallel.ForEachAsync`，并行度=CPU 核数）、`:92-94`；同类见 `Services/ExportStrategies/VirtualTextureExportStrategy.cs:52-57`。

实测（`LowTex.pak`，`flags=0x06`，5991 条目）：2 线程同步拷贝 5 轮 **0/10** 损坏；16 路并行 `CreateContentReader + CopyToAsync(FileStream)` 4 轮 **0/64** 损坏；串行对照 0/16。solid 数据在 `MemoryStream` 里，Seek→Read 窗口仅两条指令，抢占落点概率极低（按 15 ms 时间片粗算 1e-7/次读取）。

分布（本机全盘扫描，49 个 `Data\*.pak` + 4 个 `Data\Localization\*.pak`）：31 个是 LSPK，其中**只有 `LowTex.pak` 带 Solid**；`Shared`/`Game`/`Gustav`/`GustavX`/`Models`/`Textures`/`Icons`/`Effects`/`Materials`/`Localization\*` 全部非 solid，即模组作者真正会导出的包都不走这条路径。非 solid 路径另测：`Shared.pak`、`Assets.pak`、`Effects.pak`、`Icons.pak` 各 16 路并行 × 3 轮，共 **0/186** 损坏（每个条目各自 `CreateViewStream` / `LZ4DecompressionStream`，不共享位置）。

处理决定：**不在本仓库修，等 LSLib 上游修复**。上游最小改法是让 solid 条目返回独立视图（保留解压后的 `byte[]`，用 `new MemoryStream(buf, (int)offset, (int)size, writable: false)`，零拷贝且无共享位置）；若上游选择加锁，则会牺牲并行度。应用侧若将来要临时兜底，只需在 `ExportFolderAsync` 里按 `PackageFlags.Solid` 把并行度降到 1。

## 环境前提（#2、#3：作者已确认接受，不计为缺陷）

> 这两条描述的是"换机器/新克隆会怎样"，在**本机单开发者**前提下不构成缺陷：#2 是作者选定的引用方式（LSLib 没有 NuGet 包），#3 是刻意排除 license key 的必然结果。保留在此只记录触发条件——一旦上 CI、开源或换第二台机器，它们会立刻变成阻断项。

**2.（已接受）LSLib 用绝对路径 `HintPath` 引用**
`BG3PakViewer.csproj:49-52`，同样的块在 Contracts / Controls / Loader / Shared / VirtualTextures 共 6 个工程。
LSLib 没有 NuGet 包，本地二进制引用是合理选择；`D:\Tools\Packed` 同时放着它的依赖闭包（K4os、ZstdSharp、SharpGLTF、Newtonsoft、OpenTK、System.IO.Hashing/Pipelines、LSLibNative、granny2），MSBuild 会从同目录一并复制，所以本机能编能跑。
代价：`LSLib` 的 `AssemblyVersion=0.0.0.0`，升降级不会报错；`D:\Tools\Packed` 实际是一个未声明的依赖包。**仅当**需要 CI/换机/协作时再动，最小改法是把路径收成一个可覆盖的 MSBuild 属性（`<LSLibPath Condition="'$(LSLibPath)'==''">D:\Tools\Packed</LSLibPath>`，用 `-p:LSLibPath=...` 覆盖）、并把 6 份重复收进 `Directory.Build.props`——不必上 NuGet。

**3.（已接受）`Syncfusion.lic` 刻意不入库，但资源项无条件 Embed**
`.gitignore:363` 排除该文件是对的（license key 不该提交）；问题只在 `BG3PakViewer.csproj:68` 的 `<EmbeddedResource Include="Syncfusion.lic"/>` 没有条件——缺文件时 CSC 直接报 `CS1566`（最小工程实测：`读取资源"p.Syncfusion.lic"时出错 -- Could not find file`），新克隆编不过。本机有一个 84 字节的 lic，所以一直没察觉。
两个都可行的结局：① 接受现状并写进 README（当前仓库没有 README），把"需自备 `Syncfusion.lic` 才能构建"列为已知前提；② 资源项条件化 + `App.xaml.cs:196-202` 判空降级并记日志（3 处小改），让无 license 的克隆也能编译。注意条件化只解决构建，**运行时仍需 license**（Syncfusion 会提示/限制）。

## 构建卫生（#4）

**4.（P3，已修复并验证）`System.IO.Hashing` 版本冲突：是警告，不是当前故障**
7 条 `MSB3277`，每工程一条：`LSLib.dll` 编译时引用 `9.0.0.2`，实际随包的是 `8.0.23.53103`（被 SixLabors.ImageSharp 4.1.3 作为传递依赖带进来），RAR 选了较低的 8.0.0。
实测 LSLib 对 `System.IO.Hashing` 的成员引用只有 `Crc32.HashToUInt32` 一处，而 `Crc32`/`HashToUInt32` 在 8.0.0 中就存在；已确认的调用点是 `PackageWriter.WriteFile`（写 pak 时算 CRC），本工具不写 pak。.NET Core 按简单名绑定，所以**当前不会炸**。
风险是未来的：LSLib 若改用 9.0 独有的 API（如 `XxHash128`）就会 `MissingMethodException`，且警告被淹没在构建输出里。修法：显式加 `System.IO.Hashing 9.0.0`（与 LSLib 同版），并让 `MSB3277` 变错误。

**修复记录（2026-11-14，`BG3PakViewer.Loader.csproj`）**：
- 先前的 `0c2432f` 只给 Icons / Shared 加了 `System.IO.Hashing 11.0.0`，Loader 没有直接引用：它同时引用 `SixLabors.ImageSharp 4.1.3`（传递 8.0.0）和 `LSLib`（9.0.0.2），RAR 仍选 8.0.0，所以 Loader 每次构建都报 `MSB3277`（实测 22 行，全部来自该工程）。
- 现在 Loader 显式引用 `System.IO.Hashing 11.0.0`（不低于 LSLib 的 9.0.0.2），Loader 的 `project.assets.json` 与全仓库统一到 11.0.0。
- 验证：`dotnet build BG3PakViewer.slnx -m:1 -c Debug -p:Platform=x64 -t:Rebuild` → **0 警告 / 0 错误**；Loader 单独 `-t:Rebuild` 也是 0 警告，`MSB3277` 0 行；应用输出的 `System.IO.Hashing.dll` 为 11.0.26.53018。
- **未做**：没有把 `MSB3277` 提成错误（`MSBuildWarningsAsErrors`）。那是全局构建策略变更，等确实需要门禁时再加；当前冲突已消失，门禁只是防回归。

## P1：崩溃、丢数据、泄漏（5–15）

**5. 第二个实例退出时必抛未处理异常（已修复并验证）**
`App.xaml.cs:264-269`（`OnExit` 无条件 `Ioc.Default.GetRequiredService<ISettingsPersistenceService>().Save(...)`）对应 `:227-246`（"已有实例"分支在 `ConfigureServices` 之前就 `Shutdown()`）。
**应用日志里看不到这条崩溃**：异常在 `OnExit` 里抛出、没有任何 handler，不经过 Serilog；`SetupExceptionHandling` 要等 `ConfigureServices` 之后才安装，这条路径走不到。证据在 Windows 事件日志（`.NET Runtime` ID 1026），本机已记录 **10 次**同签名崩溃（11-12 5 次、11-14 5 次），堆栈精确到：

```
Exception Info: System.InvalidOperationException: The service provider has not been configured yet.
   at CommunityToolkit.Mvvm.DependencyInjection.Ioc.ThrowInvalidOperationExceptionForMissingInitialization()
   at CommunityToolkit.Mvvm.DependencyInjection.Ioc.GetRequiredService[T]()
   at BG3PakViewer.App.OnExit(ExitEventArgs e) in ...\App.xaml.cs:line 252
   at System.Windows.Application.DoShutdown()
```

其中 11-14 06:18:02 这次与应用日志 `log20261114_001.txt` 的 `06:18:02.791 [INF] Shutting down this instance` 同一秒，路径对应无疑。进程随后以 `0xE0434352`（CLR 未处理异常）被终止，同时写入 WER `CLR20r3`（`P4: CommunityToolkit.Mvvm 8.4.2.1`）。
后果：设置不保存、主 VM 不释放、退出过程带崩溃（WER 记录）。
触发：程序已开着再启动一次，提示框选是或否都会走到。
**设计约束（作者确认）**：多开时的退出路径**本来就不该持久化设置**——第二个实例没有加载过设置，写回去只会覆盖正在运行那个实例的状态。所以修复必须保住"不持久"，只把"崩溃式退出"变成"干净退出"。
正确修法：加一个 `_initialized` 标志（在 `InitializeApplication()` 末尾置 true），`OnExit` 里**仅当已初始化**才 `Save` + 释放主 VM；未初始化分支只释放自己持有的 `SingleInstanceManager`（互斥量）和早期 logger，不碰设置。
**不要**采用"把 `ConfigureServices` 提到 `OnStartup` 最前"的做法：那会让第二个实例加载设置、再在 OnExit 写回，正好破坏这条不持久约束。

**修复记录（2026-11-14，`App.xaml.cs`）**：新增 `_initialized` 字段（`:39`），在 `InitializeApplication()` 末尾置位（`:141`），`OnExit`（`:271-284`）改为两分支——未初始化只 `_singleInstanceManager?.Dispose()` 后返回，已初始化才 `Save` + `Dispose`；两分支都调用 `base.OnExit(e)`。
验证（Debug x64 实跑）：第一个实例正常启动 → 第二个实例弹出"多实例可能导致数据被错误覆盖…"对话框 → 用 UI Automation 点"否(N)" → 第二个实例 **退出码 `0x00000000`**（修复前为 `0xE0434352`）、**事件日志无新增崩溃条目**（前后计数相同）、**`AppSettings.json` 未被修改**（mtime 不变，符合"多开不持久"约束）；随后手动结束第一个实例，无残留进程。Debug/Release 均构建通过（0 错误）。
附带观察（**待确认**）：11-14 06:16:27/30/34 还有三次同签名崩溃，但没有任何对应日志行——怀疑第二个进程打不开已被占用的日志文件（Serilog.Sinks.File 默认 `FileShare.Read`，打开失败后静默丢弃 sink），即"第二个实例的启动日志可能整段丢失"。

**6. 单文件导出时点"取消导出"→ `await null` 抛 NRE（已修复并验证）**
`ViewModels/MainWindowViewModel.Export.cs` 的 `HandleIsExportingFiles`：`await _cancellationTokenSource?.CancelAsync()!;`
CTS 只在文件夹导出创建；单文件导出期间 `IsExporting=true` 而 CTS 为 null，`?.` 得 null，`await null` 抛 NRE，异常被 #11 吞掉 → "点了打开没反应"，而且对话框承诺的"取消导出"根本没发生。
**修复记录（2026-11-14）**：① `HandleIsExportingFiles` 改为判空——CTS 为 null 时提示 `Strings.ExportNotCancellableMessage` 并 `return false`（不假装能取消）；非 null 时 `await _cancellationTokenSource.CancelAsync()`（去掉 `?.`/`!`）。② `ExportFolderAsync` 改为局部 CTS + `try/finally`，结束时置 null 并 `Dispose()`，让"是否存在可取消导出"这个判断永远真实（顺带解决 #44）。
验证：Debug/Release 构建 0 错误；新增资源在 en / zh-Hans / zh-Hant / it 四种语言下都能取到正确文本；启停冒烟测试通过（正常关窗 → 退出码 0、事件日志无新增崩溃、设置照常保存）。
**未覆盖**：没有自动化"文件导出进行中 + 拖入 pak"这个组合动作，该分支靠代码路径与资源解析确认，未做端到端实跑。

**7. 取消导出后立刻换包：旧包在导出仍读取时被释放（已修复，打开路径）**
`MainWindowViewModel.Export.cs`（只发取消不等待）→ `MainWindowViewModel.Package.cs`（`CleanupAsync()`）→ `PackageService.CleanupAsync`。
`CancelAsync` 只置位，`Parallel.ForEachAsync` 还在读绑定到 `MemoryMappedView` 的流；随后立刻 `Package.Dispose()` 会与读取并发。
**修复记录（2026-11-14）**：`ExportAsync` 增加一个 `TaskCompletionSource _exportCompletion`（在 `finally` 里完成），`HandleIsExportingFiles` 取消后 `await completion.Task`，等导出循环真正退出、不再持有旧包后才返回 true，让调用方继续 `CleanupAsync`。同时处理了模态对话框期间的窗口期：导出可能在用户点确认前就结束，字段已被清空，所以取消前重新判空（否则又回到 `await null`）。
**仍未覆盖**：关窗退出路径——`MainWindowViewModel.Dispose` 是同步的，在这里等导出会与导出回投 UI 线程的续体互相卡住（死锁），所以只在打开新包这条路径上等待；关窗时用户已被告知"正在导出"，进程随后退出，影响限于半截文件与日志噪音。
验证：Debug/Release 构建 0 错误、无 CS 警告；启停冒烟通过。**未做**端到端实跑（需要在文件夹导出进行中拖入 pak）。

**8. 文件夹导出"假成功"并留下半截文件（代码成立；但日志显示该路径从未被使用）**
`ExportService.Folder.cs:86-100`（逐文件异常只记日志、`SaveStreamToFileAsync` 失败只告警）→ `:34-38` 无条件 `return true`；`Utils/FileOperations.cs:16-30` 失败不删目标文件。
触发：目标分区写满、文件被占用、读取异常。结果：UI 弹 "Export completed"，用户拿到残缺文件。对照 `Services/PreviewExportService.cs:40-53` 有正确的"失败删半成品"实现。
证据：① 代码如上；② 实测——用一个在 65536 字节处抛 `IOException` 的流调用 `FileOperations.SaveStreamToFileAsync`，返回 **false**、目标文件**仍然存在且为 65536 字节的半截文件**；③ 日志——全部 Debug/Release 日志里 `Exporting folder` / `Found N files to export` / `Folder export completed` 均为 **0 次**，即文件夹导出从未实际执行过。日志中 16 条 `Failed to export file to:` 属于虚拟纹理工具箱的 `PreviewExportService`（会逐条报告失败），不属本项。
结论：缺陷真实存在但从未被走到，优先级降为"待定"；真要启用文件夹导出时再修（统计失败数 + 失败删文件/临时文件改名）。

**9. Wwise 导出用 `File.OpenWrite`，不截断旧文件**
`Loader/WwiseAudioLoader.cs:38`。
覆盖更长旧文件时新内容后面残留旧字节 → 输出非法。修法：`File.Create(path)`。

**10. 设置保存非原子 + 反序列化严格：一次失败即丢全部设置（已修复并验证）**
`Services/SettingsPersistenceService.cs:62-74`：`File.Create` 先把 `AppSettings.json` 截断为 0 字节再序列化，异常时旧文件已废。
叠加：`Models/AppSettings.cs:25-30` 的 `[JsonConstructor] AppSettings(ObservableCollection<IRecentFileEntry> recentItems, string language)` 里 `RecentItems = [.. recentItems]`；JSON 缺 `RecentItems`（或显式 `null`）时 STJ 传 null ⇒ 构造函数抛 NRE，`Load`（`:89-107`）吞异常返回默认值 ⇒ 语言、最近文件、默认目录全丢且只有一条日志。

**实测（真实代码，非复刻）**：把 `SettingsPersistenceService.cs`、`AppSettings.cs`、`RecentFileEntry.cs`、`ISettingsPersistenceService.cs` 用 `<Compile Include>` 链进控制台宿主（仍用项目自身的 Contracts / Miscellaneous 工程引用），驱动真实 `Save<T>` / `Load<AppSettings>`：
- 用"序列化到一半抛异常"的类型调 `Save`：修复前 61 字节的设置文件被截断为 **0 字节**，旧内容全丢，随后 `Load` 得到全默认；修复后文件仍为 61 字节、内容不变、`Load` 拿回原值。
- 缺 `RecentItems` / `RecentItems:null`：修复前 `Language=[] MaxPreviewLines=500 DefaultOpenDirectory=[]`（整份作废）；修复后 `Language=[zh-Hans] MaxPreviewLines=123` 保住，只有 `RecentItems` 为空（文件里本来就没有）。
- 真截断的 JSON 仍回落默认值——非法 JSON 无可恢复，属既有语义（`Load` 不阻塞启动）。

**修复记录（2026-11-14）**：
- `SettingsPersistenceService.Save`：改为先写 `_filePath + ".tmp"`，再 `File.Move(tempPath, _filePath, overwrite: true)`（同目录原子改名）——磁盘上永远只可能是"旧的完整文件"或"新的完整文件"，也不会再出现两个实例同时写、两股字节掺进同一个文件。
- `AppSettings` 构造函数参数改为可空 `ObservableCollection<IRecentFileEntry>?`，`RecentItems = [.. recentItems ?? []]`。

**频率与严重度重估**：`Save` 全仓库只有 `App.xaml.cs:281` 一处调用（退出路径），写的是约 1.4KB。触发条件是"退出写盘过程中被杀 / 崩溃 / 断电"，或"两个已初始化实例同时退出"。本机日志里 settings 相关失败行 **0 条**、`AppSettings.json` 一直完好，实际风险很低；后果也限于偏好重置（语言、最近文件、默认目录、预览行数），无用户内容损失 ⇒ **严重度由 P1 降为 P3**。

**验证**：`BG3PakViewer.csproj` Debug x64 构建 0 错误（仅既有 7×`MSB3277`）；控制台探针重跑上述用例全部符合预期；真实应用启停冒烟——Debug 配置（`%APPDATA%\BG3PakViewer_Debug\AppSettings.json`）启动后关窗，退出码 0，设置文件被重写为合法 JSON（1809 字节）、无残留 `.tmp`。
**未覆盖**：没有真机制造"写盘中途断电"，失败路径的原子性是用可抛的序列化类型在同一个 `Save` 方法里证实的。
附带现象（可忽略）：保存失败会在配置目录留下 0 字节的 `AppSettings.json.tmp`，下次成功保存时被覆盖，不会累积。

**11. 全局异常处理器把一切标记为已处理（启动段已修复并验证）**
`App.xaml.cs:216-231`：`DispatcherUnhandledException` 里 `e.Handled = true` 后只写日志，进程继续跑。
运行期这大体是对的（本机日志 964 条被吞的异常里 854 条是同一批 XAML/绑定错误，全部弹窗会把用户逼疯），真正的缺陷在**启动段**：`SetupExceptionHandling()` 装好之后、`InitializeMainWindow()` 之前抛出的异常会被同一条路径吞掉，于是 WPF 继续跑消息循环但没有任何窗口——**进程活着、界面没有**，用户只能去任务管理器杀。

**实测（真实应用）**：Debug 配置的 `AppSettings.json` 写入 `"Language": null`（命中 #28 的 `new CultureInfo(null)`），启动：

```
alive: True          MainWindowHandle: 0          MainWindowTitle: []
[ERR] Unhandled exception: Value cannot be null. (Parameter 'name')
   at System.Globalization.CultureInfo..ctor(...)
   at BG3PakViewer.App.InitializeCulture() ... App.xaml.cs:line 191
   at BG3PakViewer.App.InitializeApplication() ... App.xaml.cs:line 134
   at BG3PakViewer.App.OnStartup(...) ... App.xaml.cs:line 249
```

并用最小 WPF 探针单独确认机制：`OnStartup` 里抛出的异常会进 `DispatcherUnhandledException`，`Handled = true` 后 `windows=0 / mainWindow=null / dispatcherAlive=True` 持续成立（第一次跑探针没写退出定时器，进程就一直挂着，只能手动 kill）。反过来，处理器安装之前（`ConfigureServices`/`InitializeLogging`）的异常仍然直接崩到 WER。

**修复记录（2026-11-14，`App.xaml.cs`）**：
- `OnStartup` 只保留三步：`InitializeEarlyLogging()` → 单实例检查（`TryRedirectToRunningInstance()`，true 则 `Shutdown()` 并返回）→ `StartApplication()`；后两步各自抽成私有方法，`OnStartup` 从 36 行压到 10 行。
- `StartApplication()` 把 `InitializeApplication()` 包进 try/catch，失败交给 `ReportStartupFailure(ex)`：记日志（`Application failed to start`）→ `ShutdownMode = ShutdownMode.OnExplicitShutdown` → 弹 `MessageBox`（新增资源 `StartupFailedMessage`：`应用启动失败：{0}。详见日志。` + `StartupFailedCaption`：`启动失败`，en / zh-Hans / zh-Hant / it 四套 + `Strings.Designer.cs`）→ `Shutdown(1)`。
  为什么要先钉 `ShutdownMode`（有 WPF 源码与实测双证）：这个 `MessageBox` 是启动失败路径上唯一的窗口，默认 `OnLastWindowClose` 下关掉它就已经以退出码 0 启动了关闭流程；随后 `Shutdown(1)` 进入 `Application.CriticalShutdown(int)` 时被开头的 `if (Application.IsShuttingDown) return;` 挡掉，`SetExitCode(1)` 根本不执行。埋点实测 `OnExit` 里 `ApplicationExitCode=0`；把它改成 `OnExplicitShutdown` 后关框不再触发关闭，`Shutdown(1)` 正常走到 `SetExitCode(1)`，进程退出码为 1，因此不再需要手写 `Environment.ExitCode = 1`。对照探针：没有窗口时 `Shutdown(1)` 真正执行后 `Environment.ExitCode` 就是 1 —— 曾据此误以为是"WPF 丢弃 Shutdown 的退出码"，实际只是它没执行到。
- `TaskScheduler.UnobservedTaskException` 里删掉 `e.SetObserved()`：.NET 5+ 的未观察任务异常本来就不会终止进程（实测：不调 `SetObserved()` 事件照常触发、进程存活、退出码 0；连强制 `AppContext.SetSwitch("ThrowUnobservedTaskExceptions", true)` 在 .NET 10.0.15 上也无效），这半句是冗余，日志保留。
- 运行期 `e.Handled = true` 保持不动（见上，绑定类异常吞掉是正确的）。
- 与 #5 的约束相容：`_initialized` 只在 `InitializeApplication()` 末尾置位，启动失败时仍为 false，`OnExit` 走不持久化分支。

**验证**（Debug x64，真实应用 + UI Automation）：
- 坏配置（`Language: null`）启动 → 1.2s 后弹出对话框，UI Automation 读到窗口文本 `应用启动失败：Value cannot be null. (Parameter 'name')。详见日志。`、标题文本 `启动失败`、按钮 `确定`；点确定 → 进程退出码 **1**（修复前：无窗口、进程永久存活、退出码 0）。
- 同一场景下设置文件 **mtime 与内容均未变化**（失败实例不写设置），验证 #5 的"未初始化不持久化"在启动失败路径上同样成立。
- 正常启动 → 主窗口 `博德之门3资源浏览器（非官方）`；再启动第二个实例 → 多实例对话框点"否(N)" → 第二实例退出码 0、第一个继续运行、设置未被改写（mtime 不变）；关闭第一个实例 → 退出码 0、设置被原子重写（1809 字节、JSON 合法、无残留 `.tmp`）。
- `BG3PakViewer.csproj` Debug x64 构建 0 错误，无新增编译/分析器警告（剩余警告仍是 #4 的 `MSB3277`）。
**未覆盖**：没有逐一给运行期每个 `catch` 补提示（那属于各调用点的错误上报，不是本条的全局处理器问题）；`OnExit`/后台线程的异常仍不在 `DispatcherUnhandledException` 覆盖范围内（仍是 WER 崩溃，见 #5）。

**12. Behavior 用 `Unloaded` 释放控件、Messenger 注销却留在 `OnDetaching`（已修复并验证）**
`Shared/Behaviors/Viewport3DxBehavior.cs`、`Shared/Behaviors/SfDataGridSearchBehavior.cs`：注销写在 `OnDetaching` 里，而 `Unloaded` 里只做 `Dispose`。
WPF 在元素卸载时**不会**调用 `OnDetaching`（行为只在被移出 `Behaviors` 集合时解绑），项目自己在 `Behaviors/MainWindowBehavior.cs:40-44` 手工 `UnregisterAll` 就是同一问题的补丁。后果：预览关闭后注册仍在，下一次 Zoom/搜索广播会打到已 `Dispose` 的 `Viewport3DX` / `SfDataGrid`；`Unloaded` 还可能多次触发 → 重复 `Dispose()`。
**实测一（真控件 WPF 探针）**：把真实的 `Viewport3DX`（HelixToolkit）和 `SfDataGrid`（Syncfusion）放进窗口，用真实 WPF 卸载（从视觉树移除）验证：`OnDetaching` 调用次数为 **0**，而修复前消息注册在卸载后仍然存在。
**实测二（更正报告里的"泄漏"说法）**：用 `CommunityToolkit.Mvvm 8.4.2` 采集验证，`WeakReferenceMessenger` **不会**因为 lambda 捕获 recipient 而把 recipient 钉死——捕获 recipient 的 lambda 注册后 recipient 被 GC 回收、handler 里捕获的独立对象也被回收、把 recipient 根在静态表里则 GC 后消息仍能送达。所以"每预览一个 `.loca` 泄漏整张 `SfDataGrid` + 全部条目"不成立；真正的缺陷是"卸载后仍会收到广播"和"重复 Dispose"。
**修复记录（2026-11-14）**：
- 两个 Behavior 都改为实现 `IRecipient<TMessage>`（`Viewport3DxBehavior : IRecipient<string>`、`SfDataGridSearchBehavior : IRecipient<ValueChangedMessage<string?>>`），改用 `Register<TMessage, TToken>(this, token)` 注册，handler 不再是捕获 `this` 的 lambda。
- `AssociatedObject_Unloaded`：先解绑 `Unloaded`、注销消息，再在 `_disposed` 保护下只 `Dispose` 一次（`EffectsManager?.Dispose()` + `Dispose()` / `SearchHelper.Dispose()` + `Dispose()`）。
- `OnDetaching` 保留对称的注销，行为被真正移除时仍然走这里。
**验证**：`BG3PakViewer.Shared` 与 `BG3PakViewer.csproj` Debug x64 构建 0 错误、无新增编译/分析器警告（只剩 #4 的 `MSB3277`）；真控件探针输出：

```
--- attached to a live window
  viewport registered: True
  grid registered:     True
--- after real WPF Unloaded (controls removed from the tree)
  viewport OnDetaching called: 0
  grid OnDetaching called:     0
  viewport registered: False
  grid registered:     False
--- broadcasts after unload (must not reach the disposed objects)
  no exception
--- raising Unloaded again (idempotency)
  no exception
```

**未覆盖**：没有在真实 UI 里跑"预览模型 → 切到本地化预览 → 点 Zoom"的端到端序列（需要具体 pak 条目 + UI 自动化）；`SearchFilterViewModel` 等 VM 仍用捕获 lambda 注册（它们在 `Dispose` 里注销，本项未改动）。

**13. 日志集合被后台线程修改，且无上限（已修复并验证）**
原实现：`App.xaml.cs` 把 Serilog observer 直接接到 `LogAccessService.Logs`（裸 `ObservableCollection`）的 `Add` 上——写入发生在"哪个线程打日志"就是哪个线程（并行导出/更新检查时会有多个）；`LogDialogViewModel` 构造时对同一个集合做 `Select(...).ToList()`。两者无同步，导出进行中点"日志"会撞 `InvalidOperationException: Collection was modified`，被 `ShowDialogWithLoggingAsync` 吞成"对话框打不开"。集合只增不减。
**实测（旧模式）**：一个后台线程 `Add` 30 万条、另一个线程循环枚举，5×10⁵ 次枚举里撞出 **6.6 万次** "Collection was modified"。
**修复记录（2026-11-14）**：
- `ILogAccessService` 换成"加锁 + 快照 + 订阅"三个操作：`Add(LogEvent)`、`Subscribe(handler)`（同一把锁内注册回调并 `ToArray()` 复制历史）、`Unsubscribe(handler)`。`Add` 在锁内追加并触发回调（因此"快照 + 订阅"是原子的：一条日志要么在快照里、要么走回调，不会重复也不会丢），超过 **5000 条**时丢弃最旧的。回调在服务锁内触发，所以约定了"处理器不能同步回调本服务"。
- `App.xaml.cs`：observer 从 `Logs.Add` 改为 `Add`。
- `LogDialogViewModel`：构造时一次 `Subscribe` 取历史，之后 `OnLogAdded` 只负责 `Application.Current.Dispatcher.BeginInvoke(...)` 把新条目排到 UI 线程追加（集合不再跨线程写入），`Dispose` 里 `Unsubscribe`；删掉了原来的 `CollectionChanged` 双向同步、`EnableCollectionSynchronization`、`_logEventsLock` 以及"从 UI 删一行又回头删源集合"的回路。
**验证**：
- 并发探针（真实 `LogAccessService` 源码链入）：4 个线程共 2 万条 `Add`，同时另开线程反复 `Subscribe`/`Unsubscribe` 500 轮 → **0 次异常**；订阅前已有 1000 条 → 快照 1000 + 回调 20000 = 21000，**无重复、无遗漏**；12,000 条后快照恰好保留最新 5000 条（7000..11999）；`Unsubscribe` 后不再收到回调。
- 真实应用（Debug x64）：启动 → 点"日志" → 对话框窗口 `日志` 正常出现，UI Automation 读到 54 个元素、按"日志时间/日志级别/日志内容"三列展示本次会话的启动日志（SfDataGrid 的行在 UIA 里是 Text 而不是 DataItem）；关闭对话框后主窗口继续运行，关闭主窗口退出码 0、设置原子保存、无残留 `.tmp`。
- `BG3PakViewer` / `BG3PakViewer.Contracts` / `BG3PakViewer.Dialogs` 编译 0 错误，无新增编译/分析器警告（只剩 #4 的 `MSB3277`）。
**未覆盖**：没有在"长时间导出过程中打开日志对话框"这个原始触发场景里做端到端跑（导出与日志对话框的 UI 自动化组合成本高）；线程安全与上限由上述并发探针保证，UI 侧只验证了"打开-展示-关闭"这条正常路径。

**14. 虚拟纹理页文件解析无任何边界校验（已修复并验证）**
`VirtualTextures/PageFiles/VirtualTextureTilePageFile.cs`：`numPages = Length / _pageSize`（`PageSize==0` 除零，极小值 ⇒ `new List<>(numPages)` 亿级预分配）；`new uint[numOffsets]` 无上界；`_chunkOffsets[page][chunk]` 无范围检查；`(int)header.Size` 未校验符号与上限；`ReadBytes` 短读被当成完整数据。触发：打开含畸形/截断 `.gtp` 的 pak 后预览或导出虚拟纹理。另外实测发现 `BinUtils.ReadStruct` 对**短流不抛异常**（喂 10 字节流给 `GTPHeader`：不抛、只前进 10 字节并返回全零头），所以截断文件连头部都"读成功"。
**报告里有一处要更正**：`numOffsets = 0xFFFFFFFF` 并不会去申请 ~16GB，而是在 `new uint[uint.MaxValue]` 处直接 `OverflowException`；真正会打到分配极限的是 int 范围以内的大值（约 10 亿项 ⇒ 4GB 数组 → `OutOfMemoryException`），但"按文件声明分配"这个风险本身成立。
**修复记录（2026-11-14）**：
- 构造时统一校验：`PageSize` 必须在 `1..4 MiB`；流长度必须 ≥ 24 字节的 `GTPHeader`（`Marshal.SizeOf<GTPHeader>()`，不再手写常量）且是页大小的整数倍；`numPages` 不得超过 4096。
- **阈值依据（2026-11-14 全量扫描实测）**：`VirtualTextures.pak` 的 16 个 tile set，`PageSize` 只有 **1 MiB** 一个值（来自 `.gts` 头 `GTSHeader.PageSize`，偏移 128；`.gtp` 自身头里没有这个字段）；12,974 个 page file 的元数据长度全部是页大小的整数倍，页数 min=1、**max=445**（445.0 MiB，`Albedo_Normal_Physical_6_6a4cd876384cbcc53456298eb8a017e5.gtp`），单个 tile set 最多 889 个 page file；抽样 49 个 page file 的 3,123 个 chunk：单页偏移数最多 **200**、最大 chunk **26,276 字节**、chunk 末尾离页首最远 88,952 字节（都远小于 1 MiB）。据此取 `MaxPageSize = 4 MiB`（4× 实测页大小）、`MaxPages = 4096`（约 9× 实测最大 445 页，1 MiB 页下即单文件 4 GiB）。两个都是合理性上限而非格式规定，注释里写明了依据。
- 逐页校验偏移表：读计数前必须还有 4 字节空间，读完计数后 `position + n*4` 必须仍在**本页**内（页 0 的计数从 24 字节头之后开始），越界抛 `InvalidDataException`；页尾用 `Position = pageEnd` 对齐。
- `ReadChunk` 校验：page/chunk 索引范围、chunk 偏移 + 12 字节头必须落在本页内、`header.Size` 不能把数据顶出本页、`ReadBytes` 实际读到的字节数必须等于 `Size`（本页边界检查后这条路已不可达，保留作最后一道）；一律抛 `InvalidDataException`。
- 构造失败时 `Dispose` 掉 reader/stream（它拥有流），顺手补掉 #23 里"构造抛异常 → 流泄漏"的那一半。
- 方法按职责拆开：`ValidatePageSize` / `ReadOffsetsOrDispose` / `ReadChunkOffsetTables` / `ValidateLayout` / `GetPageCount` / `ReadPageOffsets` / `ReadOffsetCount` / `GetChunkOffset` / `GetPageOffsets` / `ValidateChunkOffset` / `ValidateChunkEnd` / `ReadChunkData`，每个方法体都 ≤10 行（拆分前 `ReadChunkOffsetTables` 约 30 行、`ReadChunk` 约 20 行）。
**验证**：
- 合成数据探针（真实 `VirtualTextureTilePageFile.cs` 源码链入）：合法页文件能读出 chunk；`pageSize` 为 0 / 负数 / >4 MiB、流短于头部、长度非页整数倍、偏移计数 `0xFFFFFFFF`、chunk 偏移出页、chunk 大小出页、页数超过 4096——**全部按预期抛 `InvalidDataException`** 且消息可读；旧的 `new uint[uint.MaxValue]` 实测抛 `OverflowException`。
- 真实数据（`VirtualTextures.pak`）：16 个 tile set 共 **12,974 个 page file** 全部用元数据长度做了对齐/页数核对（0 例外），其中 61 个（前 60 个 + 全 pak 最大的 445 页/445 MiB 那个）真正走新构造函数，读出 108 个 chunk 全部成功；`pageSize` 实际值 1 MiB。
- 端到端：用项目自己的 `VirtualTextureCatalog` + `VirtualTextureExtractor` 对真实 pak 抽 `240912d3d30934fe0ca2c746be0638ca`，像素统计与修复前**完全一致**（L0 R[0,255] mean 139.6/G 122.4/B 105.0、12,083 种颜色；L1 同前），说明正常文件没有被误伤。
- `BG3PakViewer.VirtualTextures` / `BG3PakViewer` Debug x64 编译 0 错误、无新增编译/分析器警告（只剩 #4 的 `MSB3277`）。
**未覆盖**：没有构造一个真正"截断到半页"的真实 `.gtp` 文件走应用 UI 去验证提示文案（合成流已覆盖同一代码路径）；`VirtualTextureTilePageFileHolder` 自己的两条泄漏/无上限复制仍属 #23。

**15. PAK 条目名直接拼到文件系统（路径穿越）（已修复并验证）**
`ExportService.Folder.cs`（`Path.Combine(folder, file.Name)`，`file.Name` 是 pak 条目名）、`Services/ExportStrategies/VirtualTextureExportStrategy.cs`（页文件名来自 tile set 内容）。`Path.Combine` 遇到 rooted 的第二个参数会直接返回它，`..` 也不会被拦，所以恶意/损坏的 pak 条目名（`..\..\evil.dll`、`C:\Windows\x.dll`、UNC）可以在用户没选的位置覆盖文件。
审计时同一类写盘点还有一处：`Services/PreviewExportService.cs`（图标图集、故事脚本、虚拟纹理工具箱三种导出的公共写盘入口，文件名来自 pak 内容），一并修了。
**修复记录（2026-11-14）**：
- `BG3PakViewer.Utils/FileOperations.cs` 新增 `ResolveInside(folder, relativePath)`：`Path.GetFullPath` 归一化后用 `folder + 分隔符` 前缀做 `OrdinalIgnoreCase` 比较（同时处理盘根 `C:\`、结尾分隔符、大小写），越界抛 `InvalidDataException`。
- `ExportService.Folder.cs`：`targetPath` 改为 `ResolveInside(folder, file.Name)`（移进 try，拒绝时按"该文件导出失败"记日志）。
- `VirtualTextureExportStrategy`：页文件目标路径改为 `ResolveInside(targetFolderPath, pageFileName)`（在原有的 per-file try/catch 里，被拒绝就跳过该页并记日志）。
- `PreviewExportService`：每个文件先 `ResolveInside(folder, file.FileName)`，拒绝时返回一条失败结果（带原因）而不是抛异常，其余文件继续导出。写盘逻辑拆成 `ExportFileAsync`（解析路径 + 拒绝）/ `WriteFileAsync`（复制 + 取消/失败处理）/ `FailWrite`（记日志、删半成品、返回失败），`ExportAsync` 只剩遍历、取消检查和进度汇总（方法体 16 行）。
- 其余写盘点审计结论：单文件导出/音频转换等用的都是用户在对话框里选的路径，不受影响；虚拟纹理工具箱建子目录用的 `Path.GetFileNameWithoutExtension(gtsPath)` 不含分隔符，不构成穿越向量。
**验证**：
- 辅助函数探针（真实 `FileOperations.cs` 源码链入）：14 个用例——正常 pak 路径/Windows 分隔符/页文件名/`Public/../a.txt`（归一化后仍在根内）/`..hidden.dds` 全部接受；`..\..\evil.dll`、`Public/../../evil.dll`、`..\exp2\x.dll`、`C:\Windows\evil.dll`、`/evil.txt`、`\\server\share\evil`、空名、`.`、`..`、`Public/..\..\evil.dll` 全部拒绝；盘根 `C:\`、结尾带分隔符、大小写不同的根目录都能正确放行。
- 旧行为对照：`Path.Combine(root, "..\..\evil.dll")` 得到 root 下的 `..\..\evil.dll`（`File.Create` 会跟出去），`Path.Combine(root, "C:\Windows\evil.dll")` 直接等于 `C:\Windows\evil.dll`。
- 真实数据不误伤：`VirtualTextures.pak` 的 **12,990 个条目名**全部通过新校验（0 拒绝、0 个含 `.`/`..` 段或 rooted 的可疑名）。
- `PreviewExportService` 端到端（真实类型 + 临时目录）：正常 `ok.txt` 成功写入；`..\..\evil.txt` 和 `C:\Windows\evil.txt` 各返回 `success=False` 且带原因，临时目录外**没有**生成 evil.txt；另测"写入过程中取消"——0 条结果、目录里无残留文件、后续文件不再尝试（与原实现一致）。
- `BG3PakViewer` / `BG3PakViewer.Utils` Debug x64 编译 0 错误、无新增编译/分析器警告（只剩既有的 `MSB3277`）。
**未覆盖**：没有在真实 UI 里跑"打开恶意 pak → 选文件夹 → 导出"这条完整链路（导出文件夹功能在本机日志里从未被执行过，见 #8）；目标目录内已存在的 junction/symlink 指向外部时本校验不拦（需要在导出根目录里预先放链接，属另一类攻击面）。

## P2：功能错误与一致性问题（16–34）

**16. 文件夹匹配用字符串前缀，会多导同级前缀目录（已修复并验证）**
`ExportService.Folder.cs:57-61`：`StartsWith(folderNode.FullPath)` 无分隔符。选 `Public/Shared` 连带 `Public/SharedDev`。修法：`== p || StartsWith(p + "/")`。
**修复记录（2026-11-14）**：
- `GetFolderFiles` 改为按整段路径比较：根节点（`FullPath` 为空）直接返回全部有效文件（保持原行为），其余用 `folderNode.FullPath + "/"` 当前缀。
- 没有照抄报告里的 `== p || StartsWith(p + "/")`：`p` 为空时两条都不成立，会把"导出根节点 = 导出整个包"变成导出 0 个文件；而 `== p` 只能命中一个与文件夹同名的条目（同一 pak 里既有 `X` 又有 `X/...`），那不属于"该文件夹内的文件"，导出时还会和目录写盘冲突。
**验证**：
- 源码链入探针（真实 `ExportService.Folder.cs` + `PackageEntry.cs`，`ExportService` 用测试 partial 补上 `packageService` 字段，只在内存里编译执行）：合成 7 个条目 `Public/Shared/a.txt`、`Public/Shared/Sub/b.txt`、`Public/SharedDev/c.txt`、`Public/Sharedness/d.txt`、`Public/Other/e.txt`、`Public.txt`、`Readme.md`——新逻辑下 `Public/Shared` 恰好 2 个、`Public` 恰好 5 个（不含 `Public.txt`）、根节点 7 个、`Public/Shared/Sub` 1 个、大小写不同的 `PUBLIC/shared` 同样命中 2 个，全部通过；同一份数据用旧前缀谓词得到 4 个 / 6 个，缺陷可复现。
- 真实数据（`Shared.pak`，11,147 个条目、3,490 个文件夹）：每个文件夹节点用"按条目名逐段展开祖先目录"独立算出的期望集合逐一比对，**3,490 个文件夹 0 处不符**，根节点 11,147 个条目齐全。旧实现下 239 个文件夹会多导，最大一处 `Public/Shared` 旧 8,624 → 新 5,716；报告点名的场景实测存在：`Mods/Shared`（1,781 个）旧前缀连带 `Mods/SharedDev`（742 个）共 2,523 个，现在只剩 1,781 个。
- 真实数据不误伤：`Icons.pak`（14 个条目 / 17 个文件夹）与 `VirtualTextures.pak`（12,990 个条目 / 3 个文件夹）同样逐文件夹比对 0 处不符、根节点条目数完整（`Icons.pak` 里旧实现有 1 个文件夹会多导：`Public/Shared` 12 → 8）。
- Debug / Release x64 构建 0 错误；2 个警告均为既有项（`MSB3277` 版本冲突、ZeroQL 生成代码的 `CS0108`），无新增。
**未覆盖**：没有从 UI 实跑"选文件夹 → 导出"整条链路（文件夹导出在本机日志里从未被执行过，见 #8）；探针直接调用的是真实 `GetFolderFiles`，与导出时走同一条路径。

**17. 提供 `.tif` 过滤器但导出 `.tif` 必然失败**
`Services/ExportStrategies/ImageExportStrategy.cs:28` 列出 `.tif`，而 `Utils/FileExtensions.cs:16` 的 `BitmapImageFormats` 只有 `.tiff`，`Loader/ImageLoader.cs:45-46` 对 `.tif` 抛 `NotSupportedException`。修法：补 `.tif` 或移除过滤器。

**18. 任意 `.xml` 都允许"转换"为 `.loca`，必然失败（已修复并验证）**
`Services/ExportStrategies/LocalizationExportStrategy.cs:20-24,38-39`：非同名扩展一律走 `LocalizationLoader.LoadAsync`，而它只按二进制 loca 解析（`Loader/LocalizationLoader.cs:20`）。修法：只对 `.loca` 源提供 xml 输出。
**修复记录（2026-11-14）**：
- 按仓库既有的 `ExportOperation` 模式（`ImageExportStrategy` / `AudioExportStrategy` 同样写法）给 `LocalizationExportStrategy` 补上 `GetExportFilters` 与 `GetOperation`：`.loca → .xml` 为 `Convert`、同名扩展为 `RawCopy`、其余（含 `.xml → .loca`）为 `Forbidden`；`ExportAsync` 改为按 `GetOperation` 分派，`Forbidden` 直接返回 `false`、不写文件。
- 效果：`.loca` 源的保存过滤器仍是 `XML` + `Loca` 两项；`.xml` 源的过滤器只剩 `XML`，保存对话框里不会再出现注定失败的 `.loca`。
**验证**：
- 源码链入探针（真实 `LocalizationExportStrategy.cs` / `IExportStrategy.cs` / `LocalizationLoader.cs` / `FileOperations.cs` / `IPackageService.cs` / `PackageEntry.cs`，只把资源化的 `Strings` 换成常量替身、`IPackageService` 换成返回真实 pak 条目的桩）：
  - 操作矩阵：`.loca→.loca` RawCopy、`.loca→.xml` Convert、`.xml→.xml` RawCopy、`.xml→.loca` Forbidden、`.XML→.LOCA` Forbidden（大小写不敏感），全部符合预期。
  - 过滤器：`.loca` 源 = `.xml, .loca`；`.xml` 源 = `.xml`；`.XML` 源 = `.xml`；`Filters` 本身仍注册 `.xml, .loca`（`BuildStrategyDictionary` 依赖它）。
  - 真实数据 `English.pak` 的 `Localization/English/english.loca`（30,814,049 字节）：`.loca → .xml` 导出成功，产出 34,619,950 字节 XML，开头就是 `<?xml version="1.0" encoding="utf-8"?><contentList><content contentuid="h000006d4…"`；`.loca → .loca` 与 `.xml → .xml` 的输出 SHA-256 与源条目一致；`.xml → .loca` 返回 `false` 且目标文件未生成。
  - 反向证据：把导出的 XML 喂给 `LocalizationLoader.LoadAsync` 得到 `null`，说明旧代码那条 `.xml → .loca` 路径本来就必然失败（只是被吞成 "Export failed"）。
- Debug / Release x64 构建 0 错误，2 个既有警告（`MSB3277`、ZeroQL 生成代码的 `CS0108`），无新增。
**未覆盖**：没有从 UI 实跑"打开 `.xml` 条目 → 导出"去看保存对话框的过滤器（需要 UI 自动化）；探针直接调用的是真实的 `GetExportFilters` 与 `ExportAsync`。

**19. 预览命令执行期间 `CanExecute=false`，快速改选被丢弃（已修复并验证）**
`ViewModels/MainWindowViewModel.Preview.cs:20-31` + `Views/MainWindow.xaml:218-223`。
`AsyncRelayCommand` 默认禁止并发，而 `InvokeCommandAction` 先查 `CanExecute`（反编译证实）。大文件预览构建期间改选另一项 → 事件被静默丢弃，右侧显示 A、树选中 B，B 不会补触发。修法：最新请求胜出 + 取消令牌。
**修复记录（2026-11-14）**：
- 命令改为 `[RelayCommand(AllowConcurrentExecutions = true)]`：构建期间 `CanExecute` 保持 true，`InvokeCommandAction` 不再把选中事件丢掉。
- 预览仍然串行（同一个 pak 不并发读，避免 #1 的 solid 共享流问题）：`PreviewAsync` 只把最新选中记进 `_pendingPreviewNode`，已有构建在跑就直接返回；`ShowPendingPreviewAsync` 在每次构建结束后取走待处理节点，若构建期间又来了新选中，这个刚建好的 VM 会被释放而不是显示，然后继续构建最新的一个。面板仍在每次构建前清空（`DisposePreviewVmAsync`）。
- 没有引入 `CancellationToken`：`IPreviewService` / `IPreviewHandler`、8 个 handler 和底层 loader 都不接受令牌，真正中断构建要改整条管线；现在用"构建完再丢弃"达到同样的可见行为（被取代的预览不显示、最终显示最新选中），且不增加并发读取。
- XAML 无需改动（命令在构建期间不再禁用）。
**验证**：
- 源码链入探针（真实 `MainWindowViewModel.Preview.cs`；`MainWindowViewModel` 用测试 partial 提供 `_packageService` / `_previewService` / `PreviewVm`，`IPreviewService` 用可手动完成构建的桩，另用单线程 `SynchronizationContext` 模拟 WPF dispatcher，避免线程池让队列语义失真）：
  - 特性：`PreviewAsync` 上的 `RelayCommandAttribute.AllowConcurrentExecutions == true`（即构建期间命令保持可执行）。
  - 单次预览：选中 A → 构建完成后显示 A；改选 B → 面板立即清空并释放 A，B 构建完成后显示 B。
  - 构建期间快速改选：A 构建中依次选 B、C → 两次调用都立即返回（不阻塞），A 构建完成后其 VM 被释放、**不显示**，随后只构建 C（B 从未构建），C 完成后显示 C；请求序列实测为"首项 + 最新项"。
  - 选文件夹：清空面板、不发起构建；未加载 pak 时的选中被忽略。
- Debug / Release x64 构建 0 错误，警告仍是既有的 `MSB3277` 与 ZeroQL 生成代码的 `CS0108`，无新增。
**未覆盖**：没有真实 UI 自动化（需要在大文件预览构建期间点另一个条目再看右侧面板）；"`InvokeCommandAction` 不再丢弃事件"依据的是 `AllowConcurrentExecutions` 会让 `AsyncRelayCommand.CanExecute` 在运行期间返回 true 这一工具包语义，未在真实 WPF 消息循环里复跑。

**20. 预览处理器异常被伪装成"格式不支持"（已修复并验证）**
`Services/PreviewService.cs:90-95` 吞异常返回 null，`:42-43` 统一给"不支持预览"。损坏/加密/解码失败与真不支持无法区分。修法：区分 null 与异常，异常给"加载失败"。
**修复记录（2026-11-14）**：
- `CreatePreviewAsync` 的 catch 不再返回 null，改为 `CreateNotSupportedViewModel(LangKeys.LoadResourceFailed)`（"Failed to load resource" / "加载资源失败"）。异常现在在 `CreatePreviewViewModelAsync` 里是非 null 结果，不会再落到"File is Not Supported To Preview"。
- 只动异常分支：handler 返回 null、扩展名没有 handler 仍是"不支持预览"；`LowTex` 与包内条目缺失的既有校验不变；界面模板与资源文件未改（复用已有 `LoadResourceFailed` 文案）。
**验证**：
- 源码链入探针（真实 `PreviewService.cs` / `IPreviewHandler.cs` / `IPreviewService.cs` / `PackageEntry.cs` / `FileExtensions.cs`；`NotSupportedPreviewViewModel` 与 `LangKeys` 用最小替身，因为它们的成员由 source generator 生成）：
  - handler 抛 `InvalidDataException` → 返回携带 `HelpTextKey = "LoadResourceFailed"` 的占位 VM，且不是 `FileNotSupportedPreviewMessage`。
  - handler 返回 null → 仍是 `FileNotSupportedPreviewMessage`；扩展名没有 handler → 仍是 `FileNotSupportedPreviewMessage`。
  - handler 正常返回 VM → 原样返回（没有被包装）。
  - `icon_lowtex.dds` → `LowTexDDSNotSupportedMessage`（校验优先于 handler）；包内条目缺失 → `LoadResourceFailed`（既有行为）；文件夹节点 → `null`（不产生预览）。
- Debug / Release x64 全量重建（`-t:Rebuild`）各 0 错误、2 个警告（既有的 `MSB3277` 与 ZeroQL 生成代码 `CS0108`），无新增。
**未覆盖**：没有用真实损坏/加密的 pak 条目走完整 UI 看提示文案（探针直接调用 `PreviewService`）；`LoadResourceFailed` 与"包内条目缺失"共用同一文案，因此 UI 上仍区分不了"条目不存在"和"解码失败"（两者都归为加载失败）。

**21. 虚拟纹理工具箱索引不可取消，关闭后仍在已释放 VM 上跑（已修复并验证）**
`Dialogs/ViewModels/VirtualTextureToolboxDialogViewModel.cs:57`（`_ = IndexAsync()`）、`:161-170`（Dispose 不管索引）。索引完成后 `BuildRows()` 在已 Dispose 的 VM 上建行并触发预览。修法：加 CTS，Dispose 时取消，写 UI 前检查。
**修复记录（2026-11-14）**：
- `IndexAsync` 增加 `_indexCancellation`（`CancellationTokenSource`），`Dispose` 里 `CancelAsync()`；索引完成后的 `PopulateTextures()`、进度回调写 `IndexedCount`/`IndexTotal`、以及 `finally` 里的 `IsIndexing = false` 都先看 `IsCancellationRequested`，已释放的 VM 不再写 UI。
- `OperationCanceledException` 单独 catch，不再当成"索引失败"记 `Error`。
- 关闭对话框不会中断 catalog 的索引：`IVirtualTextureCatalog.LoadAsync` 不接受令牌，索引结果留给下次打开复用（catalog 由主 VM 共享）；这里只是不再往已释放的 VM 写状态。
**验证**：
- 反射探针（加载真实 `BG3PakViewer.Dialogs.dll`，使用真实 `VirtualTextureToolboxDialogViewModel` 与 `VirtualTextureEntry`；`IVirtualTextureCatalog` 用可控制完成时机的桩，其余服务参数传 `null`；单线程 `SynchronizationContext` 模拟 WPF dispatcher，保证 `await LoadAsync` 的续体按队列顺序执行）：
  - 打开对话框：`LoadAsync` 调用一次、`IsIndexing == true`；索引完成后 `Textures` 2 行、`SelectedRow` 选中第一项、`IsIndexing == false`。
  - 关闭对话框后再让索引完成：`Textures` 仍为 0 行、`SelectedRow` 为 null，即没有建行、也没有触发预览。同一 `PopulateTextures` 路径在未关闭时有 2 行（上一条），因此这个 0 行能区分出修复。
- Debug / Release x64 构建 0 错误、2 个既有警告（`MSB3277`、ZeroQL 生成的 `CS0108`），无新增。
**未覆盖**：没有拿真实 pak 走"打开工具箱 → 索引中关掉"的 UI 流程；探针里 catalog 的 `LoadAsync` 是桩，"关闭后索引继续在后台跑完并留给下次打开"是按接口语义推断的（真实 catalog 的 `ClearAsync`/`Dispose` 行为属 #22）。

**22. Catalog 的 Dispose 不等后台任务；释放后 `LoadAsync` 用已释放 CTS（已修复并验证）**
`VirtualTextures/Catalog/VirtualTextureCatalog.cs`、`:46-47`。索引进行中退出会读已关闭的包；释放后字段非 null（已释放），`??=` 不生效，取 `.Token` 抛 `ObjectDisposedException`。修法：释放时取消并等待（改为异步释放），释放后置 null。
**修复记录（2026-11-14）**：
- 释放改为异步：`IVirtualTextureCatalog` 只实现 `IAsyncDisposable`（不再有 `IDisposable`），`VirtualTextureCatalog.DisposeAsync` = 置 `_disposed` → `await ClearAsync()`（取消 → 等 `_loadTask` 结束并吞掉取消异常 → 清空条目与 `IsLoaded` → 取消源 `Dispose` 并置 null、任务也置 null）。于是"索引停了才关包"的保证保留，而调用方不再被阻塞。
- 退出流程改成可 await：`MainWindowViewModel` 实现 `IAsyncDisposable`（`DisposeAsync` 先 await catalog，再走原来的同步释放：extractor → package service → CTS → PreviewVm；同步 `Dispose` 保留给非退出路径，并用自己的 `_disposed` 保护避免重复释放）；`MainWindow.OnClosed` 里 await `DataContext` 的 `DisposeAsync`，然后 `Application.Current.Shutdown()`；`App.OnStartup` 因此把 `ShutdownMode` 改成 `OnExplicitShutdown`（不再随最后一个窗口自动退出），`App.Dispose(bool)` 不再释放主 VM——窗口自己负责，避免两个所有者。
- 选 `Closed` 而不是 `Closing`：`MainWindowBehavior` 在 `Closing` 里会弹"正在导出，确定退出？"，若在 `Closing` 里 `e.Cancel = true` 再 `Close()`，VM 已释放后这个提示会再弹一次，用户点取消就卡在"窗口还在但 VM 已死"；放在 `Closed`（窗口已关、不可取消）语义干净。
- 没有超时/兜底：等待期间不占用 UI 线程；异常时窗口仍会 `Shutdown()`（`try/finally`），异常照旧由全局处理器记日志。
- 实测补充：`.Token` 读已释放 CTS 在当前 .NET 11 上确实抛 `ObjectDisposedException`（C# 探针确认）；但旧代码 `Dispose` 后 `_loadTask` 仍非 null，`??=` 先短路、根本求值不到 `.Token`，所以旧行为的可观察结果是"`LoadAsync` 原样返回那个已取消的旧任务"。本项真正可复现的伤害是"不等后台任务"。
**验证**：
- 反射探针（加载真实 `BG3PakViewer.VirtualTextures.dll`、用真实 `VirtualTextureCatalog`；`IPackageService` 用桩，`GetFileByPath` 返回 `PackagedFileInfo { Solid = true, SolidStream = 慢速 MemoryStream }`，200 个 `.gts`、每次读停 200 ms，保证释放时索引仍在跑）：
  - 接口形态：`IVirtualTextureCatalog` 只继承 `IAsyncDisposable`，`VirtualTextureCatalog` 上没有同步 `Dispose` 方法（`hasSyncDispose=False`）。
  - `DisposeAsync()` **调用本身 1 ms 返回、任务未完成**（不阻塞调用线程）；`await` 等到索引停下共 **1840 ms**，完成后索引任务 `Canceled`，随后 600 ms 内 **0** 次读取；`IsLoaded=false`、`Entries=0`；二次释放立即完成；释放后再 `LoadAsync` 正常跑完（新 CTS，`RanToCompletion`）。
  - 空包/未加载路径：`LoadAsync` 后 `IsLoaded=true`，释放后 false 且 `Entries=0`；从未 `LoadAsync` 的实例释放不抛。
- Debug / Release x64 构建 0 错误、2 个既有警告（`MSB3277`、ZeroQL 生成代码的 `CS0108`），无新增。
**未覆盖**：没有用真实 pak + 真实窗口走"索引中关窗"的端到端流程（探针只覆盖 catalog，窗口/VM 的 await 链路靠编译与代码审查）；真实 pak 上释放等待时长未计时（最坏要等当前一批 `.gts` 解析结束）；改成 `OnExplicitShutdown` 后所有正常退出都必须经过 `MainWindow.OnClosed` 的 `Shutdown()`，启动失败路径仍走 `ReportStartupFailure` 的显式 `Shutdown(1)`。

**23. 页文件流在异常路径泄漏 / 非 seek 复制无上限（已修复并验证）**
`VirtualTextureTilePageFile` 构造中 `ReadChunkOffsetTables()` 抛异常时 reader/stream 不释放 —— 已随 #14 改为构造失败即 `Dispose`（现在校验会主动抛异常，这一步是必须的）。
`VirtualTextureTilePageFileHolder.cs:38-47` 剩下的问题：非 seek 流整份复制进内存时没有上限（`stream.CopyTo(buffer)`），畸形或超大页文件可能直接吃满内存。修法：复制前检查长度上限。
**修复记录（2026-11-14）**：
- 复制移到 `VirtualTextureTilePageFile` 自己（`CopyToMemoryIfNeeded` / `CopyToMemory`），因为页大小校验就在那里、上限可以直接用同一个 `MaxPages`：按 64 KiB 分块复制，超过 `MaxPages × pageSize` 就抛 `InvalidDataException` 并释放已复制的缓冲与源流。超过这个长度的页文件本来也会因页数 > `MaxPages` 被拒，复制下去没有意义。holder 只剩"关闭旧的、打开新的"这一件事。
- 顺带修掉两个真问题：
  1. **复制后没有复位 `MemoryStream.Position`**（旧代码的 `Stream.CopyTo` 同样以流末尾结束）→ 非 seek 页文件解析从流末尾开始，必然失败；探针在没有这一行时稳定复现 `Page 0 has no room for its chunk offset count`。现在复制完成后 `buffer.Position = 0`。
  2. **构造失败的兜底只覆盖了 `ReadChunkOffsetTables`**：`ValidatePageSize` 抛异常时（例如 gts 头把 PageSize 写成 0）流不会被关闭。现在整个构造体在一个 try/catch 里，任何失败都关流（reader 已创建时由 reader 关闭同一个流）。
**回归与恢复（2026-11-14）**：`d12cd9f`（"移除多余的流位置重置"）把这一行删掉了，非 seek 路径重新从流末尾开始读。用非 seek 的 3 页合成 GTP 走真实构造路径：删除后稳定复现 `InvalidDataException: Page 0 has no room for its chunk offset count`；恢复后同一探针构造成功并读出 chunk（`size=4`、`data=01-02-03-04`）。`dotnet build BG3PakViewer.slnx -m:1 -c Debug -p:Platform=x64` 0 错误。
**验证**：
- 反射探针（加载真实 `BG3PakViewer.VirtualTextures.dll`，用反射构造 `VirtualTextureTilePageFile` / `VirtualTextureTilePageFileHolder`；`pageSize=4096`，因此上限 = 4096 × 4096 = 16 MiB）：
  - 非 seek 3 页（12 KiB）→ 构造成功、源流已关闭、页文件 `Dispose` 正常；通过 holder 取页文件同样正常。
  - 恰好 4096 页（16 MiB）→ 构造成功；多一页（16 MiB + 4 KiB）→ `InvalidDataException`（`Page file is longer than the largest one of 4096 pages (16777216 bytes)`），源流已关闭。
  - 无限流 → 读到 **16,842,752** 字节（16 MiB + 一个 64 KiB 块）即抛，不再无限增长，源流已关闭。
  - `pageSize=0` → 页大小错误；非 seek 流 **0 字节**被复制（先校验、后复制）且已关闭，seek 流也被关闭。
  - seek 流照旧直接使用，页文件 `Dispose` 时流被关闭。
- Debug / Release x64 构建 0 错误，只剩既有的 `MSB3277` 与 ZeroQL 生成代码 `CS0108`。
**未覆盖**：没有拿真实 pak 里的非 seek 页文件做端到端抽取（探针是零填充的合成 GTP，只覆盖文件头/页布局与流生命周期）；上限取 `MaxPages × pageSize`，1 MiB 页大小下是 4 GiB——这是"读者能接受的最大值"，不是"小到能防内存打满"的界；要更紧的界需按真实最大页文件（445 页）另行决定。

**24.（证据已重写：原报告的触发面不成立；代码保留）瓦片范围下界取整方向**
`VirtualTextures/TileSets/VirtualTextureTileRangeCalculator.cs:75-83`：旧实现是下界 `ceil(origin/2^L)`、上界 `ceil((origin+span)/2^L)-1`，现改为下界 `floor`、上界 `(origin+span-1)/2^L`（两者只在 origin 不是 2^L 整数倍时有区别）。
**先更正原报告**：旧代码不是笔误，而是 LSLib `VirtualTileSet.ExtractTexture(level, layer, tex)` 的逐字移植（`ilspycmd -t LSLib.VirtualTextures.VirtualTileSet LSLib.dll` 反编译原文）：

```csharp
int num3 = tex.X / num;  int num4 = tex.Y / num2;      // origin，截断
int num5 = tex.Width / num;  int num6 = tex.Height / num2;  // span，截断
int minX = num3 / num7 + ((num3 % num7 > 0) ? 1 : 0);   // ceil(origin / 2^L)
int maxX = (num3 + num5) / num7 + (((num3 + num5) % num7 > 0) ? 1 : 0) - 1;
```

**真实数据普查（2026-11-14，`VirtualTextures.pak`，走项目自己的解析器 + 反射调用真实 `GetTileRange` / `RegionExists`）**：16 个 tile set、**15595** 张纹理、**46785** 个"纹理 × 层"。

| 观测 | 结果 |
| --- | --- |
| 解析到 level 0 / level>0 | 33195 / 13590（L1 11294、L2 1601、L3 449、L4 170、L5 43、L6 24、L7 3、L8 6） |
| level>0 中"正常尺寸"纹理 | 10590 |
| 其中 origin 是 2^L 整数倍 | **10590（全部）** |
| 新旧范围不同的正常尺寸纹理 | **0** |
| level>0 中"尺寸小于一格"的纹理 | 3000（≈982 张纹理 × 3 层） |
| 这 3000 个在旧代码下的范围 | **0 格（max = min−1，空）**，即每一层都返回 null ⇒ 整张纹理"没有数据" |

**结论（重写）**：原报告说的"回退到更粗 mip 就丢最左/最上一列"在这份数据里**一次都没发生**——正常尺寸纹理的 origin 全部对齐到 2^L，floor 与 ceil 给出完全相同的范围。这条改动唯一的效果是那 3000 个"尺寸小于一格"的组合：`span = tex.Width / 128` 截断成 0，LSLib 的公式于是得到 `max = ceil(o/2^L) − 1 = min − 1` 的空范围，旧代码把整张纹理判成"没有数据"；现行 floor 写法把它变成"包含它的那一格"（3000 个全部拿到 1 格，没有残留空范围）。
因此**代码保留**（对正常纹理零影响，把 3000 个"没有数据"变成可导出），但记录两点代价：① 在 origin 未对齐时偏离 LSLib，而这类纹理在这份数据里只出现在"小于一格"这一档（LSLib 对它们本来就返回 null）；② 真正的根因是 **span 截断**，floor 只是让它在未对齐时"歪打正着"，见 #54。
**未覆盖**：没有逐张比对导出的像素（这 3000 张都是子瓦片纹理，level 0 数据不存在，拿不到同层的真值）；也没有覆盖模组 pak 里 origin 未对齐 + 正常尺寸的组合（那份数据里不存在，只能确认代码会给出"包含首尾格"的超集范围）。

**25.（不修：判定本身就是对的）图标切分把"不透明但近黑"的像素当 padding——那些黑格正是补方用的填充**
`Icons/IconAtlasSlicer.cs:138-143` 要求 `A>16` 且任一 RGB `>16`，原报告据此认为"纯黑剪影会被整格丢弃"。**作者确认这是有意的**：大图集（8192×4096 那两张）把内容区补成正方形时，补出来的格子是**不透明的纯黑**，不是透明的；只按 alpha 判定会把成百上千个空黑格当成图标。
实测（`Icons.pak` 全部 14 个 `.dds`，解码走项目自己的 `ImageLoader`，网格走 `IconAtlasDetector`）：13 个图集检出网格、共 **8258** 个格子——内容格 **7171**、全透明格 **214**、不透明近黑格 **873**。873 个黑格全部满足 `maxAlpha=255` 且可见像素最亮通道只有 **0/4/12**（纯黑），其中 **798** 个位于最后一个内容格的右侧或下方、其余 **75** 个是最后一个**不完整行/列**的尾部（如 `Icons_Items_6` 第 10 行：前 2 格有内容、后 14 格纯黑；`Icons.dds` 第 7 行整行、第 6 行最后一格纯黑）。若改成"仅按 alpha"，这 873 个黑格会全部变成图标进入预览和导出，而**内容格一个都不会多**（7171 不变）。
另一个支持性数据：CCIcon 图集（`Generated_3d0c5ff8…_CCIcons.dds`）的格内内容铺满整格（抽样 5 格，亮像素包围盒与格子同宽 150、上下边距 0–10 px），说明"格内有黑边"不是图集常态，黑格只能是补方填充。
**处理：不改判定**，只在两处注释里写明"黑格是补方填充"的理由（`VisibleThreshold` 与 `IsVisibleContentPixel` 的 `<remarks>`），避免再次被当成缺陷。

**26.（不修）virtual texture 层统一按 DXT5 写盘，与 LSLib 自身导出行为一致**
`VirtualTextures/PageFiles/VirtualTextureChunkDecoder.cs`（`_outputSize` 按"全分辨率 + 半分辨率两平面"计算）与 `VirtualTextureWriter.cs:71`（固定 `dwFourCC = DXT5`）。
实测（`VirtualTextures.pak`，用项目自身 `ImageLoader` 解码导出的 DDS 并统计像素）：Albedo 层 alpha ≈ 252–255；**Normal 层 alpha 恒 ≈ 127、RGB 恒为 (0, 127, 229)**——是"两通道 BC5（X,Y）按 DXT5 解释"的特征。
**结论：不修。** 作者确认 LSLib 自身导出也是这个结果，属于与该上游导出器保持一致的既有行为；若将来要按层选择 BC5/ATI2，需要先确认游戏侧消费方的期望，不在本仓库范围内。
另一个实测现象：单张纹理（`240912d3…`，`Albedo_Normal_Physical_2.gts`）Albedo/Physical 层 alpha=0（全透明），同 tileset 其他纹理 alpha≈252 ⇒ 该层本身为空/透明，不是格式问题；这类"结构上有瓦片、像素全透明"的层按"默认导出三层"的规则照常导出。

**27. 主题切换不持久化**
`Views/MainWindow.xaml.cs:24-30` 只改 `ThemeManager`，`Models/AppSettings.cs` 无 Theme 字段 → 每次启动复位。修法：加设置项并在初始化时应用。

**28. 非法/空语言值在启动路径直接抛异常（已修复并验证）**
`App.xaml.cs:179-195`：`new CultureInfo(language)` 对 null/未知 culture 抛异常（启动路径没有 try，运行时切换语言反而有）。修法：`GetCultureInfo` + 回退。
现状（#11 修复后）：这个异常现在是"启动失败"对话框 + 退出码 1，不再是僵尸进程（本项也正好被用作 #11 的复现材料）；但启动本身仍然失败，仍需按这里修。

**修复记录（2026-11-14，`App.xaml.cs`）**：
- `InitializeCulture` 改为 `var cultureInfo = ResolveConfiguredCulture(appSettings.Language)` → 回写 `appSettings.Language = cultureInfo.Name` → `I18NExtension.Culture = cultureInfo`。回写从"只在空串分支"改成无条件：坏值在第一次启动就被改成可用的文化名，不会每次启动都重走回退。
- 新增 `ResolveConfiguredCulture(string?)`：`null`/空白直接走 `ICultureResolver.ResolveSupportedCulture()`（保留原来"空串 = 自动"的语义，也把"缺键/null"并进这条路）；非空用 `CultureInfo.GetCultureInfo(name)`，`CultureNotFoundException` 时记一条 `Warning` 再同样回退到 `ResolveSupportedCulture()`。
- 运行时切换语言那条路径（`SettingsManagerService.ApplyLanguageChange`）本来就有 try/catch，未改。
- **实际触发面比报告写的窄，但入口更隐蔽**（.NET 11 + ICU 实测）：手写的拼写错误多半**不会**抛——`"xx-XX"`、`"zh_Hans"` 都被接受（后者还被规范成 `zh_hans`），只有 `"Chinese"`、`"0"`、`"en-US.UTF-8"`、带尾空格、单字符、超长这类不合法名字才抛 `CultureNotFoundException`。现实中更可能的入口是**设置文件里没有 `Language` 键或显式 null**：`AppSettings` 的 `[JsonConstructor]` 参数是无默认值的 `string`，STJ 对缺失成员传 null（与 #10 里 `RecentItems` 完全同一机制；用最小 JSON 探针复现：缺键和显式 null 都得到 null）。

**验证**（Debug x64，真实应用，独立设置文件逐个启动→关窗→读回设置与日志）：

| 设置里的 Language | 主窗口 | 退出码 | 退出后设置里的 Language | 新增回退日志 |
| --- | --- | --- | --- | --- |
| 缺键 | 正常（中文） | 0 | `zh-Hans` | 无（空/缺键按"自动"处理） |
| `null` | 正常（中文） | 0 | `zh-Hans` | 无 |
| `""` | 正常（中文） | 0 | `zh-Hans` | 无 |
| `"Chinese"` | 正常（中文） | 0 | `zh-Hans` | `[WRN] Configured language Chinese is unknown; falling back` |
| `"ja"`（合法、未随包） | 正常（英文界面） | 0 | `ja`（不强制回退） | 无 |
| `"zh-Hans"` | 正常（中文） | 0 | `zh-Hans` | 无 |

- 修复前对照：#11 已记录同一份 `Language: null` 配置启动即抛 `ArgumentNullException`（`CultureInfo..ctor`），进程没有窗口（#11 修好后变成"启动失败"对话框 + 退出码 1）。
- `BG3PakViewer` Debug x64 构建 0 错误，1 个警告（既有 `MSB3277`），无新增。首版写法触发过 SonarAnalyzer `S6667`（catch 里记日志没带异常），已改成 `Log.Warning(ex, ...)` 消掉。
- 验证时踩到的环境坑（值得记住）：DSH 沙箱曾给仓库目录打上 `Low Mandatory Level` 完整性标签，从该目录启动的 exe 继承 Low IL，写 `%APPDATA%` 会被 Windows 完整性机制**静默拒绝**——Serilog 的 file sink 打开失败、设置保存的异常被 `catch` 吞掉，表现就是"应用能起、但既不写日志也不存设置"，很容易被误读成代码问题。清掉标签后同样的二进制立刻正常。

**未覆盖**：没有验"合法但未随包的文化名（如 `ja`）在设置对话框里的表现"（那里 `SelectedItem` 绑不上会显示空，属另一条 UX 问题）；没验 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`——实测该模式下**任何**名字（含 `en`）都抛 `CultureNotFoundException`，但那时 `CultureResolver` 构造函数里的 `new("en")` 会先抛，是一条独立路径，本项未改。

**29. `Assembly.Location` 在单文件发布时为空**
`Locales/CultureResolver.cs:29-30`：`Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!` 为 null ⇒ `GetDirectories(null)` 抛 `ArgumentNullException`，且在 try 之外。**待确认**：仓库无 `PublishSingleFile` 配置。修法：`AppContext.BaseDirectory` + 扫描整体 try。

**30. 语言回退用安装语言而非显示语言（已修复并验证）**
`Locales/CultureResolver.cs:65` 用 `InstalledUICulture`。装英文系统、显示语言改中文的用户永远回退英文。修法：优先 `CurrentUICulture`。

**修复记录（2026-11-14）**：
- `CultureResolver.ResolveSupportedCulture` 改为从 `CultureInfo.CurrentUICulture` 起算。`CurrentUICulture` 是用户选的显示语言（Windows“语言首选项”），`InstalledUICulture` 是系统安装语言，两者可以不同；注释里写明了为什么不能用后者。
- `Services/AppDiagnostics.cs` 的 `System UI Culture` 同步改记 `CurrentUICulture`：这一行原本的用途就是“把意外的英文界面追回到没有语言包的系统区域”，改完后它记录的才是真正参与回退的那个输入（否则日志里只有安装语言，误导排查）。

**验证**：
- 探针（真实 `CultureResolver.cs` / `CultureMatcher.cs` / `ICultureMatcher.cs` / `ICultureResolver.cs` 链入 `Add-Type` 编译，模拟程序目录只有 `it` / `zh-Hans` / `zh-Hant` 三个文化目录外加一个非文化名目录 `runtimes`（被忽略），并用 `AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", …)` 把扫描目录指过去；同一次运行里另编一份“旧实现副本”用 `InstalledUICulture` 做对照）：

| 显示语言（`CurrentUICulture`） | 修复后 | 旧实现（本机安装语言 zh-CN） |
| --- | --- | --- |
| `en-US` | `en` | `zh-Hans` |
| `zh-CN` | `zh-Hans` | `zh-Hans` |
| `zh-Hans-CN` | `zh-Hans` | `zh-Hans` |
| `zh-TW` | `zh-Hant` | `zh-Hans` |
| `it-IT` | `it` | `zh-Hans` |
| `ja-JP`（模拟目录里未随包） | `en` | `zh-Hans` |
| `de-DE`（模拟目录里未随包） | `en` | `zh-Hans` |

  - 旧实现对**任何**显示语言都返回本机安装语言 `zh-Hans`——“显示英文界面却得到中文”正是本项缺陷的镜像；修复后按显示语言匹配，只有模拟目录里没有对应文化目录的语言才回退 `en`。
- 真实输出目录复核（直接 `LoadFrom` 最终构建产物 `BG3PakViewer\bin\x64\Debug\…\BG3PakViewer.Locales.dll`，把 `AppContext.BaseDirectory` 指到该目录）：`SupportedCultures` = `en, cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, tr, zh-Hans, zh-Hant`；显示语言 `zh-CN → zh-Hans`、`en-US → en`、`zh-TW → zh-Hant`、`it-IT → it` 全部符合预期，即本项修复在真实目录布局下同样生效。
  - 附带观察（**既有问题，本项未改**）：`cs`/`de`/`es`/`fr`/`ja`/`ko`/`pl`/`pt-BR`/`ru`/`tr` 这些目录是框架/第三方包的语言资源，不是本应用的语言包，但现有目录扫描把它们也算成“受支持”；于是显示语言是 `de` 时解析结果是 `de`（应用没有德语包，界面回退到中性英文）。这属于 `SupportedCultures` 的口径问题（与 #29 同一段扫描代码），要修需另开项。
- 语义对照（Win32 实测）：`GetUserDefaultUILanguage` = `GetSystemDefaultUILanguage` = `zh-CN`，`CultureInfo.CurrentUICulture` = `CultureInfo.InstalledUICulture` = `zh-CN`。即 .NET 的 `CurrentUICulture` 就是“用户默认 UI 语言”，本机两者相同 ⇒ 无法在这台机器上自然复现“安装语言 ≠ 显示语言”，探针只能直接改 `CurrentUICulture` 来模拟显示语言（代码读取的正是这个值）。
- 构建：`dotnet build BG3PakViewer.slnx -m:1 -c Debug -p:Platform=x64` 0 错误（警告为既有项）。
- **未覆盖**：没有在“英文系统 + 中文显示语言”的真实机器上启动 GUI 复看界面语言；探针直接调用 `ResolveSupportedCulture`，而启动路径 `App.InitializeCulture → ResolveConfiguredCulture` 调用的正是同一个方法。另外 `zh-CN` 在 `CultureMatcher` 里靠父文化 `zh` 匹配到 `zh-Hans`，这一步依赖语言包目录的枚举顺序（真实输出目录里 `zh-Hans` 按字母序在 `zh-Hant` 之前，所以稳定取到 `zh-Hans`；但目录枚举顺序不是 API 保证的），属既有的匹配歧义，本项未改。

**31. 脚本预览 VM 在后台线程构造，且 fire-and-forget 写属性（已修复并验证）**
`Services/PreviewHandlers/StoryScriptsPreviewHandler.cs:39-48`（`Task.Run` 里 new VM）+ `Controls/ViewModels/StoryScriptsPreviewViewModel.cs:111-164`（`_ = DecompileScriptsAsync()`，续写 `Scripts` 在线程池）。疑点（与 WPF 标量绑定调度有关）：首次打开脚本面板可能为空；连点目标时旧任务可覆盖新结果；`MakeScript` 同步跑在 UI 线程会卡。

**修复记录（2026-11-14，`StoryScriptsPreviewHandler.cs` / `StoryScriptsPreviewViewModel.cs`）**：
- `CreatePreviewViewModelAsync`：`Task.Run` 只包 `new StoryReader().Read(stream)`，VM 改由 `await` 的续体（即调用方的 UI 线程）构造并 `{ Story = story }`，与 `TextBasedPreviewHandler` 同一写法。构造要填绑定用的 goal 集合、并启动第一次解压，两者都必须在 UI 线程。
- `StoryScriptsPreviewViewModel`：`_ = DecompileScriptsAsync()` → `_ = ShowScriptsAsync(value)`；`MakeScript` 与 `TruncateToLines` 一起移进 `Task.Run`（原来 `MakeScript` 同步跑在属性赋值里）；方法从 UI 线程启动、`await` 不带 `ConfigureAwait(false)` ⇒ `Scripts` 写回 UI 线程。
- 新增 `_scriptsVersion` 递增序号：只有“仍是最新一次请求”的解压结果才写 `Scripts`，被后一次选择超过的结果丢弃。
- `MakeScript` 的异常（LSLib 的 `Value.MakeScript` 对未知值抛 `InvalidDataException`）现在记 `Log.Error` 并清空脚本，不再是未观察的任务异常。
- **未改**：`Story` 属性仍可被任意线程赋值（当前唯一写入点就是上面的构造）；导出路径本来就在 `Task.Run` 里跑 `MakeScript`。

**验证**（探针：真实 `Gustav.pak` → `Mods/Gustav/Story/story.div.osi`，382 goals / 56092 nodes；用 `DispatcherSynchronizationContext` + `Dispatcher.PushFrame` 扮演 WPF UI 线程，记录每次 `PropertyChanged` 的线程号；另把“去掉 `_scriptsVersion` 守卫”的副本放进同一进程做 A/B）：

| 观察 | 修复前写法（对照） | 修复后 |
| --- | --- | --- |
| 首次打开 | VM 在 `Task.Run` 里 new，解压启动于线程池（4/6 号线程） | 解压启动于 UI 线程（2 号线程），28–39 ms 后得到 55374 字符，与直接 `MakeScript` 逐字符一致 |
| `Scripts` 通知线程 | 从线程池设置 `SelectedGoal` 时由 6 号线程写回 | 42 次通知全部在 UI 线程 |
| 40 轮随机 A→B 切换 | 最终显示**旧** goal 的文本 18 / 22 轮（两次运行） | 两次运行均 **0** 轮 |
| 20 轮“慢 goal→快 goal”（20.4 ms → 0.45 ms） | **20/20** 被旧结果覆盖 | **0/20** |
| 选中 goal 的属性赋值耗时 | 同步跑 `MakeScript`，单个 goal 0.45–22 ms（中位 0.85 ms） | 0.02 ms |
| `Story` 赋值（填 382 个 goal） | 后台线程 | UI 线程，4.6 ms |

- 构建：`dotnet build BG3PakViewer.slnx -m:1` 0 错误（余下为既有警告）。
- **待确认项已解决**（独立探针，WPF 行为）：WPF **会**把标量 `INotifyPropertyChanged` 的目标更新调度到 UI 线程——后台线程设值后，绑定的 `TextBox.Text` 变成新值、无异常。所以旧写法不是“通知被丢弃/抛异常”（这也解释了“首次打开为空”为什么没能复现：`Scripts` 最终仍会显示），真正的问题是非 UI 线程写回依赖 WPF 的隐式调度，而“最后一次结果”的语义没有任何保护。
- **未覆盖**：没有在真实窗口里点 goal 做 UI 自动化（探针驱动的是真实 VM + 真实 story，只是自己 pump dispatcher）；没在更大的 dev 剧本上复测耗时；`Goals` 现在在 UI 线程填充（382 项 4.6 ms），几千个 goal 的剧本没测。

**32. 在后台线程给已绑定的 `PackageTree` 赋值（已修复并验证）**
`MainWindowViewModel.Package.cs:102-107`、`MainWindowViewModel.Search.cs:51-56,68`。集合在后台线程构造后直接赋给绑定属性；标量通知跨线程。修法：后台只做构建，赋值回 UI 线程。

**修复记录（2026-11-14，`MainWindowViewModel.Package.cs` / `.Search.cs`）**：
- `OpenPackageAsync`：`PackageTree = null` 从 `Task.Run` 内部移到外面（UI 线程）；后台任务只做"清理 + 加载 + 建树"并**返回**新树，回到 UI 续体后再 `PackageTree = tree`。`LoadAndBuildTreeAsync` 由返回 `bool` 改成返回 `ObservableCollection<PackageEntry>?`（失败为 null），调用点按 `tree is not null` 分支，失败时的提示框与"不替换树"的语义不变。
- `SearchTreeAsync` / `ClearTreeSearchAsync`：`Task.Run` 里只留 `_packageService.BuildTree(...)`，赋值放到 await 之后的 UI 续体；两条搜索日志也回到 UI 线程（原来整段都在后台任务里）。
- 早退条件未动（未加载包 / 非空查询）。

**验证**（源码链入探针：真实 `MainWindowViewModel.Package.cs` + `.Search.cs`，测试 partial 补 `PackageTree` / `IsVirtualTexturePackage` 等成员；`IPackageService` 等用桩，`IDialogService` 用 `DispatchProxy` 返回默认值；用单线程 `SynchronizationContext` 扮演 WPF dispatcher，记录每次 `PropertyChanged` 的线程号）：
- 打开包（加载成功）→ 树 1 项，`PackageTree` 事件 1 次，**非 UI 线程 0 次**（线程 2 = UI 线程）。
- `SearchTreeAsync("abc")` → 树 1 项，事件 1 次，非 UI 0 次。
- `ClearTreeSearchAsync("")` → 树 1 项，事件 1 次，非 UI 0 次。
- `ClearTreeSearchAsync("abc")`（非空）→ 直接返回，`BuildTree` 调用次数不变，0 次事件。
- 打开包（加载失败）→ 树被清成 null，事件 1 次，非 UI 0 次；错误框走 `IDialogService` 桩。
- **对照**：把同一段搜索的赋值放回 `Task.Run` 里（修复前的写法）→ 事件落在 6 号线程，非 UI **1 次**，证明探针确实能区分两种写法。
- 两个文件随探针编译 0 错误；完整解决方案构建当时被**另一处正在进行中的改动**挡住（`BG3PakViewer.Dialogs/ViewModels/VirtualTextureToolboxDialogViewModel.cs` 缺 `MessageBoxIcon` / `ShowMessageBoxNotifyAsync` 的引用），与本项无关。

**未覆盖**：没有在真实窗口里点搜索框做 UI 自动化（那里只能看出"最终树对不对"，看不出赋值线程）；`PackageTree` 的消费方（TreeView 绑定）未改。

**33. Extractor 打开新 tile set 失败后，缓存仍指向已释放对象（已修复并验证）**
`VirtualTextures/Extraction/VirtualTextureExtractor.cs:85-96`：先 `Dispose` 旧的再创建新的，创建失败时字段仍指向已释放对象 → 该 session 内该 tile set 一直 `ObjectDisposedException`。`Dispose()`（`:74-77`）也不取 gate。修法：创建前先清字段。

**修复记录（2026-11-14）**：
- `OpenExtractorAsync`：释放旧的之后、解析新的之前，把 `_cachedExtractor` 与 `_cachedExtractorPath` 置空。解析抛异常时缓存为空，下一次调用会重新解析，而不是把刚释放的实例交出去。
- `Dispose()` 释放后同样清空这两个字段，缓存不会再持有已释放对象。
- **未改**：`Dispose()` 仍不取 `_gate`。同步等 gate 会和回投 UI 线程的续体互相卡住——`VirtualTextureLayerExtractor.ExtractAsync` 的 `await Task.Run(...)` 没有 `ConfigureAwait(false)`，续体要回 UI 线程才能让任务完成，而 UI 线程正在 `Dispose` 里等 gate。真要修得把 `IVirtualTextureExtractor` 改成 `IAsyncDisposable`、让关闭路径 await，超出本项范围。

**验证**（真实 `VirtualTextures.pak` 里的 `Albedo_Normal_Physical_2.gts`，仓库真实源码链入 `Add-Type` 编译；同一次运行里另编一份"修复前副本"做对照）：
- 场景：先用真实 tile set 成功打开一次（`HasLayerDataAsync` 返回 true），再请求一个不存在的 tile set（抛 `FileNotFoundException`），最后再请求原来那张 tile set。
- **修复后**：失败后 `_cachedExtractor = null`、`_cachedExtractorPath = null`；再次请求重新解析并成功，返回的是新实例（`ReferenceEquals` = false）。
- **修复前副本**：失败后缓存仍指向旧路径、旧实例；其内部 tile set 的流已经 `CanRead = false`（被 Dispose 过）；再次请求原样返回这个已释放实例（`ReferenceEquals` = true）——就是缺陷描述的"该 session 内一直拿到已释放对象"。
- Debug / Release x64 构建 0 错误（只剩既有 `MSB3277` / ZeroQL 生成的 `CS0108`）。
- **未覆盖**：没有在真实 UI 里制造"打开坏 tile set 再切回好 tile set"的动作；探针直接调用 `VirtualTextureExtractor.HasLayerDataAsync`，与工具箱走同一条打开路径。

**34. 单个 `.gts` 解析失败被吞成"0 个纹理"（已修复并验证）**
`VirtualTextures/Catalog/VirtualTextureCatalog.cs` 的 `ReadTileSetOrEmpty`（拆分前是 `IndexEntries` 的内联 catch）。损坏 tile set 与"没有纹理的 tile set"在 UI 上完全一致，解析回归发现不了。修法：收集失败项并提示。

**修复记录（2026-11-14）**：
- 新模型 `Models/VirtualTextureTileSetFailure.cs`（`GtsPath` + `Reason`）；`IVirtualTextureCatalog` / `VirtualTextureCatalog` 新增 `FailedTileSets`，按包内顺序列出解析失败的 tile set，`ClearAsync` 一并清空（换包不会留下上一个包的失败项）。
- `ReadTileSetOrEmpty` 改为返回 `(条目, 失败)` 元组，`ReadEntries` 收集失败项；失败时在原有逐条 `Warning` 之外再记一条汇总日志（`Failed to index {N} of {M} virtual texture tile set(s)`），这样即使没人打开工具箱，日志里也能发现解析回归。
- `VirtualTextureToolboxDialogViewModel`：索引结束、列表填好后，若有失败项就弹一条 Warning 消息框，逐条列出 `GtsPath: 原因`；对话框期间被关闭（取消）时不弹。新增 `VirtualTextureIndexFailedCaption` / `VirtualTextureIndexFailedMessage` 两条文案，en / zh-Hans / zh-Hant / it 四份 resx 与 `Strings.Designer.cs` 同步补齐。

**验证**：
- 探针（真实 tile set：`VirtualTextures.pak` 里 82,837,504 字节的 `Albedo_Normal_Physical_2.gts`；坏 tile set：12 字节垃圾"gts" 头）：
  - 混合包（好 + 坏）：`IsLoaded = true`、`Entries = 987`（全部来自好 tile set）、`FailedTileSets = 1`，记录的是 `broken/tile.gts` 与解析异常文本；好 tile set 的条目一个不少。
  - 只有坏 tile set：`Entries = 0`、`FailedTileSets = 1`（带原因），不再与"没有纹理"混为一谈。
  - `ClearAsync()` 之后 `IsLoaded = false`、`Entries = 0`、`FailedTileSets = 0`。
  - 四种语言的文案都取到正确文本（en / zh-Hans / zh-Hant / it），模板占位 `{0}` 原样保留。
- Debug / Release x64 构建 0 错误（只剩既有 `MSB3277` / ZeroQL 生成的 `CS0108`）。
- **未覆盖**：没有在真实 UI 里打开工具箱看那条消息框（探针覆盖到 catalog 的失败收集与四条文案；VM 的调用点靠编译 + 与既有 `ReportExportResultAsync` 相同的 `ShowMessageBoxNotifyAsync` 用法）；没有构造"结构合法但零纹理"的 tile set 做对照（游戏数据里没有这种 tile set，探针也没有现成构造手段，只验证了失败项被单独记下）。

## P3：卫生与低风险（35–50）

**35.（已修复并验证）** 早期 logger 被直接覆盖、未 `CloseAndFlush`；`OnExit` 也不关 `Log.Logger` → 启动/退出期日志可能丢失、句柄占用到进程结束（`App.xaml.cs:251-259 → 163-169`）。

**修复记录（2026-11-14，`App.xaml.cs`）**：
- `InitializeLogging` 在建新管线之前先 `Log.CloseAndFlush()`。原因比"没 flush"更硬：早期 logger 写的是**同一个** `log.txt`，Serilog 的 File sink 打开文件后不允许另一个 sink 再打开它，而失败是**静默的**（SelfLog 默认关闭）——实测"早期写过一行"之后，新管线一条日志也写不出来。先关闭既冲掉了启动期内容，也把文件交还给正式管线。
- `OnExit` 两条分支都 `Log.CloseAndFlush()`（原先初始化分支根本不碰 `Log.Logger`，未初始化分支直接 `return`）：退出前冲刷缓冲并释放句柄。顺带把 `if (!_initialized) { ...; return; }` 改成 if/else，让 `base.OnExit` 只有一处。
- 触发面（如实）：正常启动早期 logger 一条都不写，所以没有建管线前的日志时无害；目前唯一会早期写日志的是"已有实例在运行"那条路径，而它不会创建完整管线，因此尚未造成实际丢日志。真正危险的是"以后有人在 `InitializeLogging` 之前写日志"，那会让整轮运行没有任何日志。

**验证**：
- 探针（真实 `Serilog` + `Serilog.Sinks.File`，File sink 配置与 `App` 完全一致，两个场景对照）：
  - 场景 A（修复前的顺序：早期 logger 写过一行后被直接替换）→ 文件里**只有**早期那一行，`完整管线写下的日志` **丢失**。
  - 场景 B（修复后的顺序：先关闭早期 logger 再建管线）→ 两行都在；退出后日志文件可用 `FileShare.None` 独占打开（句柄已释放）。
  - 附带确认：早期 logger 持有句柄期间 `File.ReadAllText` 会直接抛 `IOException`，必须 `FileShare.ReadWrite` 才能读——这也是该缺陷平时不易察觉的原因。
- 真实应用（Debug x64）：启动 → 日志追加了本次会话的启动信息（`Application Version`、`Installed Language Packs` 等）→ 关窗退出码 0 → 退出后日志文件可独占打开。
- 构建：0 错误、2 个既有警告（`MSB3277`、ZeroQL 生成代码的 `CS0108`）。
- **未覆盖**：没有制造"应用内真的在 `InitializeLogging` 之前写日志"的场景（正常路径下早期 logger 不写任何东西，只能用同配置探针复现机制）；`OnExit` 的未初始化分支（第二实例）只做了代码路径确认，没跑 UI 自动化。

**36. 单实例检测 `Process.GetProcessesByName` 只取一个进程，其余不 `Dispose`；同名无关进程也可能被当目标窗口（已修复并验证）**
`Services/SingleInstanceManager.cs:67-72`。

**修复记录（2026-11-14）**：
- `FindExistingProcessInstance`（返回 `Process`）改成 `FindExistingInstanceWindow`（返回窗口句柄），枚举出的每个 `Process` 都在 `finally` 里 `Dispose`——调用方不再接管 `Process` 对象，也就没有"只释放选中的那一个、其余留在那里"的口子。
- 候选要同时满足两点才算运行中的实例：① 可执行文件路径与本进程不同则跳过（同名但不同路径的进程不是这个实例）；② 主窗口句柄非 0（没有窗口的同名进程无法激活，旧代码会把 0 传进 `GetWindowPlacement` 再报一条 warning）。路径读不出来时（例如对方以更高权限运行）仍按"名字相同"接受，否则提权实例永远无法被激活。
- `ActivateExistingInstance` 找不到窗口时只记一条 `Log.Warning`，不再调用 `ActivateExistingInstanceWindow(0)`。

**验证**（真实 `BG3PakViewer.Services.SingleInstanceManager` 从刚编译的程序集里反射调用；替身进程与它同名，由探针自己的副本充当"另一个实例"）：
- A 同路径 + 有窗口：返回的 HWND 属于该进程（`GetWindowThreadProcessId` 核对），PASS。
- B 只有另一个路径下的同名实例：返回 0（忽略）；旧逻辑返回那个进程的 PID 与窗口（`window=0xF40BDC`）——旧代码会去激活一个同名但不同路径的窗口。
- C 只有同路径但没有窗口的实例：返回 0；旧逻辑返回该进程且 `MainWindowHandle == 0`，`ActivateExistingInstanceWindow` 会把 0 传给 `GetWindowPlacement`。
- D 同路径有窗口、另一路径也有窗口同时存在：返回同路径那个实例的窗口，PASS。
- E 两个候选都在场时连续 200 次查找，当前进程句柄数增量 **0**；每次查找都会为每个候选读 `MainModule`（开句柄），若没释放应增长约 400。旧逻辑同样 200 次增量也是 0——它从不读 `MainModule`，所以"其余不 Dispose"在旧代码里主要是托管对象卫生问题，不是可观测的句柄泄漏。
- Debug / Release x64 构建 0 错误（只剩既有 `MSB3277`）。
- **未覆盖**：没有真的启动两个应用实例走 UI；探针模拟的是"同名进程 / 窗口 / 路径"这一层判据。边角取舍：同一份构建放在不同目录的副本（彼此共用互斥量）现在会被当作"无关进程"跳过——第二份实例仍会正常退出，只是不会把第一份拉到前台。


**37.（已修复并验证）** 拖放只认小写 `.pak`，`Foo.PAK` 静默无效（`MainWindowViewModel.Package.cs:36`）。

**修复记录（2026-11-14，`MainWindowViewModel.Package.cs`）**：
- 门禁由模式匹配 `Path.GetExtension(file) is not ".pak"`（string 模式、序数比较、区分大小写）改为 `!Path.GetExtension(file).Equals(".pak", StringComparison.OrdinalIgnoreCase)`，与 `Utils/FileExtensions.cs` 里其余扩展名判定同一写法。
- 只改比较方式，范围不变：仍只取第一个拖入文件，仍不认目录与无扩展名文件，被拒的拖放依旧静默返回（未新增提示）。

**验证**（探针：`Add-Type` 编译 `DispatchProxy` 桩件，`Assembly.LoadFrom` 加载真实的 `BG3PakViewer.dll`，反射构造真实 `MainWindowViewModel`——12 个依赖全为桩——再调用该类型实现的 `IDropTarget.Drop`，即拖放库实际调用的同一个方法；以 `IPackageService.LoadPackageAsync` 是否被调用及其参数判定门禁结果，以 `PackageTree` 是否发布判定是否真的走完整条打开流程）：

| 拖入路径 | 修复前 | 修复后 |
| --- | --- | --- |
| `D:\mods\Foo.pak` | 打开 | 打开 |
| `D:\mods\Foo.PAK` | **不打开** | 打开 |
| `D:\mods\Foo.PaK` | **不打开** | 打开 |
| `D:\mods\Foo.zip` | 不打开 | 不打开 |
| `D:\mods\FooPAK`（无扩展名） | 不打开 | 不打开 |
| `D:\mods\Folder`（目录） | 不打开 | 不打开 |

- "修复前"列用的是改动之前构建的 `bin\Debug\...\BG3PakViewer.dll`，"修复后"列用的是重新构建的 `bin\x64\Debug\...\BG3PakViewer.dll`；同一份探针、同一批用例，只有大小写两例结论不同。修复后 `.pak` / `.PAK` / `.PaK` 三例都确认 `LoadPackageAsync` 收到原样的路径、且 `PackageTree` 已发布。
- Debug / Release x64 构建 0 错误；警告与改动前一致（`MSB3277`、ZeroQL 生成代码的 `CS0108`）。
- **未覆盖**：没有在真实 UI 里用鼠标执行一次 OLE 拖放手势（探针调用的是同一个 `Drop` 实现；`DragOver` 的 `Effects = Copy` 与本项无关，未一并验证）。

**38. 网格对分页视图用 `Mode=TwoWay`（已修复并验证）**
`Dialogs/Views/LogDialog.xaml:67`、`RecentDialog.xaml:72` 对 `PagedSource` 用 `Mode=TwoWay`，与 `Controls/DataTemplates/LocalizationPreviewTemplate.xaml:20` 的 `OneWay` 矛盾（**待确认**：Syncfusion 35.1.46 是否回写）。

**修复记录（2026-11-14，`LogDialog.xaml` / `RecentDialog.xaml`）**：
- 两处改成 `Mode=OneWay`，与本地化预览一致：pager 是 `PagedSource` 的唯一写入者，网格只读。
- 原报告“只读的 `PagedSource`”这一前提不成立（反编译 `Syncfusion.SfGrid.WPF 35.1.46`）：`SfDataPager.PagedSource` 是**读写** DP（有 `set` 走 `SetValue`），`PropertyMetadata` 也没有变更回调，所以 `TwoWay` 不抛绑定错误；缺陷是它开了一条没有消费者的回写通道。
- **未改**：网格自己的 `ItemsSource` 语义、pager 的 `Source` 绑定都没动。

**验证**（探针：真实 `RecentDialog` / `LogDialog` 实例，WPF `Application` + 真实 Syncfusion 控件，绑定与行数直接读回）：
- 修复后两个对话框：`ItemsSource` 绑定 `Mode=OneWay Path=PagedSource ElementName=SfDataPager`；分页照常——`pager.PagedSource=GridPagedCollectionViewWrapper(30 items)`、网格 30 行（`PageSize=30`）。
- 回写通道 A/B（同一份代码，只在运行时把绑定模式换掉）：在代码里给网格赋一次 `ItemsSource`——
  - `OneWay`：`pager.PagedSource` **未被替换**（`ReferenceEquals` = False），分页视图原样保留；
  - `TwoWay`：`pager.PagedSource` 被换成那次赋值的 3 项集合（`ReferenceEquals` = True），而 `PagedSource` 的 DP 没有变更回调 ⇒ pager 不会重建分页视图，只会与网格静默对不上。
- “Syncfusion 是否回写”（待确认项）：反编译全量 `Syncfusion.SfGrid.WPF` 后检索，只有 master-detail 的克隆网格（`DetailsViewGridManager`）和 `GridQueryableCollectionViewWrapper` 会给自己的 `ItemsSource` 赋值，分页路径不赋；所以原报告描述的矛盾当前是**潜在**的，不是可见故障，改 `OneWay` 是把它堵成无入口。

**39. `IsChecked={x:Null}` 死触发器（已修复并验证）**
`Dialogs/Views/RecentDialog.xaml:23` 的 `PinCheckBoxTemplate` 里 `<Trigger Property="IsChecked" Value="{x:Null}">`（删除前在 `:33`）在非三态 CheckBox 上永不成立（死触发器）。

**修复记录（2026-11-14）**：删除该触发器（连同它唯一的 `Setter`，其值 `&#xE734;` 与 `iconElement` 的默认 `Glyph` 相同，本来就是重复项）；`IsChecked=True` 的触发器保留。

**验证**（探针：加载真实 `RecentDialog`，读 `Window.Resources["PinCheckBoxTemplate"]`）：
- 修复后 `ControlTemplate.Triggers` = **1** 个：`IsChecked=True`。
- 该触发器不可能命中的依据：数据源 `IRecentFileEntry.IsMarked` 是 `bool`（实测 `PropertyType=Boolean`），`CheckBox.IsThreeState` 默认 `False`，绑定只会写 true/false，`IsChecked` 取不到 null。

**40. 固定行高把换行文本裁成一行（已修复并验证）**
`Controls/DataTemplates/LocalizationPreviewTemplate.xaml:24-25` 固定 `RowHeight="28"` + `WrapWithOverflow`，且未挂 `SfDataGridRowHeightBehavior` → 长文本被裁成一行。

**修复记录（2026-11-14）**：
- 模板挂 `SfDataGridRowHeightBehavior MinHeight="28"`：短行仍是 28，长行按自动高度长高。`MinHeight` 用字面量而不是 `ElementName` 绑定——在 `DataTemplate` 里行为没有 namescope，绑定解析不到（对话框里的同名绑定能解析，因为它们在 Window 的 namescope 里，实测那边 `MinHeight=30`）。
- **附带修复**：`SfDataGridRowHeightBehavior` 与 `SfDataGridSearchBehavior` 都在 `Unloaded` 里 `SearchHelper.Dispose()` + `grid.Dispose()`；两者同时挂在同一个网格上（本模板就是）时第二次 `Dispose()` 会在 Syncfusion 内部抛 `NullReferenceException`，即每次切换预览一次。新增 `Shared/Extensions/SfDataGridExtensions.DisposeOnce`（`ConditionalWeakTable` 记录已释放的网格），两个行为改调它；`SfDataGridSearchBehavior` 原有的 `_disposed` 与消息注销逻辑保留。
- 附带效果的收益：三个用到该行为的界面（两个对话框 + 本地化预览）现在都走同一条释放路径。

**验证**（探针：模板内容放进 `ContentControl`，反复替换/清空来模拟“切换预览”；修复前模板用 `git show HEAD:…LocalizationPreviewTemplate.xaml` 重新 `XamlReader.Parse` 做对照；行高取网格 `QueryRowHeight` 的最终值，自动高度取 `GridColumnSizer.GetAutoRowHeight`）：

| 观察 | 修复前（HEAD 模板） | 修复后 |
| --- | --- | --- |
| 挂上的行为 | 只有 `SfDataGridSearchBehavior` | `SfDataGridRowHeightBehavior(MinHeight=28)` + 搜索行为 |
| 长文本行（row2）的自动高度 | 48 | 48 |
| 该行实际采用的高度 | **28**（文字被裁） | **48** |
| 行高上限（50 行样本） | 28（所有行恒定） | 96 |
| 短文本行 | 28 | 28（低于 MinHeight 不缩小） |
| 卸载/重载 3 轮 | 0 次异常（只有搜索行为）/ **3 次** `NullReferenceException`（两个行为都在） | **0 次** |

- 卸载/重载的对照还确认了异常来源：只留行高行为 0 次、只留搜索行为 0 次，两个都在才每次卸载抛一次 ⇒ 双重 `Dispose()`。
- 绑定错误 0 条（`MinHeight` 改字面量后不再出现 “Cannot find governing FrameworkElement” 那条）。
- `dotnet build BG3PakViewer.slnx -m:1` 0 错误。
- **未覆盖**：没有在真实预览面板里切文件做 UI 自动化（探针用 `ContentControl` 的卸载/重载模拟，与模板被替换时的生命周期一致）；行高只用自造的 50 条本地化条目测，没跑真实 `.loca`；没有测“搜索框过滤 + 自动行高”同时生效时的行高（过滤会重建视图）。

**41.（已修复并验证）** `Dialogs/Comparers/RuntimeLibraryVersionComparer.cs:28-36`：解析失败回退字符串比较时未按 `SortDirection` 取反，null 入参 NRE。

**修复记录（2026-11-14，`BG3PakViewer.Dialogs/Comparers/RuntimeLibraryVersionComparer.cs`）**：
- `Compare` 只做一件事：取两个版本比较结果，再按 `SortDirection` 取反。取反从"只在 `Version` 解析成功分支"提到最外层，于是**回退的字符串比较也跟随方向**。
- 新增 `CompareVersions(RuntimeLibrary?, RuntimeLibrary?)`：`x`/`y` 或它们的 `Version` 为 null 时直接按字符串比较（null 当最小值）；否则 `Version.TryParse` 判断能否按版本比较，失败才退回 `string.Compare(Ordinal)`。用 `TryParse` 取代原来的 `try { new Version(...) } catch (Exception)`，不再用异常做分支。
- 使用点：`AboutDialog.xaml` 的 `SortComparer PropertyName="Version"`（"依赖"页的运行时库表格）。

**二次核对（2026-11-14）——更正"方向不生效"这条结论**：反编译 `Syncfusion.Data.WPF`（`ilspycmd`）后确认，排序走 `CollectionViewAdv.SetActiveComparer` → `Syncfusion.Data.SortFieldComparer`，而它对**包括自定义比较器在内**的所有比较器，在 `SortDescription.Direction == Descending` 时统一执行 `num = -num`；并且**从不**给 `SortComparer` 设置 `ISortDirection.SortDirection`（那三处赋值只出现在分组用的 `GetGroupComparer`）。用反射直接驱动真实的 `SortFieldComparer` 实测：修复前后网格的升序/降序结果**完全相同**（-1/1、-16/16），`comparer.SortDirection` 始终是 `Ascending`。结论：旧代码的"回退分支不取反"在这条集成路径上**本来就不显现**，方向是靠外部取反碰巧对的；本项真正可能踩到的只有 **null 入参 NRE**（而 `AboutDialog` 的列表来自 `DependencyContext`，不含 null 行，所以仍是潜伏问题）。修复的价值是让比较器自身自洽、不再可能抛异常，**不改变对话框可见的排序结果**。

**验证**（源码链入探针：真实 `RuntimeLibraryVersionComparer.cs` + 真实 `Microsoft.Extensions.DependencyModel.RuntimeLibrary`（反射按参数名构造）+ 真实 `Syncfusion.Data.ISortDirection` / `SortFieldComparer`，同一份探针在修复前后各跑一次）：
- 直接调用比较器，修复前：

  | 输入 | ascending | descending |
  | --- | --- | --- |
  | 1.9.0 vs 1.10.0 | -1 | 1（正常） |
  | 1.0.0-beta vs 1.0.0-rc（不可解析） | -16 | **-16**（自身方向没生效） |
  | null vs 1.9.0 / 1.9.0 vs null / null vs null | **NRE** | **NRE** |

- 直接调用比较器，修复后：可解析版本仍是 -1/1；不可解析变成 **-16 / +16**；null 不再抛（`null vs 1.9.0` = -1、`1.9.0 vs null` = 1、`null vs null` = 0），降序同步取反。
- 真实数据（`BG3PakViewer.deps.json` 里 `type=package` 的 55 个运行时库，即"关于"对话框的数据源）：版本全部可被 `System.Version` 解析（0 个走回退分支）；升序相邻对 0 处逆序；`sign(desc) == -sign(asc)` 在全部 3025 对上成立；升序结果与直接按 `System.Version` 排序完全一致（无回归）。
- 集成（反射驱动真实 `Syncfusion.Data.SortFieldComparer`，`SortComparer PropertyName="Version"`）：升序/降序结果与修复前逐项相同，`comparer.SortDirection` 始终为 `Ascending`——证明本项不改变网格行为。
- `BG3PakViewer` / `BG3PakViewer.Dialogs` Debug x64 构建 0 错误，1 个既有警告（`MSB3277`）。
- **未覆盖**：没有在"关于"对话框里真的点两次列头（排序路径已由 `SortFieldComparer` 实测替代）；真实数据里没有不可解析版本，回退分支只在合成数据上验过。

**42.（判定：不成立，关闭，2026-11-14）同一路径既是文件又是目录前缀**
`Shared/Models/PackageEntry.cs:71-95`。
**核查**：PAK 条目名不是磁盘路径，LSPK 本身并不禁止 `foo` 与 `foo/bar` 并存，所以这条代码分支确实存在。合成输入实测：`["foo", "foo/bar"]` 时 `foo` 节点 `IsFolder=False` 却带有子节点；`["foo/bar", "foo"]` 时 `foo` 先被建成目录、文件节点不创建（文件"消失"）。正常的 `["a.txt", "a/b.txt"]` 不受影响。
**为什么判不成立**：能产生这种条目组合的只有手工构造的 pak——LSLib 是按源目录打包的，而磁盘上不允许文件与目录同名。对全部 53 个游戏 pak（`Data` + `Data\Localization`，共 **1,041,945** 个条目；18 个 `_N.pak` 分卷部件按格式跳过）逐一检查"某个条目名等于另一个条目的祖先目录"：按序数 **0** 个，忽略大小写也 **0** 个。
**处理**：不作为缺陷，不改代码；将来若要防手工构造的 pak，再和二进制输入校验一起另开项。

**43.（已修复并验证，2026-11-14）Recent 对话框发送请求消息但从不 Reply**
`Dialogs/ViewModels/RecentDialogViewModel.cs:57-60` 发送 `AsyncRequestMessage` 而 `MainWindowViewModel.Package.cs:177-190` 从不 `Reply`。
**事实更正**：MVVM Toolkit 8.4.2 的 `Send` 返回的是消息对象而不是 `Task`，`AsyncRequestMessage<T>.Response` 在未 `Reply` 时是**抛** `InvalidOperationException`（"No response was received for the given request message."），并不会挂起；而消息对象被 `_ =` 丢弃，所以连这个异常都看不到。真正的缺陷是请求/响应契约从未被履行，而这个响应本来也没有任何消费者。
**修复（2026-11-14）**：
- 改用单向消息 `ValueChangedMessage<string>`（与 `SearchQueryChanged`、`ZoomExtents` 的写法一致），`RecentDialogViewModel` 直接 `Send`，`MainWindowViewModel` 用 `async void` 处理器直接执行打开流程。
- 删除只在这里用过、且已无人引用的 `BG3PakViewer.Messaging/AsyncRequestMessage.cs`。
- **保留"对话框先关闭、再发消息"的顺序**：打开流程会弹"正在导出 / 重新打开 / 打开失败"等以主窗口为 owner 的模态框，若 RecentDialog 还开着就会形成嵌套模态，所以响应无法反哺对话框，单向消息才是与真实交互相符的抽象。
**验证**：源码探针（真实 `MainWindowViewModel` + 12 个 `DispatchProxy` 桩服务，加载真实构建产物）发送 `ValueChangedMessage<string>` 后捕获到 `Opening PAK file: "C:\nonexistent\probe.pak"` 日志，证明消息类型、令牌与处理器注册确实匹配；全解决方案 Debug x64 构建 0 错误。
**未覆盖**：没有在真实 UI 里点 Recent 列表做端到端自动化；探针直接驱动真实 VM 与真实消息总线。

**44.（随 #6 一并修复）** 每次文件夹导出 `new CancellationTokenSource()` 不 Dispose，取消时取消的是"最后一个"。现在改为局部 CTS + 结束时 `Dispose()` 并置 null（`MainWindowViewModel.Export.cs` 的 `ExportFolderAsync`），字段不再残留旧源。

**45.** 扩展名→导出策略是 first-wins，结果依赖 DI 注册顺序，重复扩展名时静默择一（`Services/ExportService.cs:73-85`）。

**46.（已修复并验证）** 单例 `PackageReader` + `Task.Run`，并发加载不安全（`Loader/PackageLoader.cs:11,22`；当前 UI 串行调用，风险潜伏）。

**机制（反编译 `LSLib.LS.PackageReader` 确认）**：`Read(path, metadataOnly)` 先 `MetadataOnly = metadataOnly`，再调 `ReadInternal(path)`；`ReadInternal` 做的第一件事是 `Pak = new Package(path)`，之后整条解析路径都通过这个**实例字段**进行。`Read` 的 catch 里是 `if (Pak != null) Pak.Dispose();` 再抛出。于是同一个 reader 上的后一次失败读取，释放掉的是**上一次成功读取返回的那个包**（`Pak` 仍指向它——异常发生在 `new Package(path)` 之前，字段没有被覆盖）；并发时则是释放另一个线程**正在解析**的包。`MetadataOnly` 同为一个会被并发改写的实例字段（本工具始终传 false，所以这一半只是理论风险）。

**修复记录（2026-11-14，`PackageLoader.cs` / `PackageService.cs` / `App.xaml.cs`）**：
- `PackageLoader` 不再持有 `PackageReader` 字段：每次 `LoadAsync` 都在 `Task.Run` 里 `new PackageReader()`，“一次读取一个 reader”，共享状态从根上消失（失败读取只会释放它自己刚建的那个包）。
- 去掉字段后类型已无任何实例状态，`PackageLoader` 改成 `static class` + 静态 `LoadAsync`：Release 的 `TreatWarningsAsErrors` 会让 SonarAnalyzer **S2325**（“可改为 static”）直接失败，而这里确实没有保留实例的理由。随之删掉 `App.xaml.cs` 的 `.AddSingleton<PackageLoader>()`、`PackageService` 的构造参数（改为 `PackageLoader.LoadAsync(path)`），以及 `App.xaml.cs` 里因此不再使用的 `using BG3PakViewer.Loader;`。
- **未改**：`Task.Run` 保留（LSLib 的 `Read` 是同步阻塞的，仍要离开 UI 线程）；`PackageService.CleanupAsync` 的“先释放旧包再加载”顺序、UI 层 `_isLoading` 的串行化都没动；`LoadAsync` 的签名与“失败返回 null 并记 Error”的语义不变。

**验证**（探针用真实 `LSLib.dll`；包用真实 `Data\GamePlatform.pak`（81 KiB）、`Gustav.pak`、`Shared.pak`，坏路径用不存在的文件；“可读”= 取包内第一条非删除条目 `CreateContentReader()` 并读 64 字节）：

1. LSLib 层（直接驱动 `PackageReader`）：

   | 场景 | 结果 |
   | --- | --- |
   | 共享 reader：先读成功，再读坏路径 | 成功返回的包随后 **`ObjectDisposedException`**；`Pak` 字段在失败前后都指向它 |
   | 每次新 reader：同样顺序 | 成功返回的包仍可读（`OK(64B)`） |
   | 共享 reader：并发（Gustav.pak，坏路径读取在解析途中发出） | 已观察到在途包（`sawInFlightPackage=True`），成功读取返回的包 **`usable=ObjectDisposedException`**；Shared.pak 相同，Engine.pak 那一轮解析已完成、包仍可用——说明这是竞态，不是必现 |
   | 每次新 reader：并发 200 轮 | **200 可用 / 0 损坏** |

2. `PackageLoader` 层（反射加载修复前/后的 `BG3PakViewer.Loader.dll`，同一份探针；修复前的 DLL 是改动前 `bin\x64\Debug` 的那份，其 `_packageReader` 字段可证明）：

   | 观察 | 修复前（HEAD 构建） | 修复后 |
   | --- | --- | --- |
   | 实例字段 | `PackageReader _packageReader` | 无（static 类，无实例字段） |
   | “加载 A → 加载坏路径”连续两轮 | 每轮 A 的包都在坏路径读取后变成 `ObjectDisposedException` | 每轮 A 的包随后仍可读（`OK(64B)`） |
   | “加载 A”与“加载坏路径”并发 100 轮 | **37 可用 / 63 损坏**（另一次 36/64） | **100 可用 / 0 损坏** |

- 集成（真实 `BG3PakViewer.Services.PackageService`，从 Debug x64 输出反射构造、无构造参数）：`LoadPackageAsync(GamePlatform.pak)` → `True`、`IsLoaded=True`、`BuildTree()` 得到 1 个根节点、`Dispose()` 后 `IsLoaded=False`——删掉 DI 构造参数后这条链路照常工作。
- 构建：`dotnet build BG3PakViewer.slnx -m:1 -c Release -p:Platform=x64` **0 警告 / 0 错误**（`TreatWarningsAsErrors` 生效，S2325 已消失）；Debug 同参 **0 警告 / 0 错误**（一次因 `LogDialog.g.cs` 被占用报 `MC1000`，重跑即过，与本次改动无关）。
- **未覆盖**：没有在真实界面里并发触发两次打开——UI 层 `_isLoading` 仍是串行化手段，本项只把 loader 变成并发安全，没有改 UI 的并发策略；`PackageService.CurrentPackage` 的“后加载覆盖先加载、旧包泄漏”仍未加锁，是另一条（本次未动）；并发样本不是 100% 命中，修复前 100 轮里 63 次命中已足以说明问题，但命中率随包大小/机器负载变化。

**47.（已修复并验证，2026-11-14）图标图集预览订阅 cell 的 `PropertyChanged` 后不退订**
`Controls/ViewModels/IconAtlasPreviewViewModel.cs:51-52` 订阅 cell 的 `PropertyChanged`，类却不实现 `IDisposable`，被替换后订阅仍然挂着（当前生命周期一致，暂不构成泄漏）。
**修复**：类改为继承 `DisposableViewModel`，在 `Dispose(bool)` 里遍历 `Items` 退订。`MainWindowViewModel.Preview.DisposePreviewAsync` 本来就会对 `IDisposable` 调用 `Dispose`，所以切换预览时就会走到这里。**没有清空 `Items`**：释放发生在 `PreviewVm = null` 之前，清空集合会在拆卸期间对仍绑定的 UI 发 `CollectionChanged`。
**验证**：探针（真实 `IconAtlasPreviewViewModel` / `IconAtlasCellViewModel`，5 参数构造，三个服务用 `DispatchProxy` 桩）：
- 选中一个 cell → `ExportSelectedCommand.CanExecuteChanged` 触发 **1** 次；
- `Dispose()` 之后再改 `IsSelected` → **不再触发**（计数仍为 1）；
- 第二次 `Dispose()` 无异常，`vm is IDisposable == True`。
- 全解决方案 Debug x64 构建 0 错误。
**未覆盖**：没有在真实 UI 里切换图集预览做端到端验证；探针直接调用真实 VM 的订阅/退订路径。

**48.（已修复）仓库卫生**
`BG3PakViewer.Dialogs/BG3PakViewer.Dialogs_owkbpdbx_wpftmp.csproj` 是 WPF 构建留在项目目录里的临时工程（`.gitignore:92` 的 `*_wpftmp.csproj` 让它从没进过 git，所以一直躺在工作区）；`BG3PakViewer.csproj` 里的 `<None Remove="..\..\..\..\.nuget\packages\naudio.vorbis\…\README.md"/>` 指向一个早已不引用的包；`.idea/` 有 5 个文件被 git 跟踪。
**修复记录（2026-11-14）**：
- 删除 `BG3PakViewer.Dialogs_owkbpdbx_wpftmp.csproj`（`Test-Path` 已为 False）。
- 删掉 `BG3PakViewer.csproj` 里那条 `naudio.vorbis` 的 `<None Remove>`（`None Remove` 的是不存在的东西，纯残留）。
- `git rm -r --cached .idea`（磁盘上的 Rider 配置保留）并在 `.gitignore` 末尾加 `.idea/`；`git check-ignore -v` 现在指向 `.gitignore:366:.idea/`，`git ls-files .idea` 为空。

**49.（已修复，CPM 除外）构建配置重复/漂移**
**修复记录（2026-11-14）**：
- `global.json`：`"version": "10.0.0"` + `rollForward: latestMajor` → **`11.0.100` + `latestFeature`**。本机只有 SDK 11.0.100，而 `10.0.0` 不是任何已安装 SDK 的版本号（之前靠 `latestMajor` 兜到 11）；改成实际版本后版本号才有意义，`latestFeature` 允许 11.0.x 的补丁带。
- 13 个 csproj 删除 `<SupportedOSPlatformVersion>7.0</SupportedOSPlatformVersion>`：TFM `net10.0-windows10.0.18362.0` 本身就声明 `10.0.18362.0`，写 7.0 是自相矛盾的覆盖；删掉后由 TFM 决定（等于把求值结果从 7.0 改成 10.0.18362.0）。
- 13 个 csproj 删除 `Debug|x64` / `Release|x64` 两个只含 `<DebugType>embedded</DebugType>` 的 PropertyGroup：`Directory.Build.props` 已经无条件设了同一个值。
- `Directory.Build.props` 里的 `Nerdbank.GitVersioning` 3.9.50 → **3.10.94**，并删除 13 个 csproj 里 `<PackageReference Update="Nerdbank.GitVersioning"><Version>3.10.94</Version></PackageReference>`（这 13 条 Update 的唯一作用就是把 props 的 3.9.50 覆盖成 3.10.94，等于把版本写在 14 个地方）。
- **没做 CPM（`Directory.Packages.props`）**：除 `SonarAnalyzer.CSharp` 外没有跨工程重复的包版本，CPM 只把这一个版本集中起来，代价是每个 `PackageReference` 都要拆 `Version`/`PrivateAssets`、覆盖全部 13 个工程；等确实要集中管理时再单独做，不混在这条里。
**验证**：`Debug` 与 `Release` 各做一次 `-t:Rebuild`，均 **0 错误**；`SupportedOSPlatformVersion` 与 `DebugType` 的求值结果与改动前一致（`DebugType=embedded` 来自 props、TFM 版本 10.0.18362.0）。

**50.（部分修复：门禁 + `.editorconfig`；测试工程与 CI 未做）**
**修复记录（2026-11-14）**：
- 新增根 `.editorconfig`：只统一编码与空白（`charset=utf-8`、最终换行、去行尾空格、cs/csproj/xaml 4 空格、json 2 空格），**不设任何 `dotnet_diagnostic.*.severity`**，所以不会改变构建结果，也不会触发全库重排。
- 门禁：`Directory.Build.props` 增加 `<TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>`——Release 是发布用的配置，Debug 仍然只报错不拦警告。
- 为让门禁是绿的，清掉两个既有告警：① `BG3PakViewer.Icons/IconAtlasSlicer.FindContentBoundingBox` 的嵌套 `for` 缺花括号（**S3973**）→ 补花括号，行为不变；② `BG3PakViewer.NexusIntegration` 的 **CS0108** 来自 ZeroQL 生成代码 → 该项目加 `<NoWarn>$(NoWarn);CS0108</NoWarn>`。
**验证**：`dotnet msbuild -getProperty:TreatWarningsAsErrors` 在 Release 返回 `true`、Debug 返回 `false`；`dotnet build -c Release -t:Rebuild` 与 `-c Debug -t:Rebuild` 都是 **0 错误**，剩下的告警只有既有的 `MSB3277`（MSBuild 级，不受 `TreatWarningsAsErrors` 影响，见 #4）。
**没做**：测试工程与 CI。CI 在 #2（LSLib 绝对路径 `HintPath`）与 #3（`Syncfusion.lic` 不入库）没解决前必然红，这两条是你已接受的环境前提；测试工程等于新开一个工程（框架、覆盖范围、是否进 `slnx` 都要定），不属于"卫生"这一档，等要动时单独提。

## 追加（来自真实日志核查）

**51.（新增，P2，已修复并验证）预览切换时 I18N 绑定以 null key 求值，抛 `ArgumentNullException`**
`BG3PakViewer.Controls/DataTemplates/NotSupportedPreviewTemplate.xaml:6`（`Text="{I18N {Binding HelpTextKey}}"`）+ `BG3PakViewer.Controls/ViewModels/NotSupportedPreviewViewModel.cs:19`（`string? HelpTextKey`，默认 null）。
真实日志（Debug 配置）里已出现 **73 次**：`log20261112_001.txt` 36 次、`log20261112.txt` 33 次、`log20261113.txt` 3 次、`log20261114.txt` 1 次。堆栈：

```
System.ArgumentNullException: Value cannot be null. (Parameter 'key')
   at System.Collections.Generic.Dictionary`2.TryGetValue(TKey key, TValue& value)
   at System.Windows.I18NExtension.LanguageDictionary.TryGetValue(String key, String& value)
   at System.Windows.I18NExtension.MultiValueLangConverter.GetValues(...)
   at System.Windows.I18NExtension.MultiValueLangConverter.Convert(...)
   at System.Windows.Data.MultiBindingExpression.TransferValue()
   ...
   at System.Windows.TreeWalkHelper.OnAncestorChanged(...)
```

机制：`MainWindowViewModel.Preview.cs:57-61` 每次切换预览都先 `PreviewVm = null`；模板拆除过程中 DataContext 短暂为 null，`{Binding HelpTextKey}` 求值为 null，Antelcat 的转换器把 null 当 key 查字典（`Dictionary.TryGetValue(null)` 抛异常）。
后果：被 #11 吞掉，但异常发生在 `InvalidateTreeDependentProperties` 的遍历里，会中断这一轮属性失效传播——可能出现"该更新的文本没更新"。每次切换预览都可能触发一条。
修法：`HelpTextKey` 默认值改为 `string.Empty`（`TryGetValue("")` 不抛），或绑定加 `TargetNullValue=''`。两行。

**修复记录（2026-11-14，`NotSupportedPreviewTemplate.xaml`）**：
- 绑定改为 `{I18N {Binding HelpTextKey, FallbackValue=''}}`，并加注释说明原因。报告里那两种修法实测都不成立：`HelpTextKey` 默认 `string.Empty` 无效——异常发生时 DataContext 已经是 null，属性根本没被读取；`TargetNullValue=''` 也无效——这是"绑定不可用"而不是"源值为 null"，实测仍然抛。只有 `FallbackValue` 会被 MultiBinding 的子绑定采用，把不可用的 key 变成空串。
- 验证 ①（源码级 A/B，真实 `Antelcat.I18N.WPF` 转换器 + 真实 `NotSupportedPreviewViewModel` + 真实模板，ContentControl 挂载后 `Content = null` 模拟清空预览）：旧模板每次抛 `ArgumentNullException(key)`（同步 + Dispatcher 各一条），新模板 20 轮 0 异常。
- 验证 ②（编译产物）：全解决方案 Debug x64 构建后，从 `BG3PakViewer.Controls.dll` 的 BAML 载入模板，反射读到 key 绑定 `FallbackValue=""`，同样 20 轮 0 异常；说明改动确实进了 BAML，不是只改了源码。
- 验证 ③：`dotnet build BG3PakViewer.slnx -m:1 -c Debug -p:Platform=x64` 0 错误，余 1 条既有 `MSB3277`（#4）。
- **未覆盖**：没有启动真实 GUI 复跑"预览不支持格式 → 清空预览"的端到端动作；结论来自真实模板、真实转换器与真实 VM 的探针。
- 附带发现（未处理）：`Dialogs/Views/VirtualTextureToolboxDialog.xaml:280` 也有 `{I18N {Binding PreviewStatusKey}}`，属同类写法；关闭工具箱窗口是否会触发同一异常未验证，不在本项范围内。

**52. 虚拟纹理工具箱把"图层无数据"报成导出失败（已修复）**
真实日志里 16 条 `Failed to export file to: …`，异常都是 `System.IO.InvalidDataException: 此图层没有数据`（旧版本栈指向 `VirtualTexturePreviewViewModel.cs:373`，当前等价物是 `VirtualTextureToolboxDialogViewModel.OpenLayerAsync` 里对 null 流抛出的异常）。原因是导出前无条件为每张纹理的每一层建文件，空层到真正抽取时才失败。
**修复记录（2026-11-14）**：按"部分空则跳过、全空则禁用按钮"的语义实现。
- `VirtualTextureLayerExtractor`：把"逐层找第一个有效瓦片区域"抽成 `FindLevel(...)`，新增 `HasLayerData(index, layer)`（元数据判断，不写流）；`ExtractTexture` 改用同一 `FindLevel`，保证"检查说有数据"与"抽取不会返回 null"是同一个判据。
- `IVirtualTextureExtractor` / `VirtualTextureExtractor`：新增 `HasLayerDataAsync(entry, layer, ct)`，与抽取共用同一把 `_gate`。
- `VirtualTextureToolboxDialogViewModel`：选择变化时异步判定"选中纹理是否有任一层有数据"（`_selectionHasData`，先置 false 再通知，旧判定用 CTS 取消），`CanExportSelected()` 增加该条件 ⇒ 全空时按钮灰。该判定后来拆成 `CheckSelectionAsync`（生命周期编排）/ `BeginSelectionCheckAsync` / `TryAnySelectedLayerHasDataAsync`（取消与失败策略）/ `AnySelectedLayerHasDataAsync`（逐层）/ `AnyTextureHasLayerDataAsync`（单层内逐纹理）/ `EndSelectionCheck`，方法体分别为 10/10/15/6/6/4 行。
- `VirtualTextureToolboxDialogViewModel.Export`：`BuildFiles` → `BuildFilesAsync`，逐层 `HasLayerDataAsync` 过滤空层（只记日志、不计失败）；`files.Count == 0` 时提示"没有可导出的文件"而不是报"已导出 0 个文件"。
验证：两个改动工程编译 0 错误；用真实 `VirtualTextures.pak` 写探针（自建 `IPackageService` 包住 LSLib `Package`，索引到 15595 张纹理），抽查 36 个"纹理×层"组合：**33 个有数据、3 个为空、`HasLayerDataAsync` 与 `ExtractLayerAsync` 的判定 0 处不一致**。判定逻辑拆分后又用 VM 探针（假 catalog / extractor / 对话框服务）回归：无数据选择 → `CanExport=False`；只有第 1 层有数据 → `CanExport=True`；慢判定被新的空选择替换 → 最终仍 `CanExport=False`（旧判定取消后不回写）；快速有数据选择覆盖旧空选择 → `CanExport=True`。
**最终规则（作者确认）**：默认导出三层；某一层没有瓦片数据就跳过该层；三层全空则命令 `CanExecute=false`（按钮灰）。"结构上有瓦片、像素全透明"的层不算空，照常导出（见 #26）。
**未覆盖**：按钮灰/亮的 UI 状态没有做端到端自动化，靠命令条件 + 上述 API 一致性支撑。

**53.（新增，P3）图标图集预览里图标之间有空隙（已修复，面板级验证）**
`BG3PakViewer.Controls/DataTemplates/IconAtlasPreviewTemplate.xaml` 的 `ListBox.ItemsPanel` 用 iNKORE 的 `ikw:VirtualizingWrapPanel`，而它**默认 `SpacingMode="Uniform"`**：把每行放不下的那截宽度平均摊到图标之间**以及行首行尾**。模板没有改这个值，于是预览里的图标不再是图集里那种紧邻关系，而是被凭空撑开——单元格本身是 150×150（CCIcon 图集）且内容铺满整格（见 #25 的抽样），所以看到的空隙全部来自布局，不来自图集。
实测（面板级探针，真实 `iNKORE.UI.WPF.Controls.VirtualizingWrapPanel`，40 个 150×150 项，宽度取预览面板在本机 2560×1600 @150% 下的约 1343 DIP）：
- `SpacingMode=Uniform`：每行 8 个，**行首缩进 15.89 DIP**，图标之间 **15.89 DIP**（= (1343 − 8×150)/9，与 Uniform 的公式一致）。
- `SpacingMode=None`：x = 0/150/300/450…，**间隙 0**，与图集里的紧邻关系一致。
**修复记录（2026-11-14）**：`<ikw:VirtualizingWrapPanel Orientation="Horizontal" SpacingMode="None" />`（并在注释里写明为什么必须显式设成 None）；`BG3PakViewer.Controls` Debug x64 构建 0 错误（只剩既有 `MSB3277`）。`BG3PakViewer.Icons` 只改了注释，同时加了"黑格是补方填充"的 `<remarks>`（见 #25）。
**未覆盖**：改动生效需要重启应用——验证时应用正在运行（PID 锁定 `bin\x64\Debug` 的 DLL），全量 `dotnet build BG3PakViewer` 因此被 `MSB3027` 挡住，没有在真实窗口里复看一次像素级间距；结论来自面板探针的实测坐标 + 属性语义。

**54.（新增，P2，已修复并验证）`GetTextureTileSpan` 用截断，宽度/高度不是整格数时丢掉最后一列/行**
`VirtualTextureTileRangeCalculator.GetTextureTileSpan`（`:62-66`）用 `tex.Width / tileSet.EffectiveTileWidth` 截断；LSLib 的 `ExtractTexture(level, layer, tex)` 里也是截断（`num5 = tex.Width / num`，见 #24 的反编译）。对 `tex.Width` 不是 128 整数倍的纹理，这会漏掉最后一列/行。
**证据（2026-11-14，`VirtualTextures.pak` 全量 + 像素级）**：
1. 几何：16 个 tile set 都是 `TileWidth=144 / TileBorder=8` ⇒ 有效格 128；全部 **15595** 张纹理的 X/Y **都是 128 的整数倍**（非整数倍 0 张），所以 origin 截断没有损失，问题只在 span。
2. 范围：**581** 张纹理的宽或高不是整格数（不含"小于一格"的子瓦片纹理）。被丢掉的那一列在 **567** 张纹理上合计 **35096 px**（每张最多丢 124 px），那一行在 **557** 张上合计 **35104 px**。
3. 归属：按"每张纹理声明自己的截断矩形"重建整张一级网格（512×256 格），被丢掉的那些格**没有任何别的纹理声明**（列 0/554、行 0/540），两张纹理的矩形也从不重叠（0 格）——不是邻居的地盘。
4. 存不存在：被丢掉的那一列在 level 0 **每一格都真的有瓦片**（列 **6467/6467**、行 **6319/6319**），不是空洞。
5. 像素级（`Albedo_Normal_Physical_0.gts` 里 `0072e3eb…`，1076×1068，tile 原点 (0,112)）：按截断取 8×8 格得 1024×1024，按向上取整取 9×9 格得 1152×1152。相邻列平均差在 **x=1024 处没有跳变**（23–28，与内部一致），到 **x=1076**（= 纹理声明宽度）直接掉到 **0.0** 并保持——即存储里就是这张纹理的完整 1076 px，1076 之后才是补白的常量区；竖直方向同样在 **y=1068** 归零。截断正好把 1076−1024 = 52 px 宽、1068−1024 = 44 px 高的真实内容切掉了。
**结论：截断确实丢内容**，LSLib 有同样的问题，不是"与上游一致所以正确"。
**修复记录（2026-11-14）**：`GetTextureTileSpan` 的宽高改为向上取整（重新引入 `DivideCeiling` 并加注释说明"存储里有最后一格的像素"），origin 仍是截断（纹理起点都在格边界上，且截断在非负坐标下即 floor）；`ToLevelRange` 不动。
- 影响面（按上面的数据核对）：581 张"非整格数"纹理在各自边缘多取一格；子瓦片纹理的 span 从 0 变 1（"它占一格"）；尺寸是整格数的约 15000 张完全不受影响。
- 代价：这 581 张的导出尺寸各多一格（含补白边），与 LSLib 的导出尺寸不再一致——LSLib 那条路径本来就在丢内容。
**验证**：
- `BG3PakViewer.VirtualTextures` / `BG3PakViewer` Debug x64 构建 0 错误（只剩既有 `MSB3277`）。
- 端到端（真实 `VirtualTextures.pak`，走 `VirtualTextureLayerExtractor.ExtractAsync`，DDS 字节数 − 128 字节头 = 像素数）：
  - tex#20（1076×1068）：1024×1024 → **1152×1152**（9×9 格，正好覆盖 1076×1068）；
  - tex#224（800×1044）：768×1024 → **896×1152**（6×8 格 → 7×9 格）；
  - tex#263（3108×3848）：3072×3840 → **3200×3968**（24×30 → 25×31）；
  - 子瓦片 tex#4（宽高都 < 一格）：仍是 1×1 格（128×128），没有因为 span 变成 1 而多取；
  - 整格数的 tex#0（2048×2048）：字节数与改动前**完全一致**（4194432）。
- 全量普查（16 个 tile set、15595 张纹理、46785 个"纹理×层"，同一探针在改前改后各跑一次）：
  - 解析到 level 0 的从 33195 变成 **36195**（+3000），level>0 从 13590 变成 **10590**；
  - 多出来的 3000 个正是"尺寸小于一格"的组合：改前它们的范围在 level 0 是空的（`max = min−1`），只能掉到 L1–L8 取一张粗格；现在 span=1，在 **level 0 就拿到全分辨率的那一格**。
  - 正常尺寸纹理的路径不变：10590 个 level>0 组合的 origin 仍然全部对齐 2^L，没有任何范围变化。
**未覆盖**：只对 1 张纹理做了像素级取证（其余 580 张走同一条代码路径 + 存在性普查）；没有在 UI 里实跑"打开纹理 → 导出"看新尺寸；更粗 mip 上被多取的那一格会混入邻居内容（网格粒度决定的，任何取整方式都避免不了）。

## 建议修复顺序

1. **止血**：#9 `File.Create`、#51 `HelpTextKey` 默认值（都只有两三行；#5、#6/#44、#10 已修并验证）。
2. **让失败可见**：#8 导出返回失败数（约一天；#11 启动段、#12 Behavior 注销、#13 日志线程安全 + 上限已修）。
3. **输入边界**：#24 虚拟纹理瓦片范围取整、#23 里 holder 的非 seek 流复制上限（#14 页文件校验、#15 路径校验已修，约一到两天）。
4. **构建卫生**：#4 版本统一、#49 配置收敛；#2/#3 属已接受的环境前提，仅在需要 CI/换机时再处理（#2 收成 MSBuild 属性、#3 条件化 Embed + 判空）。
5. **体验类**：#16、#17、#19 用户可感知度最高。
6. **不做**：#1 归上游 LSLib（`ReadOnlySubstream` / `UnpackSolidSegment` 的共享流），本仓库不改；应用侧已有事实上的保护——solid 的只有 LowTex.pak，且导出它的人极少。

## 做得好的地方（不必改动）

- 无 `.Result`/`.Wait()`；仅 2 处 `async void`，都做了 try/catch。
- 预览 VM 统一 `DisposableViewModel`，切换时先释放旧 VM；`ImagePreviewViewModel` 用 `OnPreviewChanging` 精确释放旧图。
- `BitmapSource` 在线程池创建后 `Freeze()`，跨线程安全。
- 虚拟纹理只缓存最近一个 tile set、uniform tile 复用单张图、按 band 复用 strip 缓冲。
- 二进制解析全部走 `BinUtils`，未自行拼字节序；关键算法都写了"为什么"的注释。

## 待确认（缺证据）

- #7 LZ4 解压流是否 `AcquirePointer`（决定是异常还是崩溃）。
- #26 `BC5Image` 平面布局（决定 uniform/BC 缓冲是否真不一致）。
- #29 是否存在单文件发布配置。
- 本次未做运行时验证：未启动 GUI、未做导出字节级比对。
