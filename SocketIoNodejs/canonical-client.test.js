const { after, test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const path = require('node:path');

const { CanonicalTcpClient, createToken } = require('./canonical-client');

const AUTH_SECRET = 'node-cross-language-secret';
const ADMIN_USER = 'admin';
const ADMIN_PASSWORD = 'admin-pass';
const repoRoot = path.resolve(__dirname, '..');

/** Cold CI runners can exceed 20s for first compile+start; default 120s, overridable. */
const STARTUP_TIMEOUT_MS = Number(process.env.DOTNET_SERVER_STARTUP_TIMEOUT_MS || 120_000);

const activeServers = new Set();

/**
 * Terminate a spawned dotnet server and its process group (Linux) so orphans
 * cannot stall the CI job after a startup timeout or failed test.
 */
function killServerTree(server) {
  if (!server) {
    return;
  }
  if (server.exitCode != null || server.signalCode != null) {
    activeServers.delete(server);
    return;
  }

  const pid = server.pid;
  try {
    if (pid && process.platform !== 'win32') {
      // Negative PID = process group when spawned with detached:true.
      try {
        process.kill(-pid, 'SIGTERM');
      } catch {
        // Group may already be gone or not a group leader.
      }
    }
  } catch {
    // ignore
  }

  try {
    if (!server.killed) {
      server.kill('SIGTERM');
    }
  } catch {
    // ignore
  }

  // Escalate if still alive shortly after.
  const escalate = setTimeout(() => {
    try {
      if (pid && process.platform !== 'win32') {
        try {
          process.kill(-pid, 'SIGKILL');
        } catch {
          // ignore
        }
      }
      if (server.exitCode == null && server.signalCode == null) {
        try {
          server.kill('SIGKILL');
        } catch {
          // ignore
        }
      }
    } finally {
      activeServers.delete(server);
    }
  }, 2000);
  if (typeof escalate.unref === 'function') {
    escalate.unref();
  }

  activeServers.delete(server);
}

after(() => {
  for (const server of [...activeServers]) {
    killServerTree(server);
  }
  activeServers.clear();
});

let buildPromise;

function ensureServerBuilt() {
  if (!buildPromise) {
    buildPromise = new Promise((resolve, reject) => {
      const build = spawn(
        'dotnet',
        ['build', 'SocketServerNetCore/SocketServerNetCore.csproj', '-nologo', '-v', 'q'],
        {
          cwd: repoRoot,
          stdio: ['ignore', 'pipe', 'pipe'],
        }
      );
      let output = '';
      build.stdout.on('data', (chunk) => {
        output += chunk.toString();
      });
      build.stderr.on('data', (chunk) => {
        output += chunk.toString();
      });
      build.once('error', reject);
      build.once('close', (code) => {
        if (code === 0) {
          resolve();
        } else {
          reject(new Error(`dotnet build SocketServerNetCore failed (${code}).\n${output}`));
        }
      });
    });
  }
  return buildPromise;
}

async function startDotnetServer() {
  await ensureServerBuilt();

  return new Promise((resolve, reject) => {
    const server = spawn(
      'dotnet',
      [
        'run',
        '--project',
        'SocketServerNetCore',
        '--no-build',
        '--',
        'server',
        '--port',
        '0',
        '--auth-secret',
        AUTH_SECRET,
        '--admin-user',
        ADMIN_USER,
        '--admin-password',
        ADMIN_PASSWORD,
      ],
      {
        cwd: repoRoot,
        stdio: ['ignore', 'pipe', 'pipe'],
        // New process group so we can kill the whole tree on timeout/teardown (Linux).
        detached: process.platform !== 'win32',
      }
    );

    activeServers.add(server);

    let settled = false;
    let output = '';

    const settleFail = (err) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      killServerTree(server);
      reject(err);
    };

    const settleOk = (port) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      resolve({ server, port });
    };

    const onData = (chunk) => {
      output += chunk.toString();
      const match = output.match(/127\.0\.0\.1:(\d+)/);
      if (match) {
        settleOk(Number(match[1]));
      }
    };

    server.stdout.on('data', onData);
    server.stderr.on('data', onData);
    server.once('error', settleFail);
    server.once('exit', (code, signal) => {
      if (!settled) {
        settleFail(
          new Error(
            `dotnet server exited before ready (code=${code}, signal=${signal}).\n${output}`
          )
        );
      }
    });

    const timer = setTimeout(() => {
      settleFail(
        new Error(
          `Timed out waiting for dotnet server after ${STARTUP_TIMEOUT_MS}ms.\n${output}`
        )
      );
    }, STARTUP_TIMEOUT_MS);
  });
}

test('node client creates signed token', () => {
  const token = createToken(AUTH_SECRET, 'node-device', 'client', 30);
  assert.equal(token.split('.').length, 2);
});

test('node client interoperates with dotnet server', async () => {
  const { server, port } = await startDotnetServer();
  after(() => {
    killServerTree(server);
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
    killServerTree(server);
  }
});

test('node client logs in as admin after TLS', async () => {
  const { server, port } = await startDotnetServer();
  after(() => {
    killServerTree(server);
  });

  const admin = new CanonicalTcpClient({
    host: '127.0.0.1',
    port,
    deviceId: 'node-login-admin',
    role: 'admin',
    allowUntrustedTls: true,
    useLogin: true,
    loginUsername: ADMIN_USER,
    loginPassword: ADMIN_PASSWORD,
  });
  const agent = new CanonicalTcpClient({
    host: '127.0.0.1',
    port,
    deviceId: 'node-login-agent',
    role: 'client',
    allowUntrustedTls: true,
    useLogin: true,
    loginUsername: 'node-login-agent',
    loginPassword: AUTH_SECRET,
  });

  try {
    const authenticatedAdmin = await admin.connect();
    const authenticatedAgent = await agent.connect();
    assert.equal(authenticatedAdmin.type, 'authenticated');
    assert.equal(authenticatedAdmin.payload.role, 'admin');
    assert.equal(authenticatedAdmin.payload.method, 'login');
    assert.equal(authenticatedAgent.payload.role, 'client');
    assert.ok(admin.socket.encrypted);

    const accepted = await admin.sendAdminCommand('ping-time', {
      commandId: 'node-login-ping',
      targetMode: 'devices',
      targetDeviceIds: ['node-login-agent'],
      timeoutMs: 1000,
    });
    assert.equal(accepted.type, 'admin-command.accepted');

    const { resultPayload } = await agent.processNextCommand();
    assert.equal(resultPayload.result.status, 'pong');
    const summary = await admin.waitFor((envelope) => envelope.type === 'command-summary' && envelope.correlationId === 'node-login-ping');
    assert.deepEqual(summary.payload.completedDeviceIds, ['node-login-agent']);
  } finally {
    admin.close();
    agent.close();
    killServerTree(server);
  }
});
