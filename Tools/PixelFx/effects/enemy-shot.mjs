// Barrage family shots: a hostile glitch orb (no teal). A hot pink-white core, red body and a dark rim, with one
// row torn sideways and a magenta echo pixel on alternate frames. Authored on the player art's grid (18 px per unit)
// at the shot's collision diameter; Unity draws it at art size centered on the shot. Variants: enemy-bolt,
// enemy-pellet, enemy-heavy (big, slow) and enemy-shell (mortar, with a short fall trail above it).
import { fit } from '../core.mjs';

export const palette = ['#3d0614', '#9c1238', '#ff3b5c', '#ff9aae', '#fff0f3'];
export const accent = '#ff3df0';

function hash(a, b, c = 0) {
  let h = Math.imul(a ^ 0x9e3779b9, 0x85ebca6b) ^ Math.imul(b + 0x632be5ab, 0xc2b2ae35) ^ Math.imul(c + 0x165667b1, 0x27d4eb2f);
  h ^= h >>> 13; h = Math.imul(h, 0x27d4eb2f);
  return ((h ^ (h >>> 15)) >>> 0) / 4294967296;
}

// r: disc radius in px (the disc is centered on (0, 0)); frames: loop length; trail: px of fall trail above.
export function drawShot(p, t, o, fps) {
  const C = fit(o.palette, 5), r = o.radius, N = o.frames;
  const s = Math.floor(t * fps + 1e-6) % N;
  const pulse = s % 2;                                    // the core breathes one pixel on alternate frames
  const tear = Math.floor(hash(s, o.seed) * (2 * r - 1)) - r + 1, shift = s % 2 ? 1 : -1;
  // Fall trail (shell): a fading streak above the disc, flickering.
  for (let k = 1; k <= (o.trail || 0); k++) {
    const y = -r - k;
    if (hash(k, s, o.seed + 3) < 0.25 * k / o.trail) continue;
    p.px(0, y, k < o.trail / 2 ? C[2] : C[1]);
    if (k < o.trail / 3) { p.px(-1, y, C[1]); p.px(1, y, C[1]); }
  }
  for (let y = -r; y <= r; y++) {
    for (let x = -r; x <= r; x++) {
      const d = Math.hypot(x, y);
      if (d > r + 0.35) continue;
      let c;
      if (d > r - 0.65) c = C[0];                         // dark rim
      else if (d > r - 1.6) c = C[1];
      else if (d < (o.core + pulse) * 0.5 + 0.6) c = C[4];  // hot core
      else if (d < o.core + pulse) c = C[3];
      else c = C[2];
      // The torn row slides one pixel sideways, leaving a hole and an echo.
      const xx = y === tear && d <= r - 0.65 ? x + shift : x;
      p.px(xx, y, c);
    }
  }
  // Dead/echo pixels: a magenta echo on the rim and a missing pixel inside.
  const a = hash(s * 7, o.seed + 1) * Math.PI * 2;
  p.px(Math.round(Math.cos(a) * (r + 1)), Math.round(Math.sin(a) * (r + 1)), o.accent);
  if (r >= 4) {
    const b = hash(s * 11, o.seed + 2) * Math.PI * 2, rr = r - 2;
    p.px(Math.round(Math.cos(b) * rr), Math.round(Math.sin(b) * rr), C[0]);
  }
}

export function shotMeta(name, size, origin, frames, fps, options) {
  return { name, size, origin, duration: frames / fps, fps, loop: true, bg: 'dark',
    options: { palette, accent, frames, seed: 5, ...options } };
}
