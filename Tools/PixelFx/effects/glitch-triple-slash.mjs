// Three authored VFX cues only; does not define gameplay damage or attack timing.
import { hex, fit, Rng } from '../core.mjs';

export const meta = {
  name: 'Glitch triple slash', size: [240, 192], origin: [96, 96],
  duration: 1.35, fps: 60, loop: false, bg: 'dark',
  options: { seed: 7, palette: ['#123f50', '#159eaa', '#32e7da', '#dffff5'], accent: '#ff365c' }
};

// Right-facing down-cut, returning up-cut, wider finishing cut.
export const cues = [
  { start: 0.04, duration: 0.32, tilt: 0.62, reverse: false, radius: 65 },
  { start: 0.32, duration: 0.32, tilt: -0.62, reverse: true, radius: 69 },
  { start: 0.64, duration: 0.48, tilt: 0.12, reverse: false, radius: 87 }
];

function point(a, radius, tilt, dx = 0, dy = 0) {
  const x = Math.cos(a) * radius, y = Math.sin(a) * radius * 0.73;
  return [12 + x * Math.cos(tilt) - y * Math.sin(tilt) + dx,
    x * Math.sin(tilt) + y * Math.cos(tilt) + dy];
}

function crescent(p, cue, head, tail, radius, thickness, color, alpha, dx = 0, dy = 0) {
  const outer = [], inner = [];
  for (let j = 0; j <= 44; j++) {
    const u = j / 44, angle = tail + (head - tail) * u;
    const width = thickness * Math.sin(Math.PI * u) ** 0.7;
    outer.push(point(angle, radius, cue.tilt, dx, dy));
    inner.push(point(angle, radius - width, cue.tilt, dx, dy));
  }
  p.poly([...outer, ...inner.reverse()], color, alpha);
}

export function draw(p, t, o) {
  const C = fit(o.palette, 4), red = hex(o.accent), white = hex('#ffffff');
  cues.forEach((cue, index) => {
    if (o.singleStrike !== undefined && o.singleStrike !== index) return;
    const age = t - cue.start;
    if (age < 0 || age >= cue.duration) return;
    const u = age / cue.duration, heavy = index === 2;
    const sweep = Math.min(1, age / (heavy ? 0.11 : 0.085));
    const direction = cue.reverse ? -1 : 1;
    const head = direction * (-1.65 + 3.3 * (1 - (1 - sweep) ** 2));
    const trail = Math.min(2.9, sweep * 3.3) * Math.max(0.2, 1 - Math.max(0, u - 0.38));
    const tail = head - direction * trail;
    const fade = Math.max(0, 1 - Math.max(0, u - 0.22) / 0.78);
    const radius = cue.radius + u * 7;
    const jitter = Math.floor(age * 60) % 4 < 2 ? 4 : -3;
    crescent(p, cue, head, tail, radius + 3, heavy ? 15 : 10, red, fade * 0.72, jitter, -2);
    crescent(p, cue, head, tail, radius - 5, heavy ? 19 : 12, C[0], fade * 0.65, -3, 2);
    crescent(p, cue, head, tail, radius, heavy ? 14 : 9, C[2], fade);
    crescent(p, cue, head, tail, radius - 1, heavy ? 5 : 3, C[3], fade);
    const tip = point(head, radius - 2, cue.tilt);
    p.glow(tip[0], tip[1], heavy ? 20 : 12, C[2], 0.3 * fade);

    const rng = new Rng(o.seed + index * 197);
    for (let i = 0; i < (heavy ? 36 : 22); i++) {
      const a = rng.range(-1.8, 1.8), speed = rng.range(12, 43);
      const pos = point(a, cue.radius + age * speed, cue.tilt);
      const delay = rng.range(0.015, 0.1);
      if (age < delay) continue;
      const life = Math.max(0, 1 - (age - delay) / (cue.duration - delay));
      const width = rng.range(2, heavy ? 12 : 8) * life;
      p.rect(pos[0] + jitter * life, pos[1], Math.max(1, width), i % 5 === 0 ? 3 : 1,
        i % 3 === 0 ? red : C[2], life * life);
      if (i % 4 === 0) p.line(pos[0] - 4 * life, pos[1] + 3, pos[0] + width, pos[1] + 3, red, life * 0.55);
    }
    // Small contact-like light at the finish, part of this preview, not a hit event.
    if (heavy && age >= 0.09 && age < 0.17) {
      const k = 1 - (age - 0.09) / 0.08;
      p.beam(80 - 16 * k, -6, 80 + 16 * k, -6, 2, white, k);
      p.beam(80, -6 - 13 * k, 80, -6 + 13 * k, 2, C[3], k);
      p.glow(80, -6, 24 * k, red, 0.22 * k);
    }
  });
}
