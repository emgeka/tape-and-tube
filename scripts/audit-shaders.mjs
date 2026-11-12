// SPDX-License-Identifier: GPL-3.0-only
import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
const readJson=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const revision=readJson('dependencies.json')['slang-shaders'].revision;
const root=path.resolve('assets/slang-shaders-'+revision);
const output=path.resolve('artifacts/shader-license-review');
execFileSync(process.execPath,['scripts/collect-shaders.mjs',root,output],{stdio:'pipe'});
const manifest=readJson(path.join(output,'asset-manifest.json'));
const expected=['crt-easymode','crt-aperture','crt-hyllian','crt-royale','crtglow_gauss','crtglow_lanczos','crtsim'];
if(JSON.stringify(manifest.presets)!==JSON.stringify(expected))throw Error('All seven original presets must be retained');
function dependencies(relative,visited=new Set()){
 if(visited.has(relative))return visited;visited.add(relative);
 const text=/\.(slangp|slang|h|inc)$/i.test(relative)?fs.readFileSync(path.join(root,relative),'utf8'):'';
 const enqueue=reference=>{const resolved=path.resolve(root,path.dirname(relative),reference);if(!resolved.startsWith(root+path.sep))throw Error('Reference escapes source root');dependencies(path.relative(root,resolved).split(path.sep).join('/'),visited);};
 if(relative.endsWith('.slangp')){
  const assignments=new Map([...text.matchAll(/^\s*([\w]+)\s*=\s*"?([^"\r\n]+)"?\s*$/gm)].map(m=>[m[1],m[2].trim()]));
  for(const [key,value] of assignments)if(/^shader\d+$/.test(key))enqueue(value);
  for(const texture of (assignments.get('textures')??'').split(';').filter(Boolean))enqueue(assignments.get(texture));
  for(const m of text.matchAll(/^\s*#reference\s+"([^"]+)"/gm))enqueue(m[1]);
 }
 for(const m of text.matchAll(/^\s*#\s*include\s+"([^"]+)"/gm))enqueue(m[1]);
 return visited;
}
const graphs=expected.map(name=>({name,files:[...dependencies(`crt/${name}.slangp`)].sort()}));
const base=`https://github.com/libretro/slang-shaders/blob/${revision}/`;
const files=manifest.files.map(f=>{
 const text=/\.(slang|h|inc)$/i.test(f.path)?fs.readFileSync(path.join(root,f.path),'utf8'):'';
 let declaration=null;
 if(/CC0 1\.0 Universal/.test(text))declaration='CC0-1.0';
 else if(/MIT LICENSE|Permission is hereby granted, free of charge/.test(text))declaration='MIT-style grant; preserve full original notice';
 else if(/either version 2[\s\S]{0,70}any later version/.test(text))declaration='GPL-2.0-or-later';
 else if(/License:\s*GPL/.test(text))declaration='GPL; version not specified in file';
 const royale=f.path.startsWith('crt/shaders/crt-royale/');
 return {path:f.path,sha256:f.sha256,bytes:f.bytes,presets:graphs.filter(g=>g.files.includes(f.path)).map(g=>g.name),
  fileDeclaration:declaration,noticeEvidence:declaration?base+f.path:null,
  familyLicense:royale?base+'crt/shaders/crt-royale/LICENSE.TXT':null,
  reviewStatus:declaration?'declaration-found; packaging obligations require final review':royale?'family-license-reference-found; scope/provenance review pending':/\.(png|slangp)$/.test(f.path)?'asset-or-preset-license-scope-needs-confirmation':/LICENSE/i.test(f.path)?'upstream-license-text-retained':'explicit-scope-needs-confirmation'};
});
const evidence=[
 {id:'collection-policy',url:'https://github.com/libretro/slang-shaders/issues/150#issuecomment-670993746',finding:'Maintainer describes mixed licenses and files without a listed license; this is not a blanket grant.'},
 {id:'glow-original-bloom',url:'https://github.com/libretro/common-shaders/commit/c8c5f5ab3d0a5c47c3e246efa134c4467ec5d1d4',finding:'Themaister introduced the predecessor bloom code in 2014; no license grant found in the inspected addition.'},
 {id:'glow-rename',url:'https://github.com/libretro/common-shaders/commit/a7c364862d7aa93dd5c8a66806900b36e41f5901',finding:'Themaister moved bloom files into crt/glow.'},
 {id:'glow-refactor',url:'https://github.com/libretro/common-shaders/commit/6c2ca06be42a3678a4b92f27b8a34ade92bf79bd',finding:'Hyllian moved the Cg files into crt/shaders/glow in 2015.'},
 {id:'glow-slang-port',url:'https://github.com/libretro/slang-shaders/commit/efd79721cb517b1affe366b3094115992e04dc50',finding:'rz5 translated Glow shaders/presets to Slang in 2016; inspected additions have no explicit license grant.'},
 {id:'glow-later-contributions',url:'https://github.com/libretro/slang-shaders/commit/8f26d985e683c499c92327c629ca92cd1eeee9d3',finding:'hunterk added curvature and moire compensation in 2018; current resolve.slang credits Themaister and Timothy Lottes.'},
 {id:'hyllian-assets',url:'https://github.com/libretro/slang-shaders/commit/3e7cc725a5d08168c20c9c7e736b00c27567f8d4',finding:'Hyllian describes LUT1/LUT2 as representing his Trinitron displays; provenance evidence, not an explicit standalone LUT license grant.'},
 {id:'crtsim',url:'https://github.com/libretro/slang-shaders/commit/46172167c9ed4884d92da5df588cbcc92c4a7ac2',finding:'hunterk introduced the port. Five shader files include J. Kyle Pittman CC0 notices. See curated evidence for original implementation/textures; port/preset scope remains under review.'}
];
const curated=readJson('docs/shader-license-evidence.json');
if(curated.revision!==revision)throw Error('Curated evidence refers to another shader revision');
for(const assessment of curated.fileAssessments){
 const file=files.find(f=>f.path===assessment.path);
 if(!file||file.sha256!==assessment.sha256)throw Error('Stale license evidence: '+assessment.path);
 file.provenanceAssessment=assessment;
 file.reviewStatus=assessment.status;
}
for(const file of files.filter(f=>f.fileDeclaration==='GPL; version not specified in file')){
 file.complianceVersion='GPL-3.0; original unspecified-version declaration retained';
 file.versionEvidence='gpl-unspecified-version';
}
evidence.push(...curated.findings);
const audit={reviewDate:'2026-11-11',revision,policy:'retain-all-seven-presets',releasePolicy:curated.releasePolicy,redistributionReviewComplete:false,presets:graphs.map(g=>({name:g.name,referencedFiles:g.files.length,reviewComplete:false})),files,evidence,
 conclusion:'All seven presets retained unchanged. Original CRTSim code/assets and Royale mask provenance are documented. Remaining scope questions are transparently recorded and accepted under the owner release policy, not automatic shader release blockers. This is not a blanket license grant. Native dependency packaging checks remain separate.'};
fs.writeFileSync('docs/shader-license-audit.json',JSON.stringify(audit,null,2)+'\n');
console.log(JSON.stringify({presets:graphs.length,files:files.length,perFileDeclarations:files.filter(f=>f.fileDeclaration).length,remainingSourceScopeQuestions:files.filter(f=>/\.(slang|h|inc)$/.test(f.path)&&!f.fileDeclaration&&!f.familyLicense).length,redistributionReviewComplete:false}));
