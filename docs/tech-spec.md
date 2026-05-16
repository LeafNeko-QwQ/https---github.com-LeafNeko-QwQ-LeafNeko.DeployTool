# 技术规格说明书

## 技术栈

| 项目 | 选择 | 版本 |
|------|------|------|
| 框架 | WPF | .NET 9 |
| 语言 | C# | 12 |
| 架构模式 | MVVM | - |
| 发布方式 | 单文件自包含 EXE | - |
| 最低系统 | Windows 10 (64-bit) | - |

## 项目结构

```
H:\develop\DNBZ\
├── LeafNeko.DeployTool.sln
└── LeafNeko.DeployTool\
    ├── App.xaml
    ├── App.xaml.cs
    ├── Models\
    │   ├── AppItem.cs
    │   └── DeployConfig.cs
    ├── Services\
    │   ├── LicenseService.cs
    │   ├── RepoService.cs
    │   ├── ManifestService.cs
    │   ├── DeployService.cs
    │   ├── VersionService.cs
    │   └── DownloadService.cs
    ├── ViewModels\
    │   ├── MainViewModel.cs
    │   └── AppItemViewModel.cs
    ├── Views\
    │   ├── MainWindow.xaml
    │   ├── MainWindow.xaml.cs
    │   ├── LicenseWindow.xaml
    │   └── LicenseWindow.xaml.cs
    ├── Controls\
    │   ├── AppCard.xaml
    │   ├── ActionCard.xaml
    │   └── AnimatedProgressBar.xaml
    ├── Themes\
    │   └── PinkTheme.xaml
    └── Assets\
        └── icon.ico
```

## 架构设计

### MVVM 分层

```
┌─────────────────────────────────────────┐
│  Views (XAML)                           │
│  负责界面渲染和动画                     │
├─────────────────────────────────────────┤
│  ViewModels                             │
│  负责UI状态管理、命令处理、数据绑定     │
├─────────────────────────────────────────┤
│  Models                                 │
│  纯数据对象，无业务逻辑                 │
├─────────────────────────────────────────┤
│  Services                               │
│  核心业务逻辑，与UI无关                 │
└─────────────────────────────────────────┘
```

### 数据流

```
用户操作 → View → 绑定 → ViewModel → Service → 结果 → ViewModel(状态更新) → 通知 → View(UI刷新)
```

## 模块详细设计

### LicenseService
```
职责：管理许可协议的同意状态
存储：%APPDATA%\LeafNeko-DeployTool\config.json
方法：
  - bool IsAccepted()       // 检查是否已同意
  - void Accept()           // 记录同意
  - string GetLicenseText() // 获取许可文本
```

### RepoService
```
职责：从 Gitee 仓库下载文件
基URL：https://gitee.com/LeafNeko-QwQ/zip-deploy-manifest/raw/master/
方法：
  - Task<string> DownloadTextAsync(string fileName)  // 下载文本文件
  - Task<Stream> DownloadFileAsync(string fileName, IProgress<double>)  // 下载二进制文件(带进度)
```

### ManifestService
```
职责：解析 app-list.txt
方法：
  - List<AppItem> Parse(string content)
格式：软件名称|下载直链|分类
规则：忽略空行和 # 开头的注释行
```

### DeployService
```
职责：解压ZIP、复制文件、运行安装程序
方法：
  - Task ExtractToAsync(Stream zip, string destDir, IProgress<double>)
  - void RunInstaller(string filePath)
  - void CleanTemp()
```

### VersionService
```
职责：检测软件是否已安装及版本
方法：
  - VersionInfo? Detect(string appName)
查询位置：
  - HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
  - HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
  - HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
```

### DownloadService
```
职责：管理下载队列，报告进度
属性：
  - ObservableCollection<DownloadTask> Tasks  // 下载任务列表
  - double OverallProgress                    // 总体进度
方法：
  - Task DownloadAllAsync(List<AppItem> items)
  - Task DownloadSingleAsync(AppItem item)
  - void CancelAll()
```

## 权限模型

- 程序清单（app.manifest）声明 `requireAdministrator`
- 启动时自动触发 UAC 弹窗（一次性）
- 程序内所有文件操作继承管理员权限

## 发布配置

```
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
```

输出：单个 `LeafNeko.DeployTool.exe` 文件，约 50-80 MB（含 .NET 运行时）

## 依赖列表

| 依赖 | 来源 | 用途 |
|------|------|------|
| System.IO.Compression | .NET 内置 | ZIP 解压 |
| System.Net.Http | .NET 内置 | HTTP 下载 |
| System.Text.Json | .NET 内置 | JSON 配置读写 |
| Microsoft.Win32.Registry | .NET 内置 | 注册表查询（版本检测） |
| System.Diagnostics.Process | .NET 内置 | 运行安装程序 |

**无第三方依赖。**
