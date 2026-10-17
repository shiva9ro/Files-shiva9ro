# Files (shiva9ro)

![Files (shiva9ro) icon](../src/Files.App/Assets/AppTiles/Shiva9ro/Square44x44Logo.targetsize-256.png)

**Unofficial personal fork of [Files](https://github.com/files-community/Files), based on v4.3.**
This is not an official Files release and is not affiliated with, endorsed by,
or supported by Files Community. For the official application, visit
[files.community](https://files.community/).

## Changes

- Native OLE drag-and-drop handling for elevated/UAC-disabled Windows environments,
  adapted from shiva9ro's ModernFileManager implementation and integrated with
  Files' existing storage operations.
- Separate intermediate output for the Win32/x64 file dialogs to avoid concurrent
  writes during a solution build.
- Independent package identity (`FilesShiva9ro`), execution alias
  (`files-shiva9ro.exe`), and original teal folder/S icon.

This is a focused adaptation, not a promise of complete compatibility with every
Windows configuration. Release/x64 has been tested locally; the cause of the
previous Debug MSIX startup failure has not been established.

## Build and install

Use Visual Studio with the prerequisites described in the
[upstream build documentation](https://files.community/docs/contributing/building-from-source).
Select **Release / x64**, rebuild the solution, then create a sideload MSIX package
for the same configuration. Packaging requires your own signing certificate.
No signing private key or prebuilt installer is provided in this repository.

This installs separately from official Files and earlier FilesDev/FilesRibra
packages. Its settings are separate. The package publisher remains `CN=Files`
to match the local signing certificate; the displayed publisher is `shiva9ro`.

## Licensing and credits

Files Community and the upstream contributors retain copyright in their work.
Keep the original copyright notices, [MIT license](../LICENSE-MIT),
[MPL-2.0 license](../LICENSE-MPL), and [third-party notices](NOTICE.md).
Each file retains its applicable license; the repository is not uniformly
or optionally dual-licensed merely because it contains two license texts.

Fork-specific additions are MIT-licensed unless otherwise stated. The new icon
and its vector source are MIT-licensed under [branding/LICENSE](../branding/LICENSE).
No ownership of the upstream Files name, logo, or trademarks is claimed.

See [FORK-NOTICE.md](../FORK-NOTICE.md) for provenance, distribution information,
and license scope. Applicable license texts and notices are also included by
this project's packaging settings in the application's `Licenses` directory.

## Support

This fork is provided as-is for personal use. There is no commitment to ongoing
maintenance, support, or future upstream updates. Do not send issues caused by
this fork to the upstream Files project. Upstream documentation, contributor
credits, and third-party notices remain attributable to their original authors.
