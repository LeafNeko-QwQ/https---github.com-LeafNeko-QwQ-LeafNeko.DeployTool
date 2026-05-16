# 开发执行计划

## 开发原则

- **小步前进**：每个阶段交付可验证的成果
- **稳定优先**：每个阶段通过验证才进入下一阶段
- **安全第一**：文件操作、网络操作均需异常处理

## 阶段概览

| 阶段 | 名称 | 预计产出 | 依赖 |
|------|------|----------|------|
| P0 | 项目初始化 | 空白 WPF 项目可编译运行 | - |
| P1 | 数据模型 + 基础服务 | 模型类 + 配置/许可服务 | P0 |
| P2 | 仓库服务 + 清单解析 | 能下载并解析 app-list.txt | P1 |
| P3 | 许可协议窗口 | 首次启动显示许可页 | P1 |
| P4 | 主窗口框架 + 主题 | 三区布局窗口 + 粉色主题 | P1 |
| P5 | 软件卡片控件 | AppCard 完整 UI + 勾选 | P2+P4 |
| P6 | 分类标签 + 卡片网格 | 按分类筛选 + 卡片网格展示 | P5 |
| P7 | 功能卡片控件 | ActionCard + 解压部署 | P2+P4 |
| P8 | 下载服务 + 安装 | 下载安装包 + 运行安装程序 | P2 |
| P9 | 进度条控件 + 动画 | 多进度条 + 动画效果 | P4 |
| P10 | 全流程集成 | 所有模块联调 | P6+P7+P8+P9 |
| P11 | 打包发布 | 单文件 EXE 输出 | P10 |

## 详细步骤

### P0: 项目初始化

**目标**：创建可编译运行的空白 WPF 项目

**步骤**：
1. 检查 .NET 9 SDK 是否安装
2. 使用 `dotnet new wpf` 创建项目
3. 配置 `app.manifest`，启用 `requireAdministrator`
4. 配置 `.csproj`：目标框架 net9.0-windows，启用单文件发布
5. 编译验证 `dotnet build`
6. 在开发日志记录初始化完成

**验证**：项目可编译，无错误

### P1: 数据模型 + 基础服务

**目标**：建立数据模型和基础服务层

**步骤**：
1. 创建 `Models/AppItem.cs`
   - 属性：Name, Url, Category, IsSelected, Status
2. 创建 `Models/DeployConfig.cs`
   - 属性：LicenseAccepted, LastRunTime
   - 方法：Load(), Save()
3. 创建 `Services/LicenseService.cs`
   - 方法：IsAccepted(), Accept()
   - 存储路径：`%APPDATA%\LeafNeko-DeployTool\config.json`
4. 编译验证

**验证**：项目可编译，模型类完整

### P2: 仓库服务 + 清单解析

**目标**：实现从 Gitee 下载和解析清单

**步骤**：
1. 创建 `Services/RepoService.cs`
   - 基 URL 常量
   - `DownloadTextAsync(string fileName)` 方法
   - `DownloadFileAsync(string fileName, IProgress<double>)` 方法
   - HTTP 超时设置：30 秒
2. 创建 `Services/ManifestService.cs`
   - `Parse(string content)` 方法
   - 按行解析，忽略注释和空行
   - 返回 `List<AppItem>`
3. 编译验证

**验证**：用测试数据验证解析正确

### P3: 许可协议窗口

**目标**：实现首次启动许可协议

**步骤**：
1. 创建 `Views/LicenseWindow.xaml`
   - 显示许可协议文本
   - 「同意」和「不同意」按钮
   - 粉色主题样式
2. 创建 `Views/LicenseWindow.xaml.cs`
   - 同意：保存配置 + 打开主窗口
   - 不同意：退出程序
3. 修改 `App.xaml.cs` 启动逻辑
   - 检查许可状态 → 决定显示哪个窗口
4. 编译验证

**验证**：首次启动看到许可窗口，同意后不再显示

### P4: 主窗口框架 + 主题

**目标**：搭建三区布局主窗口和粉色主题

**步骤**：
1. 创建 `Themes/PinkTheme.xaml`
   - 定义所有颜色资源
   - 定义基础样式（Button, TextBlock, ProgressBar）
   - 定义动画关键帧
2. 创建 `Views/MainWindow.xaml`
   - 顶部品牌栏：LeafNeko 品牌名 + B站/Gitee 链接
   - 分类标签区域（先放空的占位）
   - 左侧滚动区域（先放空的占位）
   - 右侧功能卡片区（先放空的占位）
   - 底部操作栏（先放空的占位）
3. 创建 `ViewModels/MainViewModel.cs`
   - 基础属性初始化
4. 在 `App.xaml` 中加载主题
5. 编译运行验证

**验证**：主窗口显示，三区布局正确，粉色主题生效

### P5: 软件卡片控件

**目标**：实现 AppCard 自定义控件

**步骤**：
1. 创建 `Controls/AppCard.xaml`
   - 图标位、软件名、分类标签
   - 勾选复选框
   - 状态指示图标
   - 圆角白色卡片样式 + 阴影
2. 创建 `ViewModels/AppItemViewModel.cs`
   - 绑定 AppItem 数据
   - IsSelected 双向绑定
   - Status 状态绑定
3. 创建静态卡片测试数据，在主窗口中展示
4. 添加卡片 hover 动画（上浮 + 阴影加深）
5. 编译验证

**验证**：卡片显示正确，hover 有动画，可勾选

### P6: 分类标签 + 卡片网格

**目标**：实现分类筛选和卡片网格布局

**步骤**：
1. 实现分类标签数据绑定
   - 从 AppItem 列表中提取所有分类
   - 默认选中「全部」
2. 实现分类筛选逻辑
   - 点击标签 → 过滤卡片列表
   - 标签选中态切换动画
3. 实现 WrapPanel 卡片网格
   - 卡片自动换行排列
   - 均匀间距
4. 实现卡片入场动画
   - 从上往下依次滑入
   - 每张延迟 50ms
5. 编译验证

**验证**：分类切换正常，卡片网格排列正确，入场动画流畅

### P7: 功能卡片控件

**目标**：实现右侧功能卡片（便携部署 / 快捷方式部署）

**步骤**：
1. 创建 `Controls/ActionCard.xaml`
   - 图标 + 标题 + 描述
   - 执行按钮
   - 迷你进度条（执行后显示）
2. 创建 `Services/DeployService.cs`
   - `ExtractToAsync(Stream zip, string destDir, IProgress<double>)`
   - 内置解压进度回调
3. 在 MainViewModel 中添加两个 ActionCard 的数据绑定
   - 便携应用部署 → 下载 portable-apps.zip → 解压到 C:\
   - 快捷方式部署 → 下载 shortcuts.zip → 解压到桌面
4. 添加执行状态更新和完成/错误反馈
5. 编译验证

**验证**：点击执行 → 下载 → 解压 → 进度条 → 完成提示

### P8: 下载服务 + 安装

**目标**：实现下载和安装功能

**步骤**：
1. 创建 `Services/DownloadService.cs`
   - 下载队列管理
   - 单文件进度报告
   - 总体进度计算
   - 临时目录管理（创建/清理）
2. 在 `DeployService.cs` 中添加 `RunInstaller(string filePath)`
   - 检测文件类型（exe / msi）
   - 使用 `Process.Start()` 运行
3. 实现底栏「全选」和「一键安装已选」按钮逻辑
   - 全选：设置所有 AppItem 的 IsSelected = true
   - 一键安装：启动已选软件的下载队列
4. 实现安装完成后的缓存清理
   - 全部完成后删除临时目录
5. 编译验证

**验证**：勾选软件 → 一键安装 → 下载 → 自动运行安装程序 → 清缓存

### P9: 进度条控件 + 动画

**目标**：实现自定义动画进度条和全部动画效果

**步骤**：
1. 创建 `Controls/AnimatedProgressBar.xaml`
   - 渐变色填充（主色→强调色）
   - 流光 shimmer 动画
   - 进度百分比文字
2. 在 MainViewModel 中添加进度相关属性
   - OverallProgress（总体进度）
   - CurrentTask（当前任务描述）
   - IsProcessing（是否正在处理）
3. 实现多进度条布局
   - 右侧进度区：总进度条（大）
   - 右侧进度区：当前任务描述文字
   - 每个 AppCard：单文件进度条（迷你）
4. 补充实现全部动画（参照设计规范动画表）
   - 按钮涟漪、完成对勾、错误闪烁
   - 切换分类的交叉过渡
5. 编译验证

**验证**：所有动画流畅，进度条美观，无卡顿

### P10: 全流程集成

**目标**：串联所有模块，端到端可用

**步骤**：
1. 在 MainViewModel 中集成所有服务
   - 启动时：拉取清单 → 解析 → 展示
   - 用户操作：分类筛选、勾选、安装
2. 实现版本检测功能
   - 创建 `Services/VersionService.cs`
   - 注册表查询
   - UI 更新状态
3. 添加错误处理和用户提示
   - 网络错误 → 弹窗提示
   - 解压错误 → 卡片状态变红
   - 安装失败 → 标记并跳过
4. 端到端测试完整流程
5. 编译验证

**验证**：完整流程可走通，异常情况有提示

### P11: 打包发布

**目标**：生成可分发的单文件 EXE

**步骤**：
1. 确认 `.csproj` 发布配置正确
2. 添加应用图标 `Assets/icon.ico`
3. 执行发布命令：
   ```
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```
4. 在干净环境测试 EXE 是否能独立运行
5. 确认文件大小合理

**验证**：单文件 EXE 可独立运行，无需安装 .NET 运行时
