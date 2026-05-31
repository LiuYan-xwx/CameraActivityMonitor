<div align="center">

<img src="https://raw.githubusercontent.com/LiuYan-xwx/CameraActivityMonitor/refs/heads/master/CameraActivityMonitor/icon.png" alt="Plugin Icon" height="100">

# CameraActivityMonitor


![GitHub License](https://img.shields.io/github/license/LiuYan-xwx/CameraActivityMonitor)
![GitHub top language](https://img.shields.io/github/languages/top/LiuYan-xwx/CameraActivityMonitor)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/LiuYan-xwx/CameraActivityMonitor/total?label=%E6%80%BB%E4%B8%8B%E8%BD%BD%E9%87%8F)
![GitHub Repo stars](https://img.shields.io/github/stars/LiuYan-xwx/CameraActivityMonitor)

</div>

一个用于监控摄像头占用状态的 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 插件。

通过 Windows 的 Media Foundation 接口监听摄像头当前是否正在被其它程序使用，并把这个状态提供给 ClassIsland 的规则系统。

## 功能

- 监控指定摄像头是否正在被占用
- 在 ClassIsland 中提供 `摄像头是否被使用` 规则
- 支持在设置页选择要监控的摄像头设备
- 支持跟随 ClassIsland 启动后自动开始监控
- 支持匹配占用的进程名

## 设置截图
<img width="1552" height="726" alt="image" src="https://github.com/user-attachments/assets/9436026b-37f9-4a82-8c73-317507f9eb8e" />

## 系统要求

- **Windows 10 1703 及以上**
- ClassIsland 2.0+

## 使用方法

安装插件后，打开 ClassIsland 的应用设置，进入 **CameraActivityMonitor** 设置页。

在这里可以：

1. 选择需要监控的摄像头。
2. 点击“开启”开始监控，点击“关闭”停止监控。
3. 查看当前的状态
4. 如有需要，可以点击“刷新”重新读取摄像头列表。
5. 打开“自动开启监控”后，ClassIsland 下次启动时会自动开始监控已选择的摄像头。

## 规则用法

插件会注册一个规则：

```
摄像头是否被使用
```

当当前选中的摄像头正在被其它程序调用时，这个规则会处于生效状态。

规则支持匹配占用的进程名，需要勾选 “匹配进程名”，然后按照设置页的进程名合适填写，不区分大小写。当且仅当摄像头被占用，且占用的进程匹配所设置的，此规则才会生效。


### 使用示例

* 使用文本组件，在 非 `摄像头是否被使用` 时隐藏，实现被使用的提示文字，或 emoji
* 配合自动化功能，在摄像头状态变化时触发提醒或其它操作。

## 注意事项

* 本插件依赖 Windows 的 Media Foundation 摄像头活动监控接口，因此只支持 Windows。
* 插件只判断摄像头是否被占用，不会读取、保存或上传摄像头画面。
* 如果摄像头列表为空，可以先确认系统里能否正常识别摄像头，然后回到设置页点击“刷新”。

## 开发与构建

项目基于 .NET 8 和 ClassIsland Plugin SDK。

本地构建插件包可以运行：

```powershell
./tools/publish.ps1
```

脚本会执行 Release 发布，并启用 `CreateCipx` 生成插件包。

## 致谢

* 插件图标由 [@LiPolymer](https://github.com/LiPolymer) 提供：[#1](https://github.com/LiuYan-xwx/CameraActivityMonitor/pull/1) 😋😋
* 本项目使用 [CsWin32](https://github.com/microsoft/CsWin32) 生成 Windows API 的 C# 绑定

## 许可证

本项目使用 [AGPLv3](https://github.com/LiuYan-xwx/CameraActivityMonitor/blob/master/LICENSE.txt) 许可证。
