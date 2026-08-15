# VideoDownloader

基于 .NET 9 + Avalonia UI 的 Windows 桌面视频下载与转码工具，底层驱动 yt-dlp（下载）与 ffmpeg（转码）。

## 功能

- **视频下载**：粘贴链接即可下载，支持多任务并行（1-8 个）、播放列表自动展开、格式/分辨率/HDR 选择、断点续传、完成后打开输出文件夹。
- **视频转码**：本地视频一键转码，支持 MP4 / MKV / WEBM / MOV 容器与 H.264 / H.265 / VP9 / AV1 编码，可选用软件编码（CPU）或硬件编码器（NVIDIA / Intel / AMD）。
- **Cookie 登录态**：选择浏览器后自动复用其已登录会话，可下载会员 / 登录后内容（`--cookies-from-browser`）。
- **代理支持**：HTTP(S) / SOCKS5 代理，可单独配置 Bilibili、YouTube 是否绕过代理，并内置一键网络连通测试。
- **多主题**：内置 Default / Cyberpunk / Neoclassical 三套界面主题。
- **GPU/驱动感知**：自动检测 NVIDIA 驱动版本，下载 ffmpeg 时选择兼容构建，避免 NVENC 驱动版本不匹配。

## 环境要求

- Windows 10 / 11（64 位）
- 单文件 exe 已内嵌 .NET 9 运行时，无需额外安装。

## 使用方法

1. **首次启动**：在主界面"设置 → 依赖检测"中点击对应「下载」按钮安装 yt-dlp 与 ffmpeg（会自动放入应用目录下 `.tools/`）。
2. **下载视频**：在「下载」页粘贴视频链接（每行一个）→ 点击任务行「获取分辨率」或「开始下载」→ 选择清晰度后下载。
3. **下载会员内容**：先在常用浏览器中登录目标网站，再到「设置」的「Cookie 来源」下拉选择该浏览器，之后下载会自动携带登录态。
4. **转码**：在「转码」页添加本地视频文件，选择容器 / 编码 / 编码器后点击「转换选中项」。
5. **代理**：如需代理，在「设置 → 代理设置」填好主机与端口并勾选启用，可用「网络测试」验证连通。

## 构建

```bash
dotnet publish src/VideoDownloader/VideoDownloader.csproj -c Release -r win-x64 --self-contained true -o publish
```

## 技术栈

- .NET 9 · Avalonia UI 12 · ReactiveUI（MVVM + 命令/绑定）
- Clean Architecture（Domain / Application / Infrastructure / UI 四项目分层）
- yt-dlp（下载与元数据解析）· ffmpeg（转码与合并）
- Serilog（日志）