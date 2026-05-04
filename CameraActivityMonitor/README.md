# CameraActivityMonitor
## 摄像头状态监控

这是一个 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 插件。
本插件主要用于监控系统中摄像头的使用状态，提供了一个规则用于指示。

## ✨ 主要功能

- **实时摄像头状态监控**：非轮询调用检测，是利用 Windows 原生 Media Foundation 接口 (`IMFSensorActivityMonitor`) 的Callback 实时监听，低功耗、无延迟。
- **提供触发规则**：新增了 `摄像头是否被使用`  规则。您可以利用该条件，在摄像头被其他软件（如相机）调用时，触发特定的 ClassIsland 操作或面板变化。
- **指定设备监控**：支持选择摄像头设备，可在设置中选择需要监控的特定摄像头。
- **自动启动**：支持跟随 ClassIsland 自动在后台开启监控逻辑。


## 🛠️ 系统要求

- **Windows 10 1703 及以上**

## ⚙️ 插件设置

在 ClassIsland 的【应用设置】 -> 找到 **CameraActivityMonitor**，可以进行以下配置：

1. **监控的摄像头设备**：在列表中选择想要监控的摄像头。
2. **自动启动监控**：勾选后，ClassIsland 每次启动时都会自动监听所选设备。

## 📖 使用方法

1. 安装插件。
2. 打开插件设置页：`CameraActivityMonitor`
3. 在“选择摄像头”中选择要监控的设备
4. 点击“开启”开始监控（可随时“关闭”）
5. 可打开“自动开启监控”开关：
   - 开启后，应用启动时若已保存 `SelectedCamera`，会自动开始监控

## 🧩 规则用法

安装本插件后，在 ClassIsland 任何支持“规则”的地方添加新条件：
- 选择 **摄像头是否被使用**：当选定监控的摄像头正在被调用（推流）时，该规则即为生效状态。
### 使用示例
- 对于一个文本组件，配置在 非 `摄像头是否被使用` 时隐藏，可以实现指示摄像头状态的文字。
- 对于自动化提醒，可以设置 `规则集更新时` 触发器，并且选中 `摄像头是否使用` ，然后配置提醒或其它操作。

## 🤝 参与贡献 (Contributing)

欢迎提交 Issue 和 Pull Request 来帮助完善此项目！

## 致谢
- 插件图标由 [<img src="https://github.com/LiPolymer.png" width="20" height="20"/>](https://github.com/LiPolymer)[@LiPolymer](https://github.com/LiPolymer) 提供 [#1](https://github.com/LiuYan-xwx/CameraActivityMonitor/pull/1) 😋😋
- 本项目使用了以下第三方库：
  - [CsWin32](https://github.com/microsoft/CsWin32) - 用于生成 Windows API 的 C# 绑定。

## 📄 许可证 (License)

本项目采用 [AGPLv3](https://github.com/LiuYan-xwx/CameraActivityMonitor/blob/master/LICENSE.txt) 许可证。
