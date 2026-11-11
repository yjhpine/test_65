// Lingering hit debris: a pixel spark, then teal/red shards and data blocks that spray forward, arc down and blink out.
// Faces right; (0, 0) is the contact point. Authored on the player art's grid (18 px per unit). No actor baked in.
import { fit, hex, burst, fly, flyV } from '../core.mjs';

export const meta = {
  name: 'Glitch hit debris', size: [56, 44], origin: [14, 24],
  duration: 0.45, fps: 30, loop: false, bg: 'dark',
  options: { seed: 9, palette: ['#1b7f8c', '#38d6d0', '#79d9df', '#e6fffb'], accent: '#d84255' }
};

const G = 260, DRAG = 3;

export function draw(p, t, o) {
  if (t >= meta.duration) return;
  const C = fit(o.palette, 4), red = hex(o.accent);

  // Contact spark: a pixel cross that snaps shut in a few frames.
  if (t < 0.06) {
    const k = 1 - t / 0.06, arm = Math.round(1 + 3 * k);
    p.line(-arm, 0, arm, 0, C[3]);
    p.line(0, -arm, 0, arm, C[3]);
    p.px(0, 0, 0xffffffff);
  }

  burst(16, o.seed, (r, i) => {
    const delay = r.range(0, 0.04), life = r.range(0.2, 0.4), age = t - delay;
    if (age < 0 || age >= life) return;
    const a = r.range(-1.05, 0.55), speed = r.range(60, 150);
    const vx = Math.cos(a) * speed, vy = Math.sin(a) * speed;
    const [x, y] = fly(0, 0, vx, vy, age, G, DRAG), [ux, uy] = flyV(vx, vy, age, G, DRAG);
    const u = age / life, color = i % 4 === 0 ? red : u < 0.35 ? C[3] : C[2];
    if (i % 5 === 0) {
      // Data blocks blink on alternate frames once they slow down.
      if (u > 0.55 && Math.floor(age * 30) % 2) return;
      const s = u < 0.5 ? 2 : 1;
      p.rect(Math.round(x), Math.round(y), s, s, i % 2 ? red : C[2]);
    } else {
      // Shards are short streaks along their motion, shortening as they slow.
      const len = Math.max(1, Math.hypot(ux, uy) * 0.018);
      const m = Math.hypot(ux, uy) || 1;
      p.line(x, y, x - (ux / m) * len, y - (uy / m) * len, (v) => (v < 0.3 ? color : C[1]), 1 - u * u);
    }
  });

  // Two short slices slip sideways right after contact.
  if (t >= 0.02 && t < 0.12) {
    const shift = Math.floor(t * 60) % 2 ? 2 : -1;
    p.line(4 + shift, -4, 9 + shift, -4, red, 0.9);
    p.line(2 - shift, 3, 6 - shift, 3, C[2], 0.9);
  }
}
