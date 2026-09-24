const crypto = require('crypto');
const os = require('os');
const readline = require('readline');
const tls = require('tls');

const PROTOCOL_VERSION = '1.0';
const ALLOWED_COMMANDS = new Set(['health-check', 'refresh-config', 'collect-diagnostics', 'list-status', 'ping-time', 'custom-cmd']);

function createToken(secret, deviceId, role, lifetimeSeconds = 600) {
  const payload = {
    deviceId,
    role,
    expiresAtUtc: new Date(Date.now() + lifetimeSeconds * 1000).toISOString(),
    protocolVersion: PROTOCOL_VERSION,
  };
  const payloadBytes = Buffer.from(JSON.stringify(payload));
  const signature = crypto.createHmac('sha256', secret).update(payloadBytes).digest();
  return `${base64UrlEncode(payloadBytes)}.${base64UrlEncode(signature)}`;
}

function base64UrlEncode(buffer) {
  return buffer.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}

class CanonicalTcpClient {
  constructor({ host, port, deviceId, role, authSecret, accessToken, allowUntrustedTls = false, timeoutMs = 2000, useLogin = false, loginUsername, loginPassword }) {
    this.host = host;
    this.port = port;
    this.deviceId = deviceId;
    this.role = role;
    this.authSecret = authSecret;
    this.accessToken = accessToken;
    this.allowUntrustedTls = allowUntrustedTls;
    this.timeoutMs = timeoutMs;
    this.useLogin = useLogin;
    this.loginUsername = loginUsername;
    this.loginPassword = loginPassword;
    this.socket = null;
    this.backlog = [];
    this.waiters = [];
    this.processedCommandIds = new Set();
  }

  async connect() {
    this.socket = await new Promise((resolve, reject) => {
      const socket = tls.connect({
        host: this.host,
        port: this.port,
        servername: 'localhost',
        rejectUnauthorized: !this.allowUntrustedTls,
      }, () => resolve(socket));
      socket.once('error', reject);
    });

    const lineReader = readline.createInterface({ input: this.socket });
    lineReader.on('line', (line) => {
      const envelope = JSON.parse(line);
      const waiterIndex = this.waiters.findIndex((waiter) => waiter.predicate(envelope));
      if (waiterIndex >= 0) {
        const [waiter] = this.waiters.splice(waiterIndex, 1);
        waiter.resolve(envelope);
        return;
      }

      this.backlog.push(envelope);
    });

    if (this.useLogin) {
      return this.login(this.loginUsername || this.deviceId, this.loginPassword || this.authSecret || '', this.role);
    }
    return this.authenticate();
  }

  close() {
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }
  }

  async authenticate() {
    const token = this.accessToken || createToken(this.authSecret || '', this.deviceId, this.role);
    const requestId = this.sendMessage('authenticate', { deviceId: this.deviceId, accessToken: token });
    const response = await this.waitFor((envelope) => envelope.requestId === requestId && ['authenticated', 'error'].includes(envelope.type));
    if (response.type === 'error') {
      throw new Error(response.payload.message);
    }
    return response;
  }

  async login(username, password, role = this.role) {
    const requestId = this.sendMessage('login', {
      deviceId: this.deviceId,
      username,
      password,
      role,
    });
    const response = await this.waitFor((envelope) => envelope.requestId === requestId && ['authenticated', 'error'].includes(envelope.type));
    if (response.type === 'error') {
      throw new Error(response.payload.message);
    }
    this.role = role;
    return response;
  }

  sendMessage(type, payload = null, { requestId, correlationId, deviceId, role } = {}) {
    if (!this.socket) {
      throw new Error('Client is not connected.');
    }

    const effectiveRequestId = requestId || crypto.randomUUID().replace(/-/g, '');
    const envelope = {
      protocolVersion: PROTOCOL_VERSION,
      requestId: effectiveRequestId,
      deviceId: deviceId || this.deviceId,
      clientId: deviceId || this.deviceId,
      role: role || this.role,
      type,
      correlationId: correlationId || null,
      timestampUtc: new Date().toISOString(),
      payload,
    };
    this.socket.write(`${JSON.stringify(envelope)}\n`);
    return effectiveRequestId;
  }

  async readMessage(timeoutMs = this.timeoutMs) {
    if (this.backlog.length > 0) {
      return this.backlog.shift();
    }

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        const index = this.waiters.indexOf(waiter);
        if (index >= 0) {
          this.waiters.splice(index, 1);
        }
        reject(new Error('Timed out waiting for a matching message.'));
      }, timeoutMs);

      const waiter = {
        predicate: () => true,
        resolve: (envelope) => {
          clearTimeout(timeout);
          resolve(envelope);
        },
      };
      this.waiters.push(waiter);
    });
  }

  async waitFor(predicate, timeoutMs = this.timeoutMs) {
    const backlogIndex = this.backlog.findIndex(predicate);
    if (backlogIndex >= 0) {
      return this.backlog.splice(backlogIndex, 1)[0];
    }

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        const index = this.waiters.indexOf(waiter);
        if (index >= 0) {
          this.waiters.splice(index, 1);
        }
        reject(new Error('Timed out waiting for a matching message.'));
      }, timeoutMs);

      const waiter = {
        predicate,
        resolve: (envelope) => {
          clearTimeout(timeout);
          resolve(envelope);
        },
      };
      this.waiters.push(waiter);
    });
  }

  async sendAdminCommand(commandName, { commandId, targetMode = 'all', targetDeviceIds = [], timeoutMs = 1000 } = {}) {
    const requestId = this.sendMessage('admin-command', {
      commandId: commandId || crypto.randomUUID().replace(/-/g, ''),
      commandName,
      targetMode,
      targetDeviceIds,
      timeoutMs,
    });
    return this.waitFor((envelope) => envelope.requestId === requestId && ['admin-command.accepted', 'error'].includes(envelope.type), timeoutMs + 1000);
  }

  async processNextCommand() {
    const command = await this.waitFor((envelope) => envelope.type === 'admin-command', 5000);
    const { commandId, commandName } = command.payload;
    this.sendMessage('command-ack', { commandId, status: 'accepted' }, { correlationId: commandId });

    const duplicate = this.processedCommandIds.has(commandId);
    if (!duplicate) {
      this.processedCommandIds.add(commandId);
    }

    const resultPayload = {
      commandId,
      commandName,
      status: 'completed',
      success: true,
      duplicate,
      result: this.executeCommand(commandName),
    };
    this.sendMessage('command-result', resultPayload, { correlationId: commandId });
    return { command, resultPayload };
  }

  executeCommand(commandName) {
    if (!ALLOWED_COMMANDS.has(commandName)) {
      throw new Error(`Command '${commandName}' is not allowlisted.`);
    }

    if (commandName === 'health-check') {
      return { status: 'ok', deviceId: this.deviceId, observedAtUtc: new Date().toISOString() };
    }

    if (commandName === 'refresh-config') {
      return { status: 'refreshed', deviceId: this.deviceId, appliedAtUtc: new Date().toISOString() };
    }

    if (commandName === 'collect-diagnostics') {
      return { status: 'collected', deviceId: this.deviceId, diagnostics: { platform: os.platform(), pid: process.pid } };
    }

    if (commandName === 'list-status') {
      return {
        status: 'ready',
        deviceId: this.deviceId,
        role: this.role,
        platform: os.platform(),
        observedAtUtc: new Date().toISOString(),
      };
    }

    if (commandName === 'ping-time') {
      return { status: 'pong', deviceId: this.deviceId, observedAtUtc: new Date().toISOString() };
    }

    if (commandName === 'custom-cmd') {
      return {
        status: 'acknowledged',
        executed: false,
        deviceId: this.deviceId,
        note: 'allowlisted stub only; no shell or code execution',
        observedAtUtc: new Date().toISOString(),
      };
    }

    throw new Error(`Command '${commandName}' is not implemented.`);
  }
}

function parseArgs(argv) {
  const result = { host: '127.0.0.1', targetDeviceIds: [] };
  for (let index = 2; index < argv.length; index += 1) {
    const key = argv[index];
    const value = argv[index + 1];
    switch (key) {
      case '--host':
        result.host = value;
        index += 1;
        break;
      case '--port':
        result.port = Number(value);
        index += 1;
        break;
      case '--device-id':
        result.deviceId = value;
        index += 1;
        break;
      case '--role':
        result.role = value;
        index += 1;
        break;
      case '--auth-secret':
        result.authSecret = value;
        index += 1;
        break;
      case '--access-token':
        result.accessToken = value;
        index += 1;
        break;
      case '--send-command':
        result.sendCommand = value;
        index += 1;
        break;
      case '--target-mode':
        result.targetMode = value;
        index += 1;
        break;
      case '--target-device-id':
        result.targetDeviceIds.push(value);
        index += 1;
        break;
      case '--timeout-ms':
        result.timeoutMs = Number(value);
        index += 1;
        break;
      case '--allow-untrusted':
        result.allowUntrustedTls = true;
        break;
      case '--login':
        result.useLogin = true;
        break;
      case '--login-user':
        result.loginUsername = value;
        index += 1;
        break;
      case '--login-password':
        result.loginPassword = value;
        index += 1;
        break;
      default:
        break;
    }
  }
  return result;
}

async function main() {
  const args = parseArgs(process.argv);
  const client = new CanonicalTcpClient({
    host: args.host,
    port: args.port,
    deviceId: args.deviceId,
    role: args.role,
    authSecret: args.authSecret || process.env.SOCKET_PLAYGROUND_AUTH_SECRET,
    accessToken: args.accessToken,
    allowUntrustedTls: !!args.allowUntrustedTls,
    timeoutMs: args.timeoutMs || 2000,
    useLogin: !!args.useLogin,
    loginUsername: args.loginUsername,
    loginPassword: args.loginPassword,
  });

  try {
    const authenticated = await client.connect();
    console.log(JSON.stringify(authenticated));

    if (args.sendCommand) {
      const accepted = await client.sendAdminCommand(args.sendCommand, {
        targetMode: args.targetMode || 'all',
        targetDeviceIds: args.targetDeviceIds,
        timeoutMs: args.timeoutMs || 1000,
      });
      console.log(JSON.stringify(accepted));
      const commandId = accepted.payload.commandId;
      while (true) {
        const envelope = await client.readMessage(args.timeoutMs || 2000);
        console.log(JSON.stringify(envelope));
        if (envelope.type === 'command-summary' && envelope.correlationId === commandId) {
          break;
        }
      }
    } else {
      console.log('Connected; waiting for allowlisted admin-command messages. Press Ctrl+C to exit.');
      while (true) {
        const { command, resultPayload } = await client.processNextCommand();
        console.log(JSON.stringify(command));
        console.log(JSON.stringify(resultPayload));
      }
    }
  } finally {
    client.close();
  }
}

if (require.main === module) {
  main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
}

module.exports = {
  CanonicalTcpClient,
  createToken,
};
