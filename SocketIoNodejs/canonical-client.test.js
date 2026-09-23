const { after, test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const path = require('node:path');

const { CanonicalTcpClient, createToken } = require('./canonical-client');

const AUTH_SECRET = 'node-cross-language-secret';
const repoRoot = path.resolve(__dirname, '..');

function startDotnetServer() {
  return new Promise((resolve, reject) => {
    const server = spawn('dotnet', [
      'run',
      '--project',
      'SocketServerNetCore',
      '--',
      'server',
      '--port',
      '0',
      '--auth-secret',
      AUTH_SECRET,
    ], {
      cwd: repoRoot,
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    let output = '';
    const onData = (chunk) => {
      output += chunk.toString();
      const match = output.match(/127\.0\.0\.1:(\d+)/);
      if (match) {
        resolve({ server, port: Number(match[1]) });
      }
    };

    server.stdout.on('data', onData);
    server.stderr.on('data', onData);
    server.once('error', reject);
    setTimeout(() => reject(new Error(`Timed out waiting for dotnet server.\n${output}`)), 20000);
  });
}

test('node client creates signed token', () => {
  const token = createToken(AUTH_SECRET, 'node-device', 'client', 30);
  assert.equal(token.split('.').length, 2);
});

test('node client interoperates with dotnet server', async () => {
  const { server, port } = await startDotnetServer();
  after(() => {
    server.kill('SIGTERM');
  });

  const admin = new CanonicalTcpClient({
    host: '127.0.0.1',
    port,
    deviceId: 'node-admin',
    role: 'admin',
    authSecret: AUTH_SECRET,
    allowUntrustedTls: true,
  });
  const agent = new CanonicalTcpClient({
    host: '127.0.0.1',
    port,
    deviceId: 'node-agent',
    role: 'client',
    authSecret: AUTH_SECRET,
    allowUntrustedTls: true,
  });

  try {
    const authenticatedAdmin = await admin.connect();
    const authenticatedAgent = await agent.connect();
    assert.equal(authenticatedAdmin.type, 'authenticated');
    assert.equal(authenticatedAgent.type, 'authenticated');

    const accepted = await admin.sendAdminCommand('health-check', {
      commandId: 'node-health-check',
      targetMode: 'devices',
      targetDeviceIds: ['node-agent'],
      timeoutMs: 1000,
    });
    assert.equal(accepted.type, 'admin-command.accepted');

    const { command, resultPayload } = await agent.processNextCommand();
    assert.equal(command.payload.commandName, 'health-check');
    assert.equal(resultPayload.result.status, 'ok');

    const ack = await admin.waitFor((envelope) => envelope.type === 'command-ack' && envelope.correlationId === 'node-health-check');
    const result = await admin.waitFor((envelope) => envelope.type === 'command-result' && envelope.correlationId === 'node-health-check');
    const summary = await admin.waitFor((envelope) => envelope.type === 'command-summary' && envelope.correlationId === 'node-health-check');

    assert.equal(ack.deviceId, 'node-agent');
    assert.equal(result.payload.result.status, 'ok');
    assert.deepEqual(summary.payload.completedDeviceIds, ['node-agent']);
    assert.equal(summary.payload.timedOut, false);
  } finally {
    admin.close();
    agent.close();
    server.kill('SIGTERM');
  }
});
