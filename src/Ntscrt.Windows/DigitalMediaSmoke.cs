// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class DigitalMediaSmoke
{
    public static async Task<List<string>> RunAsync(string directory,CancellationToken token)
    {
        var checks=new List<string>();var measurements=new List<object>();
        void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add(label);}
        static string Hash(PixelFrame frame)=>Convert.ToHexString(SHA256.HashData(frame.Pixels));
        foreach(var scenario in new[]{("720p",1280,720,new OutputSettings(1280,720),false),
            ("1080p-crop",1920,1080,new OutputSettings(720,720,OutputFit.Crop),false),
            ("portrait-vfr",1080,1920,new OutputSettings(960,1707),true)})
        {
            var (name,w,h,output,vfr)=scenario;var input=Path.Combine(directory,name+"-source.mp4");
            var args=new List<string>{"-v","error","-nostdin","-y","-f","lavfi","-i",$"testsrc2=size={w}x{h}:rate=30000/1001:duration=0.5",
                "-f","lavfi","-i","sine=frequency=660:sample_rate=48000:duration=0.5"};
            if(vfr)args.AddRange(["-vf","select=not(eq(mod(n\\,3)\\,1))","-fps_mode","vfr"]);
            args.AddRange(["-c:v","mpeg4","-q:v","3","-threads","2","-c:a","aac",input]);
            await MediaTools.CaptureAsync("ffmpeg",args.ToArray(),token);
            await using var source=await VideoSource.OpenAsync(input,token);
            using var pipeline=new EffectPipeline();var hashes=new List<string>();var renderTimes=new List<double>();
            var settings=new RenderSettings(true,true,NtscProcessor.Defaults().ToJsonString(),Path.Combine(Paths.ShaderRoot,"crt","crt-easymode.slangp"),320,3,0,[],output,DigitalCatalog.Profile,DigitalCatalog.Find("webcam").Values);
            for(int n=0;n<source.Info.FrameCount;n++)
            {
                var pixels=await source.ReadFrameAsync(n,token);var clock=Stopwatch.StartNew();
                var result=await pipeline.RenderAsync(pixels,settings with{Frame=(uint)n},name,source.Info,source.ReadFrameAsync,token);
                renderTimes.Add(clock.Elapsed.TotalMilliseconds);hashes.Add(Hash(result.Frame));
            }
            var pid=pipeline.WorkerProcessId;
            var last=source.Info.FrameCount-1;
            await pipeline.RenderAsync(await source.ReadFrameAsync(0,token),settings,name,source.Info,source.ReadFrameAsync,token);
            var seekClock=Stopwatch.StartNew();
            var sought=await pipeline.RenderAsync(await source.ReadFrameAsync(last,token),settings with{Frame=(uint)last},name,source.Info,source.ReadFrameAsync,token);
            Check(Hash(sought.Frame)==hashes[last],name+": full-size source seek reproduces sequential pixels");
            Check(pipeline.WorkerProcessId==pid,name+": worker retained across full-size render and seek");
            var seekMs=seekClock.Elapsed.TotalMilliseconds;
            var actual=new List<string>();var exportClock=Stopwatch.StartNew();var target=Path.Combine(directory,name+"-output.mp4");
            var exported=await VideoExporter.ExportAsync(input,target,settings,cancellation:token,overwrite:true,frameObserver:(i,f)=>actual.Add(Hash(f)));
            var exportMs=exportClock.Elapsed.TotalMilliseconds;
            Check(actual.SequenceEqual(hashes),name+": all NTSC + digital + CRT export frames equal preview");
            Check(exported.Width==output.Width&&exported.Height==((output.Height+1)&~1),name+": output geometry and H.264 even padding");
            await using var decoded=await VideoSource.OpenAsync(target,token);
            Check(decoded.Info.FrameCount==source.Info.FrameCount&&Math.Abs(decoded.Info.Duration-source.Info.Duration)<0.01,name+": normalized frame count and duration preserved");
            var late=await decoded.ReadFrameAsync(last,token);
            Check(late.Pixels.Where((v,i)=>i%4!=3).Average(v=>(double)v)>2,name+": late encoded frame remains visible");
            using var worker=Process.GetProcessById(pid!.Value);
            measurements.Add(new{name,source=new{w,h,source.Info.Rate,source.Info.FrameCount},output,
                firstFrameMs=renderTimes[0],warmMeanMs=renderTimes.Skip(1).Average(),seekMs,exportMs,workerWorkingSetBytes=worker.WorkingSet64,adapter=pipeline.WorkerAdapter,
                timingScope="Frame timings include BGRA/RGBA conversion, IPC, CPU effects, upload/readback and output composition; exclude media decoding. Export is end-to-end. Short synthetic clips, no realtime guarantee."});
        }
        var preciseRateInput = Path.Combine(directory, "high-precision-rate-source.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
            "testsrc2=size=96x64:rate=30", "-frames:v", "14", "-c:v", "mpeg4", "-video_track_timescale", "10000000", preciseRateInput], token);
        await using (var source = await VideoSource.OpenAsync(preciseRateInput, token))
        using (var preview = new EffectPipeline())
        {
            Check(source.Info.Rate.Split('/').Any(term => int.Parse(term, System.Globalization.CultureInfo.InvariantCulture) > 1_000_000),
                "High precision fixture exceeds the old frame-rate fraction limit");
            var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, [], null,
                DigitalCatalog.Profile, DigitalCatalog.Find("webcam").Values);
            var first = await source.ReadFrameAsync(0, token);
            var rendered = await preview.RenderAsync(first, settings, "high-precision-rate", source.Info, source.ReadFrameAsync, token);
            var output = Path.Combine(directory, "high-precision-rate-output.mp4");
            string? firstExportHash = null;
            var exported = await VideoExporter.ExportAsync(preciseRateInput, output, settings, cancellation: token, overwrite: true,
                frameObserver: (index, frame) => { if (index == 0) firstExportHash = Hash(frame); });
            Check(firstExportHash == Hash(rendered.Frame) && exported.Frames == 14 && Math.Abs(exported.Duration - source.Info.Duration) < 0.001,
                "High precision source rate works in preview and export without changing clip duration");
        }
        // Regression range for the previously reported CRTSim failure beyond ~16 s.
        var longInput=Path.Combine(directory,"crtsim-24s-source.mp4");
        await MediaTools.CaptureAsync("ffmpeg",["-v","error","-nostdin","-y","-f","lavfi","-i","testsrc2=size=96x64:rate=12:duration=24",
            "-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=24","-c:v","mpeg4","-q:v","3","-c:a","aac",longInput],token);
        await using(var source=await VideoSource.OpenAsync(longInput,token))
        using(var preview=new EffectPipeline())
        {
            var settings=new RenderSettings(true,true,NtscProcessor.Defaults().ToJsonString(),Path.Combine(Paths.ShaderRoot,"crt","crtsim.slangp"),320,3,0,[],new(144,96),DigitalCatalog.Profile,DigitalCatalog.Find("webcam").Values);
            var samples=new Dictionary<int,string>();
            foreach(var n in new[]{0,180,192,204,287})samples[n]=Hash((await preview.RenderAsync(await source.ReadFrameAsync(n,token),settings with{Frame=(uint)n},"long-clip",source.Info,source.ReadFrameAsync,token)).Frame);
            var observed=new Dictionary<int,string>();bool visible=true;
            await VideoExporter.ExportAsync(longInput,Path.Combine(directory,"crtsim-24s-output.mp4"),settings,cancellation:token,overwrite:true,frameObserver:(n,f)=>{
                if(samples.ContainsKey(n))observed[n]=Hash(f);
                if(f.Pixels.Where((v,i)=>i%4!=3).Average(v=>(double)v)<=2)visible=false;
            });
            Check(observed.OrderBy(p=>p.Key).SequenceEqual(samples.OrderBy(p=>p.Key)),"CRTsim 24 s: export matches preview before, during and after 16 s");
            Check(visible,"CRTsim 24 s: all 288 rendered frames remain visible");
            await using var encoded=await VideoSource.OpenAsync(Path.Combine(directory,"crtsim-24s-output.mp4"),token);
            Check(encoded.Info.FrameCount==288,"CRTsim 24 s: encoded export contains all 288 frames");
            Check((await encoded.ReadFrameAsync(204,token)).Pixels.Where((v,i)=>i%4!=3).Average(v=>(double)v)>2,"CRTsim 24 s: encoded picture remains visible at 17 s");
        }
        File.WriteAllText(Path.Combine(directory,"media-measurements.json"),JsonSerializer.Serialize(new{passed=true,checks,measurements},new JsonSerializerOptions{WriteIndented=true}));
        return checks;
    }
}
