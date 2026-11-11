// Glitch teleport streak: the straight line from departure to arrival, in the glitch teal/red pair.
// One 32x16 tile, repeated along the path in Unity (Tiled), +x = the travel direction. The game grows the line
// from the departure in ~0.05 s, then these frames play: a bright speed line (0-1), glitch shake with a
// teal/red channel split, slid blocks and dropped pixels (2-4), then fragments breaking off (5-7) while the
// game pulls the tail in from the departure side.
// Authored on the player art's grid (18 px per unit). Every pattern wraps at 32 px so tiles meet cleanly.
import { fit } from '../core.mjs';

export const meta = {
  name: 'Glitch streak', size: [32, 16], origin: [0, 8],
  duration: 8 / 30, fps: 30, loop: true, bg: 'dark',
  options: { seed: 23,
    teal: ['#0d3a46', '#1b7f8c', '#38d6d0', '#79d9df', '#e6fffb'],
    red: ['#4a0f1c', '#8c1f33', '#d84255', '#ff6b81', '#ffe3e8'] }
};

const W = 32;
function hash(a, b, c = 0) {
  let h = Math.imul(a ^ 0x9e3779b9, 0x85ebca6b) ^ Math.imul(b + 0x632be5ab, 0xc2b2ae35) ^ Math.imul(c + 0x165667b1, 0x27d4eb2f);
  h ^= h >>> 13; h = Math.imul(h, 0x27d4eb2f);
  return ((h ^ (h >>> 15)) >>> 0) / 4294967296;
}
const wrap = (x) => ((x % W) + W) % W;

export function draw(p, t, o) {
  const T = fit(o.teal, 5), Rd = fit(o.red, 5);
  const N = Math.round(meta.duration * meta.fps), s = Math.min(N - 1, Math.floor(t * meta.fps + 1e-6));
  const seed = o.seed, r = (k) => hash(s * 17 + seed, k);
  const shake = s >= 2 && s <= 4, breaking = s >= 5;
  const fade = breaking ? (s - 4) / 4 : 0;              // 0.25 .. 0.75 of the line breaks away

  // Speed dashes: short horizontal streaks above and below the core, sliding toward +x frame by frame.
  const dashes = breaking ? 3 : 6;
  for (let k = 0; k < dashes; k++) {
    const row = (k % 2 ? 1 : -1) * (3 + Math.floor(hash(k, 1, seed) * 4));
    const len = 3 + Math.floor(hash(k, 2, seed) * 7);
    const x0 = Math.floor(hash(k, 3, seed) * W) + s * 5;  // the dash runs forward as frames pass
    const color = k % 3 === 0 ? Rd[2] : T[k % 2 ? 2 : 3];
    for (let i = 0; i < len; i++) {
      const tail = i < 2 ? (s < 2 ? T[1] : Rd[1]) : color;  // dim tail, bright head: reads as motion
      if (breaking && hash(k, i, s) < fade) continue;
      p.px(wrap(x0 + i), row, tail);
    }
  }

  // Core: a 2 px bright line with a 1 px glow each side. Shake frames slide 4-8 px blocks up or down a pixel.
  for (let x = 0; x < W; x++) {
    let dy = 0;
    if (shake) {
      const block = Math.floor(wrap(x + Math.floor(r(1) * W)) / (4 + Math.floor(r(2) * 5)));
      const n = hash(block, 7, seed + s);
      dy = n < 0.25 ? -1 : n > 0.8 ? 1 : 0;
    }
    if (breaking) {
      // Fragments: whole 3-6 px pieces drop out, more each frame; survivors drift off the axis.
      const piece = Math.floor(x / (3 + Math.floor(hash(x >> 3, 9, seed) * 4)));
      if (hash(piece, 11, seed + s) < fade) continue;
      dy = hash(piece, 12, seed + s) < 0.5 ? -1 : 1;
      if (s >= 7 && hash(x, 13, seed) < 0.5) continue;
    }
    if (shake && hash(x, 14, seed + s) < 0.06) continue;  // dropped pixels
    const glow = breaking ? T[1] : T[2];
    p.px(x, dy - 2, glow); p.px(x, dy + 1, glow);
    p.px(x, dy - 1, breaking ? T[3] : T[4]); p.px(x, dy, s < 2 ? T[4] : T[3]);
  }

  // Channel split: a red copy of the core one row off (and a teal ghost the other way) on shake frames.
  if (shake || s === 1) {
    const side = r(20) < 0.5 ? -1 : 1, from = Math.floor(r(21) * W), len = 10 + Math.floor(r(22) * 18);
    for (let i = 0; i < len; i++) {
      const x = wrap(from + i);
      p.px(x, side * 3 - (side > 0 ? 1 : 0), Rd[2]);
      if (shake && i % 3 !== 0) p.px(x, -side * 3 - (side > 0 ? 0 : 1), T[3]);
    }
  }

  // A torn bright slice on some shake frames: a short block of the core thrown two rows off.
  if (shake && r(30) < 0.7) {
    const from = Math.floor(r(31) * W), len = 4 + Math.floor(r(32) * 6), row = r(33) < 0.5 ? -4 : 3;
    for (let i = 0; i < len; i++) p.px(wrap(from + i), row, r(34 + i) < 0.5 ? Rd[3] : T[4]);
  }

  // Loose pixels flicker around the line while it lives.
  for (let k = 0; k < (breaking ? 5 : 3); k++) {
    const x = Math.floor(r(40 + k) * W), y = Math.floor(r(50 + k) * 13) - 6;
    p.px(x, y, k % 2 ? Rd[3] : T[3]);
  }
}
