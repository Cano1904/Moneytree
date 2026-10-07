// Einstiegspunkt: REST-API + Auslieferung des gebauten Frontends
import express from 'express';
import path from 'node:path';
import fs from 'node:fs';
import './db.js';
import projects from './routes/projects.js';
import lv from './routes/lv.js';
import suppliers from './routes/suppliers.js';
import offers from './routes/offers.js';
import misc from './routes/misc.js';
import { HttpError } from './util.js';

export const app = express();
// Optionaler Zugangsschutz (HTTP Basic Auth) für den Betrieb im Netzwerk: APP_PASSWORD setzen
if (process.env.APP_PASSWORD) {
  app.use((req, res, next) => {
    const [, b64] = (req.get('authorization') || '').split(' ');
    const pass = b64 ? Buffer.from(b64, 'base64').toString().split(':').slice(1).join(':') : null;
    if (pass === process.env.APP_PASSWORD) return next();
    res.set('WWW-Authenticate', 'Basic realm="Baustoff-KI"').status(401).send('Anmeldung erforderlich');
  });
}
app.use(express.json({ limit: '10mb' }));
app.use('/api', projects, lv, suppliers, offers, misc);
app.use('/api', (req, res) => res.status(404).json({ error: 'Unbekannter API-Endpunkt' }));

const dist = path.resolve(import.meta.dirname, '..', 'dist');
if (fs.existsSync(dist)) {
  app.use(express.static(dist));
  app.get(/^\/(?!api).*/, (req, res) => res.sendFile(path.join(dist, 'index.html')));
}

// Fehler sauber an den Client melden (keine Stacktraces)
app.use((err, req, res, _next) => {
  const status = err instanceof HttpError ? err.status : err.type === 'entity.too.large' || err.code === 'LIMIT_FILE_SIZE' ? 413 : 500;
  if (status === 500) console.error(err);
  res.status(status).json({ error: status === 500 ? `Interner Fehler: ${err.message}` : err.message });
});

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(import.meta.filename)) {
  const port = Number(process.env.PORT) || 3000;
  app.listen(port, () => console.log(`Baustoff-KI läuft auf http://localhost:${port}`));
}
