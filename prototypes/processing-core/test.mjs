// SPDX-License-Identifier: GPL-3.0-only
// Identical generated inputs and assertions on Windows and Linux. No personal media.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '../..');
const options = Object.fromEntries(process.argv.slice(2).map((x,i,a) => x.startsWith('--') ? [x.slice(2),a[i+1]] : []).filter(x=>x.length));
const binary = path.resolve(options.binary || path.join(project,'artifacts/core-prototype/target/release/tapetube-core-probe'+(process.platform==='win32'?'.exe':'')));
const shaderRoot = path.resolve(options['shader-root'] || path.join(project,'assets/slang-shaders-cb01f2f238700eed7ec1c859be1737a9fbcdcb7c'));
const output = path.resolve(options.output || path.join(project,'artifacts/core-prototype/tests',new Date().toISOString().replace(/[:.]/g,'-')));
if(fs.existsSync(output) && (!fs.statSync(output).isDirectory() || fs.readdirSync(output).length!==0)) {
  throw Error('Choose a new or empty result directory; existing results are never overwritten.');
}
fs.mkdirSync(output,{recursive:true});
let configured = {};
const toolsFile=path.join(project,'runtime/media-tools.json');
if(process.platform==='win32' && fs.existsSync(toolsFile)) configured=JSON.parse(fs.readFileSync(toolsFile,'utf8').replace(/^\uFEFF/,''));
const ffmpeg=options.ffmpeg || process.env.TAPETUBE_FFMPEG || configured.ffmpeg || 'ffmpeg';
const ffprobe=options.ffprobe || process.env.TAPETUBE_FFPROBE || configured.ffprobe || 'ffprobe';
const checks=[];
const renders=[];
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
function run(program,args,{allowFailure=false,timeout=600000,...extra}={}) {
  const p=spawnSync(program,args,{encoding:'utf8',windowsHide:true,timeout,maxBuffer:4*1024*1024,...extra});
  if(!allowFailure && (p.error || p.status!==0)) throw Error(`${path.basename(program)} failed (${p.status}): ${p.error?.message||p.stderr||p.stdout}`);
  return p;
}
function check(name,condition,detail={}) {
  checks.push({name,passed:!!condition,...detail});
  console.log(`${condition?'PASS':'FAIL'} ${name}`);
  if(!condition) throw Error(name);
}
function ff(args){return run(ffmpeg,['-hide_banner','-loglevel','error','-nostdin',...args]);}
function probe(file){return JSON.parse(run(ffprobe,['-v','error','-show_streams','-show_format','-of','json',file]).stdout);}
function job(name,overrides={},allowFailure=false){
  const target=path.join(output,name+'.rgba');
  const request={apiVersion:1,inputPath:path.join(output,'decoded.rgba'),outputPath:target,width:160,height:120,
    outputWidth:320,outputHeight:240,frames:36,fpsNum:12,fpsDen:1,retroWidth:128,shaderRoot,
    shaderPreset:null,ntscJson:null,digitalFormat:null,...overrides};
  const requestPath=path.join(output,name+'.json');fs.writeFileSync(requestPath,JSON.stringify(request,null,2)+'\n');
  const p=run(binary,['--job',requestPath],{allowFailure});
  let result;try{result=JSON.parse(p.stdout);}catch{throw Error(`Invalid runner JSON for ${name}: ${p.stdout}\n${p.stderr}`);}
  fs.writeFileSync(path.join(output,name+'.result.json'),JSON.stringify(result,null,2)+'\n');
  fs.writeFileSync(path.join(output,name+'.log'),p.stderr||'');
  if(!allowFailure && (!result.ok||result.apiVersion!==1))throw Error(result.error||'Bad protocol result');
  renders.push({name,...result});
  return {request,result,p,bytes:fs.existsSync(request.outputPath)?fs.readFileSync(request.outputPath):null};
}
function equalSelected(name,baseline,selected,indices){
  const size=baseline.request.outputWidth*baseline.request.outputHeight*4;
  check(name,indices.every((index,i)=>baseline.bytes.subarray(index*size,(index+1)*size).equals(selected.bytes.subarray(i*size,(i+1)*size))),{indices});
}
function png(raw,w,h,destination){ff(['-f','rawvideo','-pixel_format','rgba','-video_size',`${w}x${h}`,'-i',raw,'-frames:v','1','-update','1',destination]);}
function rmsBins(pcm,rate=48000){
  const bin=Math.floor(rate/100), values=[];
  for(let start=0;start+bin*4<=pcm.length;start+=bin*4){let energy=0;for(let i=0;i<bin;i++){const f=pcm.readFloatLE(start+i*4);energy+=f*f;}values.push(Math.sqrt(energy/bin));}
  return values;
}
const started=new Date();
let failure=null;
try{
  const description=JSON.parse(run(binary,['--describe']).stdout);
  check('protocol and effect metadata',description.apiVersion===1&&description.digitalFormats.length===6&&description.digitalParameters.length===32&&description.crtPresets.length===7);
  fs.writeFileSync(path.join(output,'description.json'),JSON.stringify(description,null,2)+'\n');
  fs.writeFileSync(path.join(output,'ffmpeg-version.txt'),run(ffmpeg,['-version']).stdout);
  const w=160,h=120,count=36,frameBytes=w*h*4;
  const source=Buffer.alloc(frameBytes*count);
  for(let n=0;n<count;n++)for(let y=0;y<h;y++)for(let x=0;x<w;x++){
    const i=n*frameBytes+(y*w+x)*4;
    const box=(x>n*3%110&&x<n*3%110+35&&y>30&&y<70);
    source[i]=box?240:(x*255/(w-1)+n*7)%256;
    source[i+1]=box?40:y*255/(h-1);
    source[i+2]=box?50:((x+y+n)%9<4?220:25);
    source[i+3]=255;
  }
  fs.writeFileSync(path.join(output,'source.rgba'),source);
  const rate=48000,seconds=count/12,samples=rate*seconds;
  const wav=Buffer.alloc(44+samples*2);wav.write('RIFF',0);wav.writeUInt32LE(wav.length-8,4);wav.write('WAVEfmt ',8);wav.writeUInt32LE(16,16);wav.writeUInt16LE(1,20);wav.writeUInt16LE(1,22);wav.writeUInt32LE(rate,24);wav.writeUInt32LE(rate*2,28);wav.writeUInt16LE(2,32);wav.writeUInt16LE(16,34);wav.write('data',36);wav.writeUInt32LE(samples*2,40);
  for(let i=0;i<samples;i++){const t=i/rate;const active=(t>=1&&t<1.2)||(t>=2&&t<2.2);wav.writeInt16LE(active?Math.round(Math.sin(2*Math.PI*880*t)*12000):0,44+i*2);}
  fs.writeFileSync(path.join(output,'source.wav'),wav);
  const sourceMp4=path.join(output,'source.mp4');
  ff(['-f','rawvideo','-pixel_format','rgba','-video_size',`${w}x${h}`,'-framerate','12','-i',path.join(output,'source.rgba'),'-i',path.join(output,'source.wav'),'-map','0:v:0','-map','1:a:0','-c:v','libx264','-crf','14','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k','-t',String(seconds),sourceMp4]);
  ff(['-i',sourceMp4,'-map','0:v:0','-an','-vf','setpts=PTS-STARTPTS,fps=12,setsar=1','-fps_mode','passthrough','-pix_fmt','rgba','-f','rawvideo',path.join(output,'decoded.rgba')]);
  check('actual MP4 decoding has every source frame',fs.statSync(path.join(output,'decoded.rgba')).size===frameBytes*count);
  png(path.join(output,'decoded.rgba'),w,h,path.join(output,'original.png'));

  const settings={fields:0,frame_rate:12,sensor_noise:0.15,quality:0.25,keyframe_interval:0,codec:0};
  const bypass=job('bypass');
  const intra=job('dct-intra',{digitalSettings:settings});
  check('DCT changes the same-sized bypass picture',!intra.bytes.equals(bypass.bytes));
  const video1=job('video1',{digitalSettings:{...settings,codec:1,keyframe_interval:0.5,data_loss:0.3}});
  check('Video 1 produces a distinct look',!intra.bytes.equals(video1.bytes));
  const indices=[0,9,3,35,1,18,0,1,2,12,24,35];
  for(const [name,config] of [
    ['intra',settings],
    ['video1',{...settings,codec:1,keyframe_interval:0.5,data_loss:0.3}],
    ['predicted',{...settings,keyframe_interval:0.5,data_loss:0.4}],
    ['datamosh',{...settings,keyframe_interval:0.5,data_loss:0.15,datamosh:1}]
  ]){
    const sequential=name==='intra'?intra:name==='video1'?video1:job(name+'-sequential',{digitalSettings:config});
    const seeking=job(name+'-seek-loop-skip',{digitalSettings:config,indices});
    equalSelected(name+' sequential vs seeks, loops and dropped presentation frames',sequential,seeking,indices);
    check(name+' independent render instances',seeking.result.framesWritten===indices.length&&seeking.bytes.length===indices.length*320*240*4);
  }

  const crtNames=['crt-easymode','crt-aperture','crt-hyllian','crt-royale','crtglow_gauss','crtglow_lanczos','crtsim'];
  for(const name of crtNames){
    const still=job(name,{indices:[0],shaderPreset:`crt/${name}.slangp`,ntscJson:'{"version":1}',digitalSettings:settings});
    let nonBlack=0;for(let i=0;i<still.bytes.length;i+=4)if(still.bytes[i]+still.bytes[i+1]+still.bytes[i+2]>5)nonBlack++;
    check(name+' renders Digital + NTSC + CRT',nonBlack>320*240/20,{nonBlackPixels:nonBlack,adapter:still.result.adapter});
    png(still.request.outputPath,320,240,path.join(output,name+'.png'));
  }

  const full=job('full-video',{shaderPreset:'crt/crt-easymode.slangp',ntscJson:'{"version":1}',digitalFormat:'webcam',digitalSettings:{fields:0,frame_rate:12,keyframe_interval:0.5,data_loss:0.15}});
  const fullSeeking=job('full-video-preview',{shaderPreset:'crt/crt-easymode.slangp',ntscJson:'{"version":1}',digitalFormat:'webcam',digitalSettings:{fields:0,frame_rate:12,keyframe_interval:0.5,data_loss:0.15},indices});
  equalSelected('Digital + NTSC + easymode preview matches export after seek/loop/skip',full,fullSeeking,indices);
  const exportMp4=path.join(output,'processed.mp4');
  ff(['-copyts','-f','rawvideo','-pixel_format','rgba','-video_size','320x240','-framerate','12','-i',full.request.outputPath,'-itsoffset','0','-i',sourceMp4,'-map','0:v:0','-map','1:a:0','-map_metadata','-1','-map_chapters','-1','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k','-af','aresample=async=1:first_pts=0,apad','-t',String(seconds),'-movflags','+faststart',exportMp4]);
  const exported=probe(exportMp4),video=exported.streams.find(s=>s.codec_type==='video'),audio=exported.streams.find(s=>s.codec_type==='audio');
  check('video export dimensions, frames and audio',video?.width===320&&video?.height===240&&Number(video.nb_frames)===count&&audio?.codec_name==='aac');
  check('audio and video timestamps aligned',Math.abs(Number(video.start_time)-Number(audio.start_time))<=1/12&&Math.abs(Number(video.duration)-Number(audio.duration))<=1/12,{videoStart:video.start_time,audioStart:audio.start_time,videoDuration:video.duration,audioDuration:audio.duration});
  for(const [name,file]of[['in',sourceMp4],['out',exportMp4]])ff(['-i',file,'-map','0:a:0','-ac','1','-ar','48000','-f','f32le',path.join(output,name+'.audio.f32')]);
  const originalEnergy=rmsBins(fs.readFileSync(path.join(output,'in.audio.f32'))),outputEnergy=rmsBins(fs.readFileSync(path.join(output,'out.audio.f32')));
  const onset=bins=>bins.findIndex(x=>x>0.05);
  check('audio pulse position retained',Math.abs(onset(originalEnergy)-onset(outputEnergy))<=1,{inputPulseSeconds:onset(originalEnergy)/100,outputPulseSeconds:onset(outputEnergy)/100,toleranceSeconds:0.01});
  const decodedOut=path.join(output,'export-decoded.rgba');ff(['-i',exportMp4,'-an','-pix_fmt','rgba','-f','rawvideo',decodedOut]);
  const exportedPixels=fs.readFileSync(decodedOut);let blackFrames=0;for(let n=0;n<count;n++){let visible=0;for(let i=n*320*240*4;i<(n+1)*320*240*4;i+=4)if(exportedPixels[i]+exportedPixels[i+1]+exportedPixels[i+2]>8)visible++;if(visible<320*240/100)blackFrames++;}
  check('test export contains no black frames',blackFrames===0,{blackFrames});

  for(const [name,override]of[['invalid-version',{apiVersion:99}],['invalid-parameter',{digitalSettings:{unknown_knob:1}}],['invalid-index',{indices:[36]}],['invalid-shader',{shaderPreset:'../outside.slangp'}]]){
    const invalid=job(name,override,true);check(name+' rejected without output',invalid.p.status!==0&&invalid.result.ok===false&&invalid.bytes===null);
  }
  const overwrite=job('overwrite-existing-output',{outputPath:full.request.outputPath},true);check('existing output protected',overwrite.p.status!==0&&overwrite.result.ok===false&&sha(overwrite.bytes)===sha(full.bytes));
  fs.writeFileSync(path.join(output,'export-probe.json'),JSON.stringify(exported,null,2)+'\n');
}catch(e){failure=e.stack||String(e);console.error(failure);process.exitCode=1;}
const report={schemaVersion:1,passed:!failure,startedAt:started.toISOString(),completedAt:new Date().toISOString(),platform:process.platform,architecture:process.arch,osRelease:os.release(),binary,shaderRoot,output,checks,renders,failure,scope:'Generated 160x120/12fps/3s clip. This does not establish real-time speed, all GPUs, historical VirtualBox bug resolution or native Linux desktop support.'};
fs.writeFileSync(path.join(output,'report.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({passed:report.passed,checks:checks.length,report:path.join(output,'report.json'),preview:path.join(output,'crt-easymode.png'),video:path.join(output,'processed.mp4')}));
