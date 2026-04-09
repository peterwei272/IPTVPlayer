# IPTVPlayer

一个面向 Windows 的 IPTV 播放器，基于 `.NET 6 + WPF + libmpv`，支持多订阅源、多 EPG、4K/HDR/HLG 播放、频道分组筛选和安装版打包。

项目不提供任何 IPTV 内容、默认订阅或节目单服务。首次启动后，请在“管理源”中自行填写 `M3U` 和 `EPG` 地址。

## 功能

- 导入多个 `M3U URL`
- 导入多个 `EPG URL`，支持 `.xml` 与 `.xml.gz`
- 启动时优先读取本地缓存，再后台刷新订阅和节目单
- 基于 `tvg-id` / `tvg-name` / 频道显示名的精确 EPG 匹配
- 基于 `libmpv` 的 `SDR / HDR10 / HLG / 4K` 播放
- 搜索、分组筛选、右侧节目单、全屏和键盘切台

## 许可证

本项目采用 `GPL-2.0-or-later` 开源。

第三方依赖和分发说明见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。

## 环境要求

- Windows 10/11 x64
- `.NET 6 SDK`（源码构建需要）
- `Inno Setup 6`（仅安装包构建需要）

## 源码构建

```powershell
dotnet build .\IPTVPlayer.sln -c Release
```

默认输出目录：

`build\bin\Release\net6.0-windows\`

## 准备 mpv 运行库

源码仓库默认不直接提交 `mpv\mpv-2.dll`，需要在本地准备运行库：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\ensure-mpv.ps1
```

这个脚本会使用 `Invoke-WebRequest -UserAgent "Wget"` 从 SourceForge 获取锁定版本的 `shinchiro` Windows `libmpv` 开发包，校验文件类型和 SHA256 后提取 `libmpv-2.dll`，并在本地放到 `mpv\mpv-2.dll`。

如果缺少 `mpv\mpv-2.dll`，项目仍可编译，但播放器无法实际播放视频。

## 绿色版运行

先完成构建，再运行：

`build\bin\Release\net6.0-windows\IPTVPlayer.App.exe`

绿色版模式下，程序优先把设置和缓存写到程序目录下的 `data\`；如果目录不可写，则回退到 `%LocalAppData%\IPTVPlayer`。

## 安装版打包

安装版采用 `Windows x64 + self-contained` 发布，不依赖目标机器预装 `.NET 6 Desktop Runtime`。

推荐脚本：

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

脚本会：

- 检查本地 `mpv\mpv-2.dll`，缺失时自动调用 `scripts\ensure-mpv.ps1`
- 执行 `dotnet publish` 到 `build\publish\win-x64\`
- 自动查找 `ISCC.exe` 并生成 `build\installer\IPTVPlayer-Setup-x64.exe`

默认安装目录：

`C:\Program Files\IPTVPlayer`

安装版运行时会检测安装目录中的 `installed.mode` 标记文件，并强制把设置和缓存写到 `%LocalAppData%\IPTVPlayer`。

## 常用操作

- `Enter`: 播放当前选中频道
- `Up / Down`: 切换并播放上下频道
- `F5`: 手动刷新订阅和 EPG
- `F11`: 全屏
- 双击视频区域：切换全屏
- 顶部“管理源”：新增、删除、排序 `M3U` / `EPG` 源

## 开源说明

- 源码仓库不包含任何默认 IPTV 源
- 源码仓库不直接提交 `mpv\mpv-2.dll` 这类大体积二进制
- 如果你准备参与开发，请先阅读 [CONTRIBUTING.md](./CONTRIBUTING.md)
