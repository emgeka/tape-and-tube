// SPDX-License-Identifier: GPL-3.0-only
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const [rootArg, destinationArg, metadataArg] = process.argv.slice(2);
const root = path.resolve(rootArg), destination = path.resolve(destinationArg);
fs.mkdirSync(destination,{recursive:true});
const supplementalRoot=path.join(root,'licenses/upstream');
const supplementalSources=JSON.parse(fs.readFileSync(path.join(supplementalRoot,'sources.json'),'utf8'));
for(const source of supplementalSources){
  const bytes=fs.readFileSync(path.join(supplementalRoot,source.file));
  const blob=crypto.createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
  if(blob!==source.gitBlob)throw Error('Original upstream notice changed: '+source.file);
}
const entries=[];
function copyNotices(directory, target) {
  if (!fs.existsSync(directory)) return [];
  const copied=[];
  for (const name of fs.readdirSync(directory))
    if (/^(LICENSE|LICENCE|COPYING|NOTICE|COPYRIGHT|THIRD.PARTY.NOTICES)([._-]|$)/i.test(name) && fs.statSync(path.join(directory,name)).isFile()) {
      fs.mkdirSync(target,{recursive:true});fs.copyFileSync(path.join(directory,name),path.join(target,name));copied.push(name);
    }
  return copied;
}
const metadata=JSON.parse(fs.readFileSync(metadataArg,'utf8').replace(/^\uFEFF/,''));
for (const pkg of metadata.packages) {
  if (pkg.name === 'ntscrt-ntsc') continue;
  const directory=path.dirname(pkg.manifest_path), target=path.join(destination,'rust',`${pkg.name}-${pkg.version}`);
  const copied=copyNotices(directory,target);
  if(pkg.name==='ntsc-rs'||pkg.name==='ntsc-rs-macros') copied.push(...copyNotices(path.resolve(directory,'../..'),target));
  if(pkg.license_file && fs.existsSync(path.join(directory,pkg.license_file))) {
    fs.mkdirSync(target,{recursive:true});fs.copyFileSync(path.join(directory,pkg.license_file),path.join(target,path.basename(pkg.license_file)));
  }
  let metadataFile=null;
  if(copied.length===0){
    fs.mkdirSync(target,{recursive:true});
    const original=path.join(directory,'Cargo.toml.orig');
    const source=fs.existsSync(original)?original:pkg.manifest_path;
    fs.copyFileSync(source,path.join(target,'PACKAGE-METADATA.toml'));
    metadataFile=`rust/${pkg.name}-${pkg.version}/PACKAGE-METADATA.toml`;
  }
  entries.push({ecosystem:'rust',name:pkg.name,version:pkg.version,license:pkg.license,repository:pkg.repository,authors:pkg.authors,metadataFile,notices:[...new Set(copied)]});
}
const assets=JSON.parse(fs.readFileSync(path.join(root,'src/Ntscrt.Windows/obj/project.assets.json'),'utf8'));
const libraries = {...assets.libraries};
for(const name of ['microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64']) {
  const directory=path.join(root,'.tools/nuget',name,'10.0.14');
  if(fs.existsSync(directory))libraries[`${name}/10.0.14`]={type:'package',path:`${name}/10.0.14`};
}
for(const [id,library] of Object.entries(libraries)) {
  if(library.type!=='package') continue;
  const directory=path.join(root,'.tools/nuget',library.path);
  const nuspec=fs.readdirSync(directory).find(x=>x.endsWith('.nuspec'));
  const text=nuspec?fs.readFileSync(path.join(directory,nuspec),'utf8'):'';
  const tag=name=>(text.match(new RegExp(`<${name}(?:\\s[^>]*)?>([\\s\\S]*?)<\\/${name}>`))??[])[1]??'';
  const target=path.join(destination,'nuget',id.replace('/','-'));
  const copied=copyNotices(directory,target);
  const supplemental={
    'Vortice.Mathematics/2.1.0':'Vortice.Mathematics-LICENSE.txt',
    'Vortice.Direct3D11/3.8.3':'Vortice.Windows-LICENSE.txt',
    'Vortice.DirectX/3.8.3':'Vortice.Windows-LICENSE.txt',
    'Vortice.DXGI/3.8.3':'Vortice.Windows-LICENSE.txt',
    'SharpGen.Runtime/2.4.2-beta':'SharpGenTools-LICENSE.txt',
    'SharpGen.Runtime.COM/2.4.2-beta':'SharpGenTools-LICENSE.txt',
    'microsoft.windowsdesktop.app.runtime.win-x64/10.0.14':'WindowsDesktop-THIRD-PARTY-NOTICES.txt'
  }[id];
  if(supplemental){
    fs.mkdirSync(target,{recursive:true});fs.copyFileSync(path.join(root,'licenses/upstream',supplemental),path.join(target,supplemental));copied.push(supplemental);
  }
  fs.mkdirSync(target,{recursive:true});
  if(nuspec)fs.copyFileSync(path.join(directory,nuspec),path.join(target,nuspec));
  entries.push({ecosystem:'nuget',name:id,license:tag('license'),copyright:tag('copyright'),authors:tag('authors'),notices:copied});
}
const rustDocs=path.join(root,'.tools/rustup/toolchains/stable-x86_64-pc-windows-gnu/share/doc/rust');
copyNotices(rustDocs,path.join(destination,'rust-runtime'));
if(fs.existsSync(path.join(rustDocs,'licenses')))fs.cpSync(path.join(rustDocs,'licenses'),path.join(destination,'rust-runtime/license-texts'),{recursive:true});
fs.cpSync(path.join(root,'licenses/upstream'),path.join(destination,'upstream'),{recursive:true});
const missingNotices=entries.filter(e=>e.notices.length===0).map(e=>({name:e.name,version:e.version,declaredLicense:e.license,metadataFile:e.metadataFile}));
const lines=['# Included dependency notices','', 'Own application and host: GPL-3.0-only. Upstream components retain their licenses.','', 'This inventory records declared licenses and collected original texts; it is not a new license grant. See the accompanying dependency and shader review for documented uncertainties.','', '| Component | Declared license | Notice files |','| --- | --- | --- |',...entries.map(e=>`| ${e.name}${e.version?' '+e.version:''} | ${e.license} | ${e.notices.join(', ')||'No standalone notice supplied; package metadata included'} |`),'','## Remaining notice gaps','',...missingNotices.map(e=>`- ${e.name} ${e.version}: declared ${e.declaredLicense}; original package metadata preserved. No copyright statement was invented.`),'','- librashader: official binary/source provenance verified; a complete transitive notice inventory is not supplied by its binary archive. See librashader-release-evidence.json.',''];
fs.writeFileSync(path.join(destination,'README.md'),lines.join('\n'));
fs.writeFileSync(path.join(destination,'dependency-inventory.json'),JSON.stringify({reviewComplete:false,
  missingNotices,
  note:'Declared license metadata and available upstream notices. Verify notice completeness, particularly native librashader transitive dependencies, before public redistribution.',entries},null,2));
console.log(JSON.stringify({dependencies:entries.length,noticeFiles:entries.reduce((n,e)=>n+e.notices.length,0)}));
