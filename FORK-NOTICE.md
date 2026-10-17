# Files (shiva9ro): origin and licensing

This is an unofficial, personal fork of [Files](https://github.com/files-community/Files),
based on upstream tag `v4.3`, commit `cbfb4ba54a89632fbceb82ea94ada60a9bae3737`.
It is not an official Files release and is not endorsed or supported by Files Community.

The main changes are native OLE drag-and-drop handling for elevated/UAC-disabled
Windows environments, fixes to dialog build output isolation, and independent
package naming and branding. Drag-and-drop behavior was adapted from the
author's local ModernFileManager implementation and integrated with Files'
existing storage operations and generated Win32 interop.

## Existing code and dependencies

- Files Community and the upstream contributors retain their copyright in
  their work. Existing copyright and license notices are preserved.
- Upstream MIT-licensed material remains under [LICENSE-MIT](LICENSE-MIT).
- Files explicitly marked MPL-2.0 remain under [LICENSE-MPL](LICENSE-MPL).
  The presence of both license files does not make every file dual-licensed.
- Third-party components retain their own terms; see
  [.github/NOTICE.md](.github/NOTICE.md). This fork does not replace those terms.
- Fork-specific additions are MIT-licensed unless a file states otherwise.
  Changes to existing MPL-covered files remain MPL-2.0.

## Name and artwork

"Files" identifies the upstream project. No ownership of its name, logo,
or trademarks is claimed. This fork uses the distinct display name
`Files (shiva9ro)` and package identity `FilesShiva9ro`.

The active application icon is original shiva9ro artwork under
[branding/LICENSE](branding/LICENSE). Original upstream artwork remains in
the repository as part of its source history and is not the fork's app icon.
Its presence does not imply a new grant of trademark rights.

## Distribution and maintenance

Source for this fork is available at
https://github.com/shiva9ro/Files-shiva9ro.
When distributing a binary, identify its corresponding source commit and
provide the applicable license texts and third-party notices with it.
For any MPL-covered material distributed in executable form, tell recipients
where to obtain the corresponding MPL-covered source.

This fork is provided as-is for personal use. There is no commitment to
ongoing maintenance, support, or compatibility with future Files releases.
Do not report fork-specific problems to the upstream project.
