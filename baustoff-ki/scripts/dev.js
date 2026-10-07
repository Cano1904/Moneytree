// Startet API-Server (mit Neustart bei Änderungen) und Vite-Dev-Server gemeinsam – plattformunabhängig (auch Windows)
import { spawn } from 'node:child_process';

const procs = [
  spawn(process.execPath, ['--no-warnings', '--watch', 'server/index.js'], { stdio: 'inherit' }),
  spawn(process.platform === 'win32' ? 'npx.cmd' : 'npx', ['vite'], { stdio: 'inherit', shell: process.platform === 'win32' }),
];
const stop = () => { for (const p of procs) p.kill(); process.exit(); };
process.on('SIGINT', stop);
process.on('SIGTERM', stop);
for (const p of procs) p.on('exit', (code) => { if (code) stop(); });
