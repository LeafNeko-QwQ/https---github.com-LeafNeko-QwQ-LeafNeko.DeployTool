using LeafNeko.DeployTool.Models;

namespace LeafNeko.DeployTool.Services;

public class LicenseService
{
    private const string LicenseText = """
        LeafNeko 装机助手 — 软件许可协议

        版权所有 (c) 2025 LeafNeko

        本软件仅供个人学习和研究使用，禁止用于任何商业用途。

        使用条款：
        1. 本软件可以免费使用、复制和分发给他人。
        2. 本软件"按原样"提供，不提供任何明示或暗示的保证。
        3. 在任何情况下，作者不对因使用本软件而产生的任何损害承担责任。
        4. 用户通过本软件下载和安装的任何第三方软件，其版权归各自所有者所有。
        5. 本软件会向 C 盘根目录写入文件，需要管理员权限。
        6. 作者保留随时修改本协议的权利。

        继续使用即表示您已阅读并同意以上条款。

        LeafNeko
        B站: https://space.bilibili.com/1580757085
        Gitee: https://gitee.com/LeafNeko-QwQ/zip-deploy-manifest
        """;

    private DeployConfig _config = DeployConfig.Load();

    public bool IsAccepted() => _config.LicenseAccepted;

    public string GetLicenseText() => LicenseText;

    public void Accept()
    {
        _config.LicenseAccepted = true;
        _config.LastRunTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _config.Save();
    }
}
