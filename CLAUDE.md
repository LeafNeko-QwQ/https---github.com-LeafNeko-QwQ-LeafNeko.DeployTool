# CLAUDE.md — LeafNeko DeployTool 项目工作指引

## 项目简介

为 LeafNeko 开发一款 Windows 必装软件部署工具。技术栈：WPF (.NET 9) + C# + MVVM。

## 重要：每次开始工作前

1. 读取 `devlog/` 目录下最新日期的开发日志，了解当前进度
2. 读取 `docs/execution-plan.md`，确认当前处于哪个开发阶段
3. 工作完成后更新开发日志（新建或追加当日日志）

## 标准文件索引

| 文件 | 内容 | 用途 |
|------|------|------|
| `docs/requirements.md` | 需求规格说明书 | 了解功能需求和非功能需求 |
| `docs/tech-spec.md` | 技术规格说明书 | 了解架构、模块设计、API 接口 |
| `docs/design-spec.md` | UI/UX 设计规范 | 了解配色、布局、动画、组件规范 |
| `docs/execution-plan.md` | 开发执行计划 | 11 个阶段的详细步骤和验证标准 |
| `devlog/` | 开发日志目录 | 每天的工作记录，命名格式 `YYYY-MM-DD.md` |

## 项目路径

- 项目根目录：`H:\develop\DNBZ`
- 解决方案文件：`H:\develop\DNBZ\LeafNeko.DeployTool.sln`（尚未创建）
- 项目目录：`H:\develop\DNBZ\LeafNeko.DeployTool\`（尚未创建）

## 工作原则

1. **小步推进**：严格按 `execution-plan.md` 的阶段顺序开发，不跳步
2. **每阶段验证**：完成一个阶段后确认编译通过、功能正确，再进入下一阶段
3. **每日记录**：每天工作结束后在 `devlog/` 写入完成事项和待办事项
4. **参考规范**：编码时随时参照 `design-spec.md` 和 `tech-spec.md`
5. **安全优先**：文件操作、网络请求必须有异常处理

## 关键技术决策

- 管理员权限：通过 app.manifest 声明，启动时弹一次 UAC
- 桌面路径：使用 `Environment.SpecialFolder.Desktop`，不硬编码
- 缓存策略：安装全部完成后统一删除临时目录
- 无第三方依赖：仅使用 .NET 内置库

## 仓库信息

- Gitee 仓库：https://gitee.com/LeafNeko-QwQ/zip-deploy-manifest
- 仓库 owner：LeafNeko-QwQ
- 仓库名：zip-deploy-manifest
- 分支：master
- Raw 文件基 URL：`https://gitee.com/LeafNeko-QwQ/zip-deploy-manifest/raw/master/`
