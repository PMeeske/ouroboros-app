/**
 * list-sessions.mjs — query gateway for available agents and sessions
 */
import { createPrivateKey, sign } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const WS = require('C:/Users/phili/AppData/Roaming/npm/node_modules/openclaw/node_modules/ws');

const devFile = JSON.parse(readFileSync('C:/Users/phili/AppData/Roaming/Ouroboros/openclaw_device.json', 'utf8'));
const token = JSON.parse(readFileSync('C:/Users/phili/.openclaw/openclaw.json', 'utf8')).gateway.auth.token;
const deviceId = devFile.deviceId;
const seedBytes = Buffer.from(devFile.seed, 'base64');
const header = Buffer.from('302e020100300506032b657004220420', 'hex');
const privKey = createPrivateKey({ key: Buffer.concat([header, seedBytes]), format: 'der', type: 'pkcs8' });
const pubKey = 'frHUruaaUWn7_EUiOZVlXYwGgq58n_0GFNNTtXZ9UkY';
function b64url(buf) { return buf.toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=/g,''); }
function signPayload(p) { return b64url(sign(null, Buffer.from(p, 'utf8'), privKey)); }

const SCOPES = ['operator.read', 'operator.write', 'operator.admin'];
let reqId = 1;
const nextId = () => `req_${reqId++}`;
const pending = new Map();

const ws = new WS('ws://127.0.0.1:18789');

ws.on('message', msg => {
  const data = JSON.parse(msg.toString());

  if (data.type === 'event' && data.event === 'connect.challenge') {
    const nonce = data.payload.nonce;
    const signedAt = Date.now();
    const scopesCsv = SCOPES.join(',');
    const sig = signPayload(`v2|${deviceId}|gateway-client|backend|operator|${scopesCsv}|${signedAt}|${token}|${nonce}`);
    const id = nextId();
    pending.set(id, 'connect');
    ws.send(JSON.stringify({
      type: 'req', id, method: 'connect',
      params: {
        minProtocol: 3, maxProtocol: 3, role: 'operator', scopes: SCOPES,
        client: { id: 'gateway-client', version: '1.0.0', platform: 'win32', mode: 'backend' },
        auth: { token },
        device: { id: deviceId, publicKey: pubKey, signature: sig, signedAt, nonce },
      }
    }));
    return;
  }

  const step = pending.get(data.id);
  if (!step) return;
  pending.delete(data.id);

  if (data.ok === false) { console.log('[error]', data.error?.message); ws.terminate(); process.exit(1); return; }

  if (step === 'connect') {
    console.log('[connected and authenticated]');
    // Query sessions and agents
    const s1 = nextId(); pending.set(s1, 'sessions.list');
    ws.send(JSON.stringify({ type: 'req', id: s1, method: 'sessions.list', params: {} }));
    const s2 = nextId(); pending.set(s2, 'agents.list');
    ws.send(JSON.stringify({ type: 'req', id: s2, method: 'agents.list', params: {} }));
    return;
  }

  if (step === 'sessions.list') {
    console.log('\n=== SESSIONS ===');
    console.log(JSON.stringify(data.result ?? data, null, 2));
  }
  if (step === 'agents.list') {
    console.log('\n=== AGENTS ===');
    console.log(JSON.stringify(data.result ?? data, null, 2));
    setTimeout(() => { ws.terminate(); process.exit(0); }, 300);
  }
});

ws.on('error', e => { console.log('[WS ERROR]', e.message); process.exit(1); });
ws.on('close', () => process.exit(0));
setTimeout(() => { ws.terminate(); process.exit(1); }, 10000);
