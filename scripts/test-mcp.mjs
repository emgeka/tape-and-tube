// SPDX-License-Identifier: GPL-3.0-only
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';

const build = readFileSync('runtime/current-app.txt', 'utf8').trim();
const executable = path.resolve(build, 'TapeTube.Cli.exe');
const folder = path.resolve('artifacts', 'mcp-smoke');
mkdirSync(folder, { recursive: true });
const input = path.join(folder, 'input.png');
const preset = path.join(folder, 'crop.json');
const output = path.join(folder, 'result.png');
const hevc = path.join(folder, 'result-hevc.mp4');
const cancelled = path.join(folder, 'cancelled.gif');
const fixture = spawnSync(process.env.TAPETUBE_FFMPEG || 'ffmpeg', ['-v', 'error', '-nostdin', '-y',
  '-f', 'lavfi', '-i', 'testsrc2=size=96x64:rate=12', '-frames:v', '1', '-update', '1', input],
{ windowsHide: true, encoding: 'utf8' });
assert.equal(fixture.status, 0, fixture.stderr);
writeFileSync(preset, JSON.stringify({ version: 3, downscale: { enabled: false }, ntsc: { enabled: false },
  shader: { enabled: false }, crop: { ratio: '1:1', x: 1 } }));

const server = spawn(executable, ['mcp'], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
const exited = new Promise((resolve, reject) => {
  server.once('error', reject);
  server.once('exit', (code, signal) => resolve({ code, signal }));
});
const pending = new Map();
const notifications = [];
let stderr = '';
server.stderr.on('data', chunk => { stderr += chunk; });
const reader = readline.createInterface({ input: server.stdout });
reader.on('line', line => {
  const message = JSON.parse(line);
  assert.equal(message.jsonrpc, '2.0');
  const callback = pending.get(message.id);
  if (callback) {
    pending.delete(message.id);
    callback(message);
  } else {
    notifications.push(message);
  }
});
const timer = setTimeout(() => {
  server.kill();
  throw new Error('MCP smoke test timed out: ' + stderr);
}, 30000);

function send(message) {
  server.stdin.write(JSON.stringify({ jsonrpc: '2.0', ...message }) + '\n');
}
function request(id, method, params) {
  return new Promise(resolve => {
    pending.set(id, resolve);
    send({ id, method, params });
  });
}

try {
  const hello = await request(1, 'initialize', {
    protocolVersion: '2025-11-25', capabilities: {}, clientInfo: { name: 'test-mcp', version: '1' }
  });
  assert.equal(hello.result.protocolVersion, '2025-11-25');
  assert.equal(hello.result.capabilities.tools.listChanged, false);
  send({ method: 'notifications/initialized' });
  const tools = (await request(2, 'tools/list')).result.tools;
  assert.deepEqual(tools.map(tool => tool.name), ['describe', 'render']);
  assert.deepEqual(tools[1].inputSchema.properties.codec.enum, ['h264', 'hevc']);
  assert.deepEqual(tools[1].inputSchema.properties.quality.enum, ['standard', 'high', 'very-high', 'maximum']);
  assert.equal(tools[1].inputSchema.properties.loops.maximum, 50);
  assert.equal(tools[1].inputSchema.properties.snap.default, false);
  const details = (await request(3, 'tools/call', { name: 'describe' })).result;
  assert.equal(JSON.parse(details.content[0].text).effectStages.length, 7);
  const digitalCatalog = (await request(14, 'tools/call', { name: 'describe', arguments: { stage: 'digital' } })).result;
  assert.equal(JSON.parse(digitalCatalog.content[0].text).effects.digital.parameters.length, 32);
  const shaderCatalog = (await request(15, 'tools/call', { name: 'describe', arguments: { stage: 'crt', shader: 'crtsim' } })).result;
  assert(JSON.parse(shaderCatalog.content[0].text).effects.crt.crtsim.length > 0);
  const badCatalog = await request(16, 'tools/call', { name: 'describe', arguments: { stage: 'unknown' } });
  assert.equal(badCatalog.error.code, -32602);
  assert.deepEqual(JSON.parse(details.content[0].text).outputs, ['png', 'mp4', 'gif']);
  assert.deepEqual(JSON.parse(details.content[0].text).mp4Codecs, ['h264', 'hevc']);
  const rendered = (await request(4, 'tools/call', {
    name: 'render', arguments: { input, output, preset, overwrite: true }
  })).result;
  assert.equal(JSON.parse(rendered.content[0].text).width, 64);
  assert.equal(JSON.parse(rendered.content[0].text).height, 64);
  const adjusted = (await request(17, 'tools/call', { name: 'render', arguments: {
    input, output, overwrite: true, effects: { processing: { mode: 'on', width: 64 },
      ntsc: { values: { composite_noise: true, composite_noise_intensity: 0.4 } } }
  } })).result;
  assert.equal(JSON.parse(adjusted.content[0].text).width, 96);
  const beforePlan = readFileSync(output);
  const plan = JSON.parse((await request(19, 'tools/call', { name: 'render', arguments: {
    input, output, preset, dryRun: true, snap: true, width: 106, height: 106
  } })).result.content[0].text);
  assert.deepEqual([plan.dryRun, plan.width, plan.height, plan.targetExists], [true, 128, 128, true]);
  assert.deepEqual(readFileSync(output), beforePlan);
  const batchPath = path.join(folder, 'planned.png');
  const batchArgs = { input, output: batchPath, times: [0.4, 0, 8], seconds: 0.5, fps: 12, zoom: 4, zoomAt: [0.75, 0.25] };
  const batchPlan = JSON.parse((await request(20, 'tools/call', { name: 'render', arguments: {
    ...batchArgs, dryRun: true
  } })).result.content[0].text);
  assert.deepEqual(batchPlan.outputs.map(position => position.frameIndex), [5, 0, 5]);
  assert.deepEqual(batchPlan.closeup.crop, { x: 60, y: 8, width: 24, height: 16 });
  const batchResult = JSON.parse((await request(21, 'tools/call', { name: 'render', arguments: {
    ...batchArgs, overwrite: true
  } })).result.content[0].text);
  assert.deepEqual(batchResult.outputs.map(position => position.path), batchPlan.outputs.map(position => position.path));
  assert.deepEqual(readFileSync(batchResult.outputs[0].path), readFileSync(batchResult.outputs[2].path));
  const badEffects = (await request(18, 'tools/call', { name: 'render', arguments: {
    input, output, overwrite: true, effects: { digital: { values: { quality: 100 } } }
  } })).result;
  assert.equal(badEffects.isError, true);
  assert.match(badEffects.content[0].text, /quality/);
  const snapped = (await request(5, 'tools/call', { name: 'render', arguments: {
    input, output, preset, width: 106, height: 106, snap: true, overwrite: true
  } })).result;
  assert.equal(JSON.parse(snapped.content[0].text).height, 128);
  const original = readFileSync(output);
  const collision = await request(6, 'tools/call', { name: 'render', arguments: { input, output } });
  assert.equal(collision.result.isError, true);
  assert.match(collision.result.content[0].text, /exist/i);
  assert.deepEqual(readFileSync(output), original);
  const invalid = await request(7, 'tools/call', { name: 'render', arguments: { input: 'relative.png', output } });
  assert.equal(invalid.error.code, -32602);
  const hevcResult = (await request(10, 'tools/call', { name: 'render', arguments: {
    input, output: hevc, codec: 'hevc', quality: 'very-high', seconds: 0.5, fps: 12, overwrite: true
  } })).result;
  assert.equal(JSON.parse(hevcResult.content[0].text).frames, 6);
  const probe = spawnSync(process.env.TAPETUBE_FFPROBE || 'ffprobe', ['-v', 'error', '-select_streams', 'v:0',
    '-show_entries', 'stream=codec_name', '-of', 'csv=p=0', hevc], { windowsHide: true, encoding: 'utf8' });
  assert.equal(probe.status, 0, probe.stderr);
  assert.equal(probe.stdout.trim(), 'hevc');
  const repeated = (await request(11, 'tools/call', { name: 'render', arguments: {
    input: hevc, output: path.join(folder, 'repeated-hevc.mp4'), codec: 'hevc', loops: 2, overwrite: true
  } })).result;
  assert.equal(JSON.parse(repeated.content[0].text).frames, 12);
  const customGif = (await request(12, 'tools/call', { name: 'render', arguments: {
    input, output: path.join(folder, 'custom.gif'), gifWidth: 120, gifFps: 50,
    seconds: 0.5, fps: 12, overwrite: true
  } })).result;
  const gif = JSON.parse(customGif.content[0].text);
  assert.deepEqual([gif.width, gif.height, gif.frames], [120, 80, 25]);
  send({ id: 8, method: 'tools/call', params: { name: 'render', arguments: {
    input, output: cancelled, width: 960, height: 540, seconds: 120, fps: 60
  } } });
  assert.deepEqual((await request(9, 'ping')).result, {});
  send({ method: 'notifications/cancelled', params: { requestId: 8 } });
  server.stdin.end();
  assert.deepEqual(await exited, { code: 0, signal: null }, stderr);
  assert(!existsSync(cancelled));
  assert(!readdirSync(folder).some(name => name.startsWith('.tapetube-')));
  assert(!notifications.some(message => message.id === 8));
  console.log('MCP initialize, tool discovery, cropped PNG, HEVC repeat export, overwrite protection and cancellation passed.');
} finally {
  clearTimeout(timer);
  reader.close();
  if (server.exitCode === null && server.signalCode === null) server.kill();
}
