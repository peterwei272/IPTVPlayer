此目录用于存放本地构建和安装所需的 Windows x64 libmpv 运行库。

源码仓库默认不提交 mpv-2.dll。

准备本地运行库:
- 运行: powershell -ExecutionPolicy Bypass -File .\scripts\ensure-mpv.ps1

当前锁定来源:
- Provider: shinchiro
- Package: mpv-dev-x86_64-20260301-git-05fac7f.7z
- Download page: https://sourceforge.net/projects/mpv-player-windows/files/libmpv/
- SHA256: DC991B2077F9B899FC022B92B3CAEDF1A70916C06560B6216F1AB09B5C808258

说明:
- 下载脚本会使用 Invoke-WebRequest 的 Wget User-Agent 从 SourceForge 获取压缩包。
- 上游包内原始文件名是 libmpv-2.dll。
- 为了与应用当前的 P/Invoke 入口保持一致，项目内本地使用 mpv-2.dll。
- 应用会优先从程序目录下的 mpv\mpv-2.dll 加载运行库。
