// Project-owned sample: a compact cyan/red glitch burst. No stock actor is baked in.
import { hex, fit } from '../core.mjs';
export const meta = {
  name: 'Glitch burst', size: [96, 80], origin: [48, 40],
  duration: 0.3, fps: 60, loop: false, bg: 'dark',
  options: { seed: 7, palette: ['#126879', '#28dfcf', '#eafffa'], accent: '#ff425e' }
};

export function draw(p, t, o) {
  if (t >= meta.duration) return;
  const u = t / meta.duration, fade = (1 - u) ** 2;
  const colors = fit(o.palette, 3), accent = hex(o.accent);
  let seed = o.seed >>> 0;
  const random = () => ((seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0) / 4294967296);
  for (let i = 0; i < 24; i++) {
    const a = random() * Math.PI * 2;
    const distance = 3 + (9 + random() * 26) * Math.sqrt(u);
    const x = Math.round(Math.cos(a) * distance), y = Math.round(Math.sin(a) * distance * 0.72);
    const width = Math.max(1, Math.round((2 + random() * 7) * (1 - u)));
    p.rect(x, y, width, i % 3 === 0 ? 2 : 1, i % 3 === 0 ? accent : colors[1], fade);
  }
  if (t < 0.05) {
    const r = Math.round(8 * (1 - t / 0.05));
    p.rect(-r, -1, r * 2 + 1, 3, colors[2]);
    p.rect(-1, -r, 3, r * 2 + 1, colors[2]);
  }
}
