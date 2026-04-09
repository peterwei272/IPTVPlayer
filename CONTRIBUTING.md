# Contributing

感谢你关注这个项目。

## 开发前提

- Windows 10/11 x64
- `.NET 6 SDK`
- `Inno Setup 6`（仅安装包打包需要）

## 本地构建

```powershell
dotnet build .\IPTVPlayer.sln -c Release
```

如果你需要本地实际播放或打安装包，请先准备 `libmpv`：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\ensure-mpv.ps1
```

## 安装包构建

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

这个脚本会自动检查本地 `mpv\mpv-2.dll`，缺失时尝试准备运行库，再执行 `publish` 和 Inno Setup 打包。

## 提交前约定

- 不要提交 `build/`、`temp/`、本地缓存、安装包、下载产物或 `mpv\mpv-2.dll`
- 不要在仓库里加入任何默认 IPTV 内容、默认订阅地址或可直接使用的节目单数据
- 保持 README、第三方说明和构建脚本中的版本信息一致
- 如果改动影响安装包或 `libmpv` 获取流程，请同步更新 `README.md` 和 `THIRD_PARTY_NOTICES.md`

## 代码风格

- 默认使用 ASCII 文本
- 尽量保持现有 WPF / MVVM 结构
- 优先保证播放器基本行为稳定，再做界面层优化
