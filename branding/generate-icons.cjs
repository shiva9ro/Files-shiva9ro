// Copyright (c) 2026 shiva9ro. Licensed under the MIT License.
// Run with Node.js and sharp available in NODE_PATH.
const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('sharp');

const root = path.resolve(__dirname, '..');
const templates = path.join(root, 'src/Files.App/Assets/AppTiles/Release');
const output = path.join(root, 'src/Files.App/Assets/AppTiles/Shiva9ro');

async function main() {
  const source = await fs.readFile(path.join(__dirname, 'shiva9ro-icon.svg'));
  const mono = (color) => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256"><defs><mask id="s"><path fill="white" d="M28 60a16 16 0 0 1 16-16h50l22 22h96a16 16 0 0 1 16 16v116a16 16 0 0 1-16 16H44a16 16 0 0 1-16-16Z"/><path fill="none" stroke="black" stroke-width="13" stroke-linecap="round" d="M153 117c-10-10-45-11-49 9-5 24 52 16 49 42-3 23-39 25-54 11"/></mask></defs><path fill="${color}" mask="url(#s)" d="M0 0h256v256H0Z"/></svg>`);
  let count = 0;
  async function renderFolder(relative = '') {
    for (const entry of await fs.readdir(path.join(templates, relative), { withFileTypes: true })) {
      const name = path.join(relative, entry.name);
      if (entry.isDirectory()) { await renderFolder(name); continue; }
      if (!name.endsWith('.png')) continue;
      const { width, height } = await sharp(path.join(templates, name)).metadata();
      const pixels = name.includes('contrast-white') ? mono('#000')
        : name.includes('contrast-black') || name.startsWith('BadgeLogo') ? mono('#fff') : source;
      const edge = Math.max(1, Math.round(Math.min(width, height) * 0.9));
      const icon = await sharp(pixels).resize(edge, edge).png().toBuffer();
      const target = path.join(output, name);
      await fs.mkdir(path.dirname(target), { recursive: true });
      await sharp({ create: { width, height, channels: 4, background: '#00000000' } })
        .composite([{ input: icon, gravity: 'centre' }]).png().toFile(target);
      count++;
    }
  }
  await renderFolder();
  const sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
  const images = await Promise.all(sizes.map(size => sharp(source).resize(size, size).png().toBuffer()));
  const directory = Buffer.alloc(6 + 16 * images.length);
  directory.writeUInt16LE(1, 2);
  directory.writeUInt16LE(images.length, 4);
  let offset = directory.length;
  images.forEach((data, index) => {
    const pos = 6 + index * 16;
    directory[pos] = directory[pos + 1] = sizes[index] === 256 ? 0 : sizes[index];
    directory.writeUInt16LE(1, pos + 4);
    directory.writeUInt16LE(32, pos + 6);
    directory.writeUInt32LE(data.length, pos + 8);
    directory.writeUInt32LE(offset, pos + 12);
    offset += data.length;
  });
  await fs.writeFile(path.join(output, 'Logo.ico'), Buffer.concat([directory, ...images]));
  console.log(`Generated ${count} PNG assets and one ICO from the original SVG.`);
}

main().catch(error => { console.error(error); process.exitCode = 1; });
