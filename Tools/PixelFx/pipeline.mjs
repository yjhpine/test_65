import { existsSync, readFileSync, writeFileSync, mkdirSync, readdirSync, copyFileSync, unlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const vendor = join(root, 'Library/PixelFxPipeline/vendor/pixelfx');
const lock = JSON.parse(readFileSync(join(here, 'pixelfx.lock.json'), 'utf8'));
const [command = 'help', id = 'custom/glitch-burst', optionsFile] = process.argv.slice(2);
const json = (path, value) => writeFileSync(path, JSON.stringify(value, null, 2) + '\n');
function git(args) {
  const r = spawnSync('git', args, { encoding: 'utf8' });
  if (r.status !== 0) throw new Error(r.stderr || r.error?.message || 'Git failed');
  return r.stdout.trim();
}
function ensure() {
  if (!existsSync(join(vendor, '.git'))) throw new Error('Run: node Tools/PixelFx/pipeline.mjs setup');
  if (git(['-C', vendor, 'rev-parse', 'HEAD']) !== lock.commit) throw new Error('Vendor revision differs from pixelfx.lock.json. Run setup.');
  if (git(['-C', vendor, 'status', '--porcelain'])) throw new Error('Vendor contains edits. Preserve them outside vendor before continuing.');
}
if (command === 'setup') {
  if (!existsSync(vendor)) {
    mkdirSync(dirname(vendor), { recursive: true });
    git(['clone', lock.repository, vendor]);
  }
  if (git(['-C', vendor, 'status', '--porcelain'])) throw new Error('Vendor contains edits; setup will not overwrite them.');
  git(['-C', vendor, 'checkout', '--detach', lock.commit]);
  ensure();
  console.log(`Ready: ${lock.commit}\nNo npm install required for PNG/HTML export.`);
} else if (command === 'list' || command === 'render') {
  ensure();
  const lib = await import(pathToFileURL(join(vendor, 'tools/lib/fxlib.mjs')));
  const { encodePNG } = await import(pathToFileURL(join(vendor, 'tools/lib/png.mjs')));
  if (command === 'list') {
    console.log([...lib.allIds(), ...readdirSync(join(here, 'effects')).filter(f => f.endsWith('.mjs')).map(f => 'custom/' + f.slice(0, -4))].join('\n'));
  } else {
    if (!/^[a-z0-9-]+\/[a-z0-9-]+$/.test(id)) throw new Error('Use a category/name ID from list.');
    const name = id.split('/')[1];
    const source = id.startsWith('custom/') ? join(here, 'effects', name + '.mjs') : join(vendor, 'src/fx', id + '.js');
    const mod = await import(pathToFileURL(source));
    const fx = { id, mod, meta: mod.meta };
    const settings = optionsFile ? JSON.parse(readFileSync(resolve(optionsFile), 'utf8')) : {};
    const fps = settings.fps ?? mod.meta.fps;
    if (!Number.isFinite(fps) || fps < 1 || fps > 120) throw new Error('fps must be 1..120');
    const seed = settings.seed ?? 7;
    const args = { actor: false, fps, seed, options: settings.options ?? {} };
    const r = lib.renderFrames(fx, args);
    const repeat = lib.renderFrames(fx, args);
    const digest = frames => createHash('sha256').update(Buffer.concat(frames.map(f => Buffer.from(f.clear)))).digest('hex');
    if (digest(r.frames) !== digest(repeat.frames)) throw new Error('Non-deterministic effect output');
    if (!r.frames.some(f => f.clear.some((a, i) => i % 4 === 3 && a > 0))) throw new Error('Effect is entirely empty');
    const out = join(root, 'Library/PixelFxPipeline/output', id);
    mkdirSync(join(out, 'frames'), { recursive: true });
    // Remove only this generator's obsolete numbered frames on a shorter rerender.
    for (const file of readdirSync(join(out, 'frames'))) {
      const match = file.match(new RegExp(`^${name}-(\\d+)\\.png$`));
      if (match && Number(match[1]) >= r.n) unlinkSync(join(out, 'frames', file));
    }
    const sheet = lib.spriteSheet(fx, r, name);
    // Copy straight RGBA exactly: upstream canvas.paste blends RGB against black,
    // which darkens semi-transparent edges when a Unity sprite is blended again.
    const { w, h } = sheet.json.meta.size;
    const rgba = new Uint8Array(w * h * 4);
    r.frames.forEach((f, i) => {
      const rect = sheet.json.frames[i].frame;
      for (let y = 0; y < r.h; y++) rgba.set(f.clear.subarray(y * r.w * 4, (y + 1) * r.w * 4), ((rect.y + y) * w + rect.x) * 4);
      writeFileSync(join(out, 'frames', `${name}-${String(i).padStart(3, '0')}.png`), encodePNG(r.w, r.h, f.clear));
    });
    const png = encodePNG(w, h, rgba);
    writeFileSync(join(out, name + '.png'), png);
    json(join(out, name + '.json'), sheet.json);
    writeFileSync(join(out, 'contact.png'), lib.contactSheet(fx, r, { max: 16, zoom: 3 }));
    for (const file of ['LICENSE', 'NOTICE.md']) copyFileSync(join(vendor, file), join(out, file));
    const edgePixels = r.frames.reduce((sum, f) => sum + Array.from({ length: r.w * r.h }, (_, i) => i).filter(i => (i % r.w === 0 || i % r.w === r.w - 1 || i < r.w || i >= r.w * (r.h - 1)) && f.clear[i * 4 + 3] > 0).length, 0);
    const lastFrameEmpty = !r.frames.at(-1).clear.some((a, i) => i % 4 === 3 && a > 0);
    json(join(out, 'manifest.json'), { id, repository: lock.repository, commit: lock.commit, sourceSha256: createHash('sha256').update(readFileSync(source)).digest('hex'), seed, fps, options: args.options, frames: r.n, frameSize: [r.w, r.h], duration: mod.meta.duration, loop: mod.meta.loop, unityPivot: [mod.meta.origin[0] / r.w, 1 - mod.meta.origin[1] / r.h], rgbaSha256: digest(r.frames), deterministic: true, edgePixels, lastFrameEmpty });
    const preview = `<!doctype html><meta charset="utf-8"><title>PixelFX preview</title><style>body{background:#171822;color:#eee;font:16px system-ui;text-align:center}canvas{image-rendering:pixelated;max-width:95vw;background:repeating-conic-gradient(#222530 0% 25%,#1b1e28 0% 50%) 0/24px 24px}button,input{margin:12px}</style><h1>${id}</h1><canvas width="${r.w}" height="${r.h}" style="width:${r.w * 4}px"></canvas><p><button id="play">Pause</button><input id="seek" type="range" min="0" max="${r.n - 1}" value="0"><span id="label"></span></p><script>const data=${JSON.stringify(sheet.json)},fps=${fps},img=new Image(),canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d'),seek=document.querySelector('#seek'),label=document.querySelector('#label');let playing=true,index=0,last=0;document.querySelector('#play').onclick=e=>{playing=!playing;e.target.textContent=playing?'Pause':'Play'};seek.oninput=()=>{index=+seek.value;playing=false;document.querySelector('#play').textContent='Play';draw()};function draw(){const f=data.frames[index].frame;ctx.clearRect(0,0,canvas.width,canvas.height);ctx.drawImage(img,f.x,f.y,f.w,f.h,0,0,f.w,f.h);seek.value=index;label.textContent=(index+1)+' / '+data.frames.length+' · '+fps+' fps'}function tick(t){if(playing&&t-last>=1000/fps){index=(index+Math.floor((t-last)*fps/1000))%data.frames.length;last=t;draw()}requestAnimationFrame(tick)}img.onload=()=>{draw();requestAnimationFrame(t=>{last=t;tick(t)})};img.src='data:image/png;base64,${png.toString('base64')}';</script>`;
    writeFileSync(join(out, 'preview.html'), preview);
    console.log(`${out}\n${r.n} frames, ${r.w}x${r.h}, ${fps} fps; deterministic; edge pixels=${edgePixels}; last frame empty=${lastFrameEmpty}`);
  }
} else if (command === 'new') {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id)) throw new Error('Use a kebab-case effect name');
  const target = join(here, 'effects', id + '.mjs');
  writeFileSync(target, readFileSync(join(here, 'effects/glitch-burst.mjs')), { flag: 'wx' });
  console.log(`Created ${target}. Edit meta/draw, then render custom/${id}.`);
} else {
  console.log('node Tools/PixelFx/pipeline.mjs setup | list | new <name> | render <category/name> [settings.json]');
}
