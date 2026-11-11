// Lock-on reticle: four arcs turning round the monster the player is locked onto, with a red RGB-split echo,
// short ticks between the arcs, a torn arc now and then and loose pixels. Looks like the glitch marker's cousin.
// Authored on the player art's grid (18 px per unit); (0, 0) is the center of the locked monster.
import { fit } from '../core.mjs';

export const meta = {
  name: 'Lock-on', size: [32, 32], origin: [16, 16],
  duration: 0.8, fps: 30, loop: true, bg: 'dark',
  options: { seed: 9,
    ready: ['#0d3a46', '#1b7f8c', '#38d6d0', '#79d9df', '#e6fffb'],
    stop: ['#4a0f1c', '#8c1f33', '#d84255', '#ff6b81', '#ffe3e8'] }
};

// Stable per-frame noise: the same frame index always gives the same value, so the loop has no seam.
function hash(a, b) {
  let h = Math.imul(a ^ 0x9e3779b9, 0x85ebca6b) ^ Math.imul(b + 0x632be5ab, 0xc2b2ae35);
  h ^= h >>> 13; h = Math.imul(h, 0x27d4eb2f);
  return ((h ^ (h >>> 15)) >>> 0) / 4294967296;
}

function arc(p, cx, cy, radius, from, span, color) {
  const steps = Math.ceil(span / 3);
  for (let i = 0; i <= steps; i++) {
    const a = (from + (span * i) / steps) * Math.PI / 180;
    p.px(cx + Math.round(Math.cos(a) * radius), cy + Math.round(Math.sin(a) * radius), color);
  }
}

export function draw(p, t, o) {
  const C = fit(o.ready, 5), A = fit(o.stop, 5);
  const N = Math.round(meta.duration * meta.fps), s = Math.floor(t * meta.fps + 1e-6) % N, u = s / N;
  const r = (k) => hash(s * 7 + o.seed, k);
  // Four-fold symmetry: a quarter turn per loop closes the seam.
  const turn = u * 90;
  // The ring tightens by a pixel for a beat twice a loop.
  const radius = s % 12 < 3 ? 9 : 10;
  // A faint disc keeps the reticle readable over busy backgrounds.
  p.each(-radius, -radius, radius, radius, (x, y, sx, sy) => { if (x * x + y * y <= radius * radius) p.blendPx(sx, sy, C[1], 0.1); });

  const torn = r(3) < 0.2 ? Math.floor(r(4) * 4) : -1, tear = r(5) < 0.5 ? -2 : 2;
  const split = r(6) < 0.45 ? (r(7) < 0.5 ? -1 : 1) : 0;
  for (let k = 0; k < 4; k++) {
    const dx = k === torn ? tear : 0, from = turn + k * 90 - 28;
    if (split) arc(p, dx + split, 0, radius, from, 56, A[2]);
    arc(p, dx, 0, radius, from, 56, C[3]);
    // Bright tip at the leading end of each arc.
    const a = (from + 56) * Math.PI / 180;
    p.px(dx + Math.round(Math.cos(a) * radius), Math.round(Math.sin(a) * radius), C[4]);
  }
  // Ticks between the arcs, pointing in.
  for (let k = 0; k < 4; k++) {
    const a = (turn + 45 + k * 90) * Math.PI / 180;
    for (const d of [radius + 2, radius + 3]) p.px(Math.round(Math.cos(a) * d), Math.round(Math.sin(a) * d), k % 2 ? A[2] : C[2]);
  }
  // Loose pixels flicker round the ring.
  for (let k = 0; k < 3; k++) {
    const a = r(20 + k) * Math.PI * 2, d = radius + 3 + r(30 + k) * 2;
    p.blend(Math.round(Math.cos(a) * d), Math.round(Math.sin(a) * d), k % 2 ? A[2] : C[3], 0.85);
  }
}
