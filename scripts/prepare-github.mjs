// SPDX-License-Identifier: GPL-3.0-only
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
const root=process.cwd();
const readJson=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const release=readJson(path.join(root,'release.json'));
const prerelease=release.version.includes('-');
if((release.channel==='beta')!==prerelease)throw Error('Version and release channel disagree');
const stamp=new Date().toISOString().replace(/[:.]/g,'-');
const destination=path.join(root,'artifacts','github',stamp);
const repository=path.join(destination,'repository');
fs.mkdirSync(repository,{recursive:true});
const files=[];
function copy(relative){
 const input=path.join(root,relative),output=path.join(repository,relative);
 if(!fs.statSync(input).isFile())throw Error('Not a source file: '+relative);
 fs.mkdirSync(path.dirname(output),{recursive:true});fs.copyFileSync(input,output);files.push(relative.replaceAll('\\','/'));
}
for(const file of ['README.md','RELEASE_README.md','RELEASE_REVIEW.md','RELEASE_NOTES.md','CHANGELOG.md','CONTRIBUTING.md','PUBLICATION.md','THIRD_PARTY.md','COPYRIGHT.md','LICENSE','dependencies.json','components.json','release.json','global.json','.gitignore','Start.ps1'])copy(file);
for(const directory of ['src/Ntscrt.Windows','scripts'])
 for(const file of fs.readdirSync(path.join(root,directory)))if(/\.(cs|xaml|csproj|ps1|mjs|json)$/.test(file)&&fs.statSync(path.join(root,directory,file)).isFile())copy(directory+'/'+file);
for(const file of ['native/Cargo.toml','native/Cargo.lock','native/src/lib.rs','docs/screenshot.jpg','docs/shader-license-audit.json','docs/shader-license-evidence.json','docs/librashader-release-evidence.json'])copy(file);
for(const file of fs.readdirSync(path.join(root,'licenses/upstream')))copy('licenses/upstream/'+file);
for(const file of fs.readdirSync(path.join(root,'installer')))if(/\.(c|iss|txt)$/.test(file))copy('installer/'+file);
function copyDocs(directory){for(const entry of fs.readdirSync(path.join(root,directory),{withFileTypes:true})){const relative=directory+'/'+entry.name;if(entry.isDirectory())copyDocs(relative);else if(/\.(md|ya?ml)$/.test(entry.name))copy(relative);}}
copyDocs('docs');copyDocs('.github');
// Validate only the explicit upload tree; never inspect or upload unrelated workspace data.
const forbidden=/(^|\/)(\.tools|\.git|bin|obj|artifacts|runtime|assets)(\/|$)|components\.local\.json|media-tools\.json|\.env($|\.)|\.(exe|dll|pdb|mp4|mov|mkv|pem|key)$/i;
const secrets=/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|\bgh[pousr]_[A-Za-z0-9]{24,}|\bgithub_pat_[A-Za-z0-9_]{20,}/;
const hashes=[];
for(const relative of files){
 if(forbidden.test(relative))throw Error('Forbidden upload file: '+relative);
 const bytes=fs.readFileSync(path.join(repository,relative));
 if(!relative.endsWith('.jpg')){
  const text=bytes.toString('utf8');
  if(text.includes(root)||text.includes(root.replaceAll('\\','\\\\')))throw Error('Private workspace path: '+relative);
  if(secrets.test(text))throw Error('Credential-like content: '+relative);
 }
 hashes.push({path:relative,bytes:bytes.length,sha256:crypto.createHash('sha256').update(bytes).digest('hex')});
}
for(const file of ['src/Ntscrt.Windows/Translations.json','native/Cargo.lock','src/Ntscrt.Windows/packages.lock.json','.github/ISSUE_TEMPLATE/bug_report.md','LICENSE'])if(!files.includes(file))throw Error('Missing source file: '+file);
const catalog=readJson(path.join(repository,'src/Ntscrt.Windows/Translations.json'));
if(Object.keys(catalog.languages).length!==8)throw Error('Expected eight added language catalogs plus German/English');
for(const [language,strings] of Object.entries(catalog.languages))if(Object.keys(strings).length!==Object.keys(catalog.translations).length)throw Error('Incomplete translations: '+language);
// Verify repository-relative Markdown links in every own documentation file.
for(const relative of files.filter(f=>f.endsWith('.md'))){
 const text=fs.readFileSync(path.join(repository,relative),'utf8');
 for(const m of text.matchAll(/!?\[[^\]]*\]\(([^)\s]+)\)/g)){
  if(/^(https?:|mailto:|#)/.test(m[1]))continue;
  const target=path.resolve(path.dirname(path.join(repository,relative)),decodeURIComponent(m[1].split('#')[0]));
  if(!target.startsWith(repository+path.sep)||!fs.existsSync(target))throw Error('Broken documentation link: '+relative+' -> '+m[1]);
 }
}
const archive=`TapeTube-${release.version}-source-preview.zip`;
const script='$ErrorActionPreference="Stop"; Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory($env:TAPETUBE_SOURCE_DIR,$env:TAPETUBE_SOURCE_ZIP)';
execFileSync(path.join(process.env.SystemRoot,'System32','WindowsPowerShell','v1.0','powershell.exe'),['-NoProfile','-NonInteractive','-Command',script],{env:{...process.env,TAPETUBE_SOURCE_DIR:repository,TAPETUBE_SOURCE_ZIP:path.join(destination,archive)}});
const digest=crypto.createHash('sha256').update(fs.readFileSync(path.join(destination,archive))).digest('hex');
fs.writeFileSync(path.join(destination,'SHA256SUMS.txt'),digest+'  '+archive+'\n');
fs.copyFileSync(path.join(root,'RELEASE_NOTES.md'),path.join(destination,'RELEASE_NOTES.md'));
fs.writeFileSync(path.join(destination,'repository-files.json'),JSON.stringify(hashes,null,2)+'\n');
fs.writeFileSync(path.join(destination,'github-draft.json'),JSON.stringify({repository:{owner:'emgeka',name:'tape-and-tube',visibility:'public',created:false,description:'Windows adaptation of NTSCRT by Finn McKenty: NTSC/VHS and CRT effects via ntsc-rs, RetroArch shaders and librashader.',topics:['windows','wpf','ntsc','vhs','crt','video-effects','rust','csharp']},release:{tag_name:'v'+release.version,name:'Tape & Tube '+release.version,draft:true,prerelease,make_latest:prerelease?'false':'true',targetCommit:null,notes:'RELEASE_NOTES.md',assets:[`TapeTube-${release.version}-win-x64-setup.exe`,`TapeTube-${release.version}-source.zip`,'SHA256SUMS.txt']},externalWritesPerformed:false,ownerApprovalPending:!release.publicReleaseApproved},null,2)+'\n');
fs.writeFileSync(path.join(destination,'validation.json'),JSON.stringify({passed:true,sourceFiles:files.length,languages:10,translationEntries:Object.keys(catalog.translations).length,allowlistChecked:true,workspacePathsAbsent:true,credentialPatternsAbsent:true,documentationLinksChecked:true,thirdPartyBinariesAndShadersExcluded:true,externalWritesPerformed:false,source:{file:archive,sha256:digest},binaryPublicationAuthorized:release.publicReleaseApproved,channel:release.channel,remainingLimitations:['Fresh-VM missing-prerequisite flow unverified','VirtualBox CRTSim export issue','Native transitive notice inventory gaps documented'],shaderScopeUncertaintiesAccepted:true},null,2)+'\n');
fs.mkdirSync(path.join(root,'runtime'),{recursive:true});fs.writeFileSync(path.join(root,'runtime/current-github.txt'),path.relative(root,destination).replaceAll('\\','/')+'\n');
console.log(JSON.stringify({directory:path.relative(root,destination),source:archive,files:files.length,sha256:digest,externalWrites:false}));
