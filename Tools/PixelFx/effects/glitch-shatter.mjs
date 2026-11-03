// A defeated unit is deleted: a white selection box snaps out, data blocks burst, tear lines slide and pixels rain down.
// (0, 0) is the unit's body center. Authored on the player art's grid (18 px per unit). No actor baked in.
import { fit, hex, burst, fly } from '../core.mjs';

export const meta = {
  name: 'Glitch shatter', size: [80, 72], origin: [40, 36],
  duration: 0.6, fps: 30, loop: false, bg: 'dark',
  options: { seed: 13, palette: ['#1b7f8c', '#38d6d0', '#79d9df', '#e6fffb'], accent: '#d84255' }
};

export function draw(p, t, o) {
  if (t >= meta.duration) return;
  const C = fit(o.palette, 4), red = hex(o.accent), white = 0xffffffff;

  // Selection box: snaps from the body's size outward, white then teal, gone in a few frames.
  if (t < 0.1) {
    const k = t / 0.1, h = Math.round(10 + 14 * k), w = Math.round(12 + 16 * k), c = k < 0.4 ? white : C[2];
    for (const [x0, y0, x1, y1] of [[-w, -h, w, -h], [-w, h, w, h], [-w, -h, -w, h], [w, -h, w, h]])
      p.line(x0, y0, x1, y1, c, 1 - k * 0.7);
    if (t < 0.04) p.each(-8, -8, 8, 8, (x, y, sx, sy) => p.blendPx(sx, sy, white, 0.5 * (1 - t / 0.04)));
  }

  // Data blocks burst out, slow, and flicker away.
  burst(28, o.seed, (r, i) => {
    const delay = r.range(0, 0.05), life = r.range(0.3, 0.55), age = t - delay;
    if (age < 0 || age >= life) return;
    const a = r.range(0, Math.PI * 2), speed = r.range(50, 150);
    const [x, y] = fly(r.range(-6, 6), r.range(-8, 8), Math.cos(a) * speed, Math.sin(a) * speed * 0.8, age, 120, 3.5);
    const u = age / life;
    if (u > 0.6 && Math.floor(age * 30 + i) % 2) return;
    const s = Math.max(1, Math.round(r.range(2, 4.5) * (1 - u * 0.6)));
    p.rect(Math.round(x - s / 2), Math.round(y - s / 2), s, s, i % 3 === 0 ? red : u < 0.25 ? C[3] : C[1 + (i % 2)]);
  });

  // Tear lines slide across the body's height for the first beats.
  if (t >= 0.02 && t < 0.22) {
    burst(4, o.seed + 5, (r, i) => {
      const y = Math.round(r.range(-14, 14)), len = Math.round(r.range(12, 30));
      const slide = Math.round((t - 0.02) * r.range(-120, 120));
      p.line(slide - len / 2, y, slide + len / 2, y, i % 2 ? red : C[2], 1 - (t - 0.02) / 0.2);
    });
  }

  // Pixels rain out of where the body was and wink out.
  burst(14, o.seed + 9, (r) => {
    const delay = r.range(0.08, 0.25), life = r.range(0.2, 0.35), age = t - delay;
    if (age < 0 || age >= life) return;
    const [x, y] = fly(r.range(-12, 12), r.range(-10, 6), 0, r.range(-20, 10), age, 220, 0);
    p.dot(Math.round(x), Math.round(y), r.chance(0.3) ? red : C[2], 1 - age / life);
  });
}
