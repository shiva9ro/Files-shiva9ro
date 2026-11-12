## UAC無効環境でのドラッグ＆ドロップ修正

このリポジトリは Files v4.3 の非公式forkです。UAC（ユーザーアカウント制御）が
無効なWindowsで管理者として動く場合など、Filesが昇格しているときのファイル・
フォルダーのドラッグ＆ドロップを修正しています。

昇格時のドロップ受信には、WinUIのドロップ処理に代わってWindows OLEの
`IDropTarget` を使います。受信した `CF_HDROP` データからファイルやフォルダーの
パスを取得し、ドロップ先を判定して、Filesの既存のコピー・移動処理に渡します。
Filesのウィンドウ外へドラッグしたときは、OLEのドラッグ処理に引き継ぎます。

- [MainWindow.NativeDrop.cs](../src/Files.App/MainWindow.NativeDrop.cs) — ドロップの受信を有効にし、ドロップ先を判定します。
- [OleFileReceiver.cs](../src/Files.App/Utils/Shell/OleFileReceiver.cs) — `IDropTarget` を実装し、受信データからパスを取り出します。
- [BaseLayoutPage.NativeDrop.cs](../src/Files.App/Views/Layouts/BaseLayoutPage.NativeDrop.cs) — 取得したパスを既存のコピー・移動処理に渡します。
- [MainWindow.LocalDrag.cs](../src/Files.App/MainWindow.LocalDrag.cs) と [OleFileSender.cs](../src/Files.App/Utils/Shell/OleFileSender.cs) — ウィンドウ内のドラッグと、外へ出たときのOLEへの引き継ぎを扱います。

[上流 v4.3 とD&D修正版のソース差分を見る](https://github.com/shiva9ro/Files-shiva9ro/compare/v4.3...v4.3-uac-drag-drop)

## Drag-and-drop fix for UAC-disabled Windows

This unofficial fork of Files v4.3 fixes file drag-and-drop for elevated Files
sessions, including administrator sessions on Windows with User Account
Control (UAC) disabled.

For elevated sessions, Files registers a native Windows OLE `IDropTarget`
in place of its WinUI drop handlers. The target reads file and folder paths
from the incoming OLE `CF_HDROP` data. Files resolves the drop destination
and hands the paths to its existing copy or move operations. When a drag
leaves the Files window, Files starts an outbound OLE drag.

- [MainWindow.NativeDrop.cs](../src/Files.App/MainWindow.NativeDrop.cs) — enables native drop handling and resolves the destination.
- [OleFileReceiver.cs](../src/Files.App/Utils/Shell/OleFileReceiver.cs) — implements `IDropTarget` and extracts file paths.
- [BaseLayoutPage.NativeDrop.cs](../src/Files.App/Views/Layouts/BaseLayoutPage.NativeDrop.cs) — connects the drop to Files' existing copy and move operations.
- [MainWindow.LocalDrag.cs](../src/Files.App/MainWindow.LocalDrag.cs) and [OleFileSender.cs](../src/Files.App/Utils/Shell/OleFileSender.cs) — handle drags within the window and the handoff to outbound OLE.

[Compare upstream v4.3 with the tagged drag-and-drop fix](https://github.com/shiva9ro/Files-shiva9ro/compare/v4.3...v4.3-uac-drag-drop)

---

## Original Files project

The following content is retained from the upstream Files repository.
Download links and contribution instructions below refer to the
official Files project, not this modified fork.

<p align="center">
  <img alt="Files hero image" src="./assets/ReadmeHero.png" />
</p>

<p align="center">
  <a style="text-decoration:none" href="https://files.community/">
    <img src="https://img.shields.io/badge/Files-Website-F9B81F" alt="Files Website" /></a>
  <a style="text-decoration:none" href="https://github.com/files-community/Files/actions/workflows/ci.yml">
    <img src="https://github.com/files-community/Files/actions/workflows/ci.yml/badge.svg" alt="Files CI Status" /></a>
  <a style="text-decoration:none" href="https://crowdin.com/project/files-app">
    <img src="https://badges.crowdin.net/files-app/localized.svg" alt="Files Localization Status" /></a>
  <a style="text-decoration:none" href="https://discord.gg/files">
    <img src="https://img.shields.io/discord/725513575971684472?label=Discord&color=7289da" alt="Files Discord" /></a>
</p>

Files is a modern file manager that helps users organize their files and folders. Our mission with Files is to build the best file manager for Windows, and we’re proud to be building it out in the open so everyone can participate. User feedback helps shape the features we work on, & the bug reports on GitHub help to make Files more reliable. Built and maintained by the open-source community, Files features robust multitasking experiences, file tags, deep integrations, and an intuitive design.

## Installing and running Files

Files is a community-driven project that depends on your support to grow and improve. Please consider purchasing Files through the Microsoft Store or supporting us on GitHub if you use the classic installer.

You can also use the preview version alongside the stable release to get early access to new features and improvements.

<p align="left">
  <!-- Store Badge -->
  <a style="text-decoration:none" href="https://apps.microsoft.com/detail/9NGHP3DX8HDX?launch=true&mode=full">
    <picture>
      <source media="(prefers-color-scheme: light)" srcset="./assets/StoreBadge-dark.png" height="80" />
      <img src="./assets/StoreBadge-light.png" height="80" /></picture></a>
  &ensp;
  <!-- Classic Installer Badge -->
  <a style="text-decoration:none" href="https://files.community/download">
    <picture>
      <source media="(prefers-color-scheme: light)" srcset="./assets/ClassicInstallerBadge-dark.png" height="80" />
      <img src="./assets/ClassicInstallerBadge-light.png" height="80" /></picture></a>
</p>

## Building from source

Instructions for building the source code can be found on our [documentation site](https://files.community/docs/contributing/building-from-source).


## Contributing to Files

Want to contribute to this project? Let us know with an [issue](https://github.com/files-community/Files/issues) that communicates your intent to create a [pull request](https://github.com/files-community/Files/pulls). Also, view our [contributing guidelines](https://github.com/files-community/Files/blob/main/.github/CONTRIBUTING.md) to make sure you're up to date on the coding conventions.

Looking for a place to start? Check out the [task board](https://github.com/orgs/files-community/projects/3/views/2), where you can sort tasks by size and priority.

## Screenshots

![Files](./assets/FilesScreenshot.png)
