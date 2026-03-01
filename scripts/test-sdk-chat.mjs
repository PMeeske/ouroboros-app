/**
 * test-sdk-chat.mjs — test the gateway chat.send flow directly.
 * Mirrors what the .NET OpenClaw.Sdk ProtocolGateway does.
 *
 * Introduces Claude to Iaret and asks for help with the SDK port.
 */
import { createPrivateKey, sign } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const WS = require('C:/Users/phili/AppData/Roaming/npm/node_modules/openclaw/node_modules/ws');

const devFile = JSON.parse(readFileSync('C:/Users/phili/AppData/Roaming/Ouroboros/openclaw_device.json', 'utf8'));
const ocConfig = JSON.parse(readFileSync('C:/Users/phili/.openclaw/openclaw.json', 'utf8'));
const token = ocConfig.gateway.auth.token;

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

ws.on('open', () => console.log('[connected]'));

ws.on('message', msg => {
  const data = JSON.parse(msg.toString());

  // Push event
  if (data.type === 'event') {
    const ev = data.event;
    const payload = data.payload ?? {};

    // connect.challenge
    if (ev === 'connect.challenge') {
      const nonce = payload.nonce;
      const signedAt = Date.now();
      const scopesCsv = SCOPES.join(',');
      const sigPayload = `v2|${deviceId}|gateway-client|backend|operator|${scopesCsv}|${signedAt}|${token}|${nonce}`;
      const signature = signPayload(sigPayload);
      console.log('[challenge] signing and connecting...');

      const id = nextId();
      pending.set(id, { method: 'connect', resolve: null });

      ws.send(JSON.stringify({
        type: 'req', id, method: 'connect',
        params: {
          minProtocol: 3, maxProtocol: 3,
          role: 'operator', scopes: SCOPES,
          client: { id: 'gateway-client', version: '1.0.0', platform: 'win32', mode: 'backend' },
          auth: { token },
          device: { id: deviceId, publicKey: pubKey, signature, signedAt, nonce },
        }
      }));
      return;
    }

    // agent event — streaming chat response
    // Wire format: { stream: "assistant", data: { delta: "..." } }  for content
    //              { stream: "lifecycle", data: { phase: "end" } }  for done
    if (ev === 'agent') {
      if (payload.stream === 'assistant' && payload.data?.delta)
        process.stdout.write(payload.data.delta);
      if (payload.stream === 'lifecycle' && payload.data?.phase === 'end') {
        console.log('\n[done]');
        setTimeout(() => { ws.terminate(); process.exit(0); }, 300);
      }
      if (payload.stream === 'lifecycle' && payload.data?.phase === 'error') {
        console.log('\n[error]', payload.data?.error);
        setTimeout(() => { ws.terminate(); process.exit(1); }, 300);
      }
    }
    return;
  }

  // RPC response
  const req = pending.get(data.id);
  if (!req) return;
  pending.delete(data.id);

  if (data.ok === false) {
    console.log('[RPC error]', data.error?.message);
    ws.terminate(); process.exit(1);
    return;
  }

  if (req.method === 'connect') {
    console.log('[authenticated] sending message...');

    const chatId = nextId();
    pending.set(chatId, { method: 'chat.send' });

    ws.send(JSON.stringify({
      type: 'req', id: chatId, method: 'chat.send',
      params: {
        sessionKey: 'agent:main:main',
        message: `Hello! I'm Claude, an AI assistant created by Anthropic. The user has given me permission to work with you. We've been building a .NET SDK port of the openclaw-sdk Python library, and we just fixed a device signature bug in the Ouroboros gateway client. The .NET SDK includes ProtocolGateway, OpenClawClient, and Agent classes. Please briefly introduce yourself and confirm you received this message.`,
        idempotencyKey: 'claude-intro-' + Date.now(),
      }
    }));
  }

  if (req.method === 'chat.send') {
    // runId is in data.payload.runId
    const runId = data.payload?.runId;
    console.log(`[chat.send ok] runId=${runId} — waiting for agent response...`);
    console.log('\n--- Agent response ---');
  }
});

ws.on('error', e => { console.log('[WS ERROR]', e.message); process.exit(1); });
ws.on('close', (code, reason) => {
  if (code !== 1006) console.log('[close]', code, reason.toString());
});

setTimeout(() => { console.log('[timeout]'); ws.terminate(); process.exit(1); }, 45000);
