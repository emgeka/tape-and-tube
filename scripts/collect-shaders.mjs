// SPDX-License-Identifier: GPL-3.0-only
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const [sourceArg, destinationArg] = process.argv.slice(2);
if (!sourceArg || !destinationArg) throw new Error('Usage: node collect-shaders.mjs SOURCE DESTINATION');
const source = path.resolve(sourceArg), destination = path.resolve(destinationArg);
const presets = ['crt-easymode','crt-aperture','crt-hyllian','crt-royale','crtglow_gauss','crtglow_lanczos','crtsim'];
const pending = presets.map(name => `crt/${name}.slangp`), files = new Map();
const within = absolute => absolute.startsWith(source + path.sep);
function enqueue(from, relative) {
  const absolute = path.resolve(source, path.dirname(from), relative.replaceAll('/', path.sep));
  if (!within(absolute)) throw new Error(`Shader reference escapes asset root: ${from}: ${relative}`);
  if (!fs.existsSync(absolute)) throw new Error(`Missing shader reference: ${from}: ${relative}`);
  pending.push(path.relative(source, absolute).replaceAll(path.sep, '/'));
}
while (pending.length) {
  const relative = pending.pop();
  if (files.has(relative)) continue;
  const bytes = fs.readFileSync(path.join(source, relative));
  const text = /\.(slangp|slang|h|inc|txt)$/i.test(relative) ? bytes.toString('utf8') : '';
  files.set(relative, {path:relative,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),bytes:bytes.length,
    hasLicenseNotice:/license|copyright|public domain dedication|permission is hereby granted/i.test(text)});
  if (/\.slangp$/i.test(relative)) {
    const assignments = new Map([...text.matchAll(/^\s*([\w]+)\s*=\s*"?([^"\r\n]+)"?\s*$/gm)].map(m=>[m[1],m[2].trim()]));
    for (const [key,value] of assignments) if (/^shader\d+$/.test(key)) enqueue(relative,value);
    for (const name of (assignments.get('textures') ?? '').split(';').filter(Boolean)) {
      if (!assignments.has(name)) throw new Error(`Texture is not assigned: ${relative}: ${name}`);
      enqueue(relative,assignments.get(name));
    }
    for (const m of text.matchAll(/^\s*#reference\s+"([^"]+)"/gm)) enqueue(relative,m[1]);
  }
  for (const m of text.matchAll(/^\s*#\s*include\s+"([^"]+)"/gm)) enqueue(relative,m[1]);
  let directory = path.dirname(relative);
  while (directory !== '.' && directory !== path.dirname(directory)) {
    for (const name of fs.readdirSync(path.join(source,directory)))
      if (/^(LICENSE|COPYING|NOTICE|COPYRIGHT)([.-]|$)/i.test(name) && fs.statSync(path.join(source,directory,name)).isFile())
        pending.push(path.join(directory,name).replaceAll(path.sep,'/'));
    directory = path.dirname(directory);
  }
}
for (const relative of files.keys()) {
  const output = path.join(destination,relative);
  fs.mkdirSync(path.dirname(output),{recursive:true});
  fs.copyFileSync(path.join(source,relative),output);
}
const manifest = {revision:'cb01f2f238700eed7ec1c859be1737a9fbcdcb7c',presets,
  redistributionReviewComplete:false,
  releasePolicy:JSON.parse(fs.readFileSync('docs/shader-license-evidence.json','utf8')).releasePolicy,
  note:'Original files and notices retained. Documented shader-scope uncertainties are accepted under the owner release policy, not automatic release blockers. Missing notices are not new license grants. See the accompanying shader review and evidence.',
  files:[...files.values()].sort((a,b)=>a.path.localeCompare(b.path))};
fs.writeFileSync(path.join(destination,'asset-manifest.json'),JSON.stringify(manifest,null,2));
console.log(JSON.stringify({files:files.size,bytes:manifest.files.reduce((n,f)=>n+f.bytes,0),noticeMissing:manifest.files.filter(f=>/\.(slang|h|inc)$/i.test(f.path)&&!f.hasLicenseNotice).length}));
