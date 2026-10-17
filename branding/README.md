# Files (shiva9ro) icon

The teal folder and S in `shiva9ro-icon.svg` are original artwork for this
unofficial fork, not a recoloring or tracing of the Files Community logo.
The S is a vector path; no font or third-party illustration is embedded.

Copyright (c) 2026 shiva9ro. Licensed under the [MIT License](LICENSE).
This license also covers the generated icon files in
`src/Files.App/Assets/AppTiles/Shiva9ro/`.

The checked-in PNG and ICO files are ready to build; regeneration is optional.
To regenerate, use Node.js with `sharp` available and run
`node branding/generate-icons.cjs`. The generator reads the dimensions and
Windows asset qualifiers of the upstream Release assets, then renders the
new SVG. It does not use pixels from the upstream artwork.
