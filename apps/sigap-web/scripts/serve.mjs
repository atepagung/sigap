import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import { APP_ROOT, generateRemoteSources, readRemoteIdentity } from './remote-identity.mjs';

// Port hanya ada di remote-identity.json, tidak di angular.json — jalankan lewat `npm start`,
// bukan `ng serve` langsung.
generateRemoteSources();
const { port } = readRemoteIdentity();
const ngBin = createRequire(import.meta.url).resolve('@angular/cli/bin/ng.js');

const child = spawn(
  process.execPath,
  [ngBin, 'serve', '--port', String(port), ...process.argv.slice(2)],
  { cwd: APP_ROOT, stdio: 'inherit' },
);
child.on('exit', (code) => process.exit(code ?? 1));
