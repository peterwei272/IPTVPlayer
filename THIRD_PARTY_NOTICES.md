# Third-Party Notices

本项目的源码仓库不直接提交 `mpv\mpv-2.dll`，但本地开发、绿色版运行和安装版打包会使用第三方 `libmpv` 运行库。

## mpv / libmpv

- Upstream project: [mpv](https://mpv.io/)
- Upstream source repository: [mpv-player/mpv](https://github.com/mpv-player/mpv)
- Windows package provider: `shinchiro`
- Locked package: `mpv-dev-x86_64-20260301-git-05fac7f.7z`
- Download page: [SourceForge libmpv builds](https://sourceforge.net/projects/mpv-player-windows/files/libmpv/)

本项目通过 [scripts/ensure-mpv.ps1](./scripts/ensure-mpv.ps1) 下载并提取该开发包中的 `libmpv-2.dll`，然后在本地保存为 `mpv\mpv-2.dll`，以适配当前应用的 P/Invoke 入口。

根据 upstream `mpv` 项目的许可证文件，`mpv` 采用 `GPL-2.0-or-later` 分发。Windows 预编译包可能还会间接包含其他第三方组件；在重新分发你自己的修改版安装包或二进制前，请同时核对 upstream 包和其依赖的许可证要求。

## Microsoft .NET Runtime

安装版使用 `win-x64 self-contained publish`，会随安装产物一起分发 Microsoft 提供的 .NET 运行时文件。这些文件的使用和再分发受 Microsoft `.NET` 相关许可证约束。
