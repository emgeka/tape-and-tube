// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class DigitalSmoke
{
    public static async Task RunAsync()
    {
        var directory=Path.Combine(Paths.Root,"artifacts","digital-integration");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(Paths.Root,"artifacts","digital-smoke-error.txt"),"");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var token=timeout.Token;var checks=new List<string>();
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);}
        MainWindow.CheckDigitalFormatSelection(Check);
        await MainWindow.CheckStillMotionAsync(Check, token);
        static string Hash(PixelFrame f)=>Convert.ToHexString(SHA256.HashData(f.Pixels));
        var input=Path.Combine(directory,"source.mp4");
        await MediaTools.CaptureAsync("ffmpeg",["-v","error","-nostdin","-y","-f","lavfi","-i","testsrc2=size=96x64:rate=6:duration=2",
            "-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=2","-c:v","mpeg4","-q:v","2","-c:a","aac",input],token);
        await MainWindow.CheckPreparedPreviewAsync(input, Check, token);
        await using var source=await VideoSource.OpenAsync(input,token);
        var original=await source.ReadFrameAsync(0,token);
        var baseline=new RenderSettings(false,false,NtscProcessor.Defaults().ToJsonString(),"",96,3,0,[],null,DigitalCatalog.Profile);
        using var pipeline=new EffectPipeline();
        async Task<PixelFrame> Render(int n,RenderSettings settings,string id="clip") =>
            (await pipeline.RenderAsync(await source.ReadFrameAsync(n,token),settings with{Frame=(uint)n},id,source.Info,source.ReadFrameAsync,token)).Frame;
        var identity=await Render(0,baseline);
        Check(Hash(original)==Hash(identity),"BGRA/RGBA channels, opaque alpha and unscaled identity preserved");
        var process=pipeline.WorkerProcessId;
        Check(process.HasValue,"Persistent worker started");
        Check(Hash(await Render(0,baseline with{RetroWidth=64}))==Hash(original),"All effects off bypasses retro downscale regardless of retained CRT width");
        foreach(var look in DigitalCatalog.Looks)DigitalCatalog.Validate(look.Values);
        Check(DigitalCatalog.Looks.Count==6&&DigitalCatalog.Parameters.Count==32,"Pinned six-format catalog and all 32 settings validate");
        var invalid=DigitalCatalog.Defaults;invalid["not-a-setting"]=0;
        try{DigitalCatalog.Validate(invalid);throw new Exception("Invalid catalog accepted");}catch(ArgumentException){checks.Add("Unknown digital settings rejected");}

        foreach(var shader in new[]{"crt-easymode","crt-aperture","crt-hyllian","crt-royale","crtglow_gauss","crtglow_lanczos","crtsim"})
        {
            // Resolve the same seven presets as the app, including CRTSim's subfolder.
            var path=Directory.GetFiles(Paths.ShaderRoot,shader+".slangp",SearchOption.AllDirectories).First();
            var look=DigitalCatalog.Find("webcam");
            var settings=baseline with{Crt=true,Shader=path,RetroWidth=look.Width,Digital=look.Values,Output=new(160,120)};
            string? expected3=null,expected9=null;
            for(int n=0;n<=9;n++){var f=await Render(n,settings);if(n==3)expected3=Hash(f);if(n==9)expected9=Hash(f);}
            Check(Hash(await Render(3,settings))==expected3,shader+": backward seek reconstructs genuine temporal history");
            Check(Hash(await Render(9,settings))==expected9,shader+": forward skip matches sequential output");
            await Render(0,settings);
            Check(Hash(await Render(9,settings))==expected9,shader+": loop starts from clean temporal state");
        }
        var pausedSettings = baseline with
        {
            Frame = 9,
            Crt = true,
            Shader = Path.Combine(Paths.ShaderRoot, "crt", "crtsim.slangp"),
            Digital = DigitalCatalog.Find("minidv").Values,
            Output = new(160, 120)
        };
        var previous = await Render(9, pausedSettings with { Digital = DigitalCatalog.Find("webcam").Values });
        var current = await source.ReadFrameAsync(9, token);
        var immediate = await pipeline.RenderAsync(current, pausedSettings, "clip", source.Info, cancellation: token, singleFrame: true);
        Check(Hash(immediate.Frame) != Hash(previous), "Changing a paused look renders the current frame without access to history");
        var exactAfterPreview = await Render(9, pausedSettings);
        using (var reference = new EffectPipeline())
        {
            var exact = await reference.RenderAsync(current, pausedSettings, "paused-reference", source.Info, source.ReadFrameAsync, token);
            Check(Hash(exactAfterPreview) == Hash(exact.Frame), "Paused single-frame preview does not contaminate the exact temporal result");
        }
        Check(pipeline.WorkerProcessId==process,"Settings changes, seven shaders and seeks retain the worker process");
        var alternate=new PixelFrame(original.Width,original.Height,original.Pixels.Select((v,i)=>(byte)(i%4==3?255:255-v)).ToArray());
        var switched=await pipeline.RenderAsync(alternate,baseline,"another-source",source.Info,source.ReadFrameAsync,token);
        Check(Hash(switched.Frame)==Hash(alternate),"Source replacement cannot reuse stale cached pixels");
        using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            cancel.CancelAfter(120);
            var longInfo=source.Info with{FrameCount=1000};
            async Task<PixelFrame> SlowHistory(int n,CancellationToken t){await Task.Delay(40,t);return original;}
            try{await pipeline.RenderAsync(original,baseline with{Frame=999},"cancel",longInfo,SlowHistory,cancel.Token);throw new Exception("Cancellation did not interrupt");}
            catch(OperationCanceledException){checks.Add("Cancellation interrupts history replay");}
        }
        Check(pipeline.WorkerProcessId==null,"Cancellation terminates worker and discards temporal state");
        Check(Hash(await Render(0,baseline))==Hash(original),"Next render recovers after cancellation");
        Check(pipeline.WorkerProcessId!=process,"Recovery creates a clean process");
        try{await pipeline.RenderAsync(original,baseline with{Frame=9},"missing-history",source.Info,null,token);throw new Exception("Missing history accepted");}
        catch(InvalidDataException){checks.Add("Missing source history fails without a fake result");}
        Check(Hash(await Render(0,baseline))==Hash(original),"Recovery after missing history");
        using(var crashed=Process.GetProcessById(pipeline.WorkerProcessId!.Value)){crashed.Kill();await crashed.WaitForExitAsync(token);}
        Check(Hash(await Render(0,baseline))==Hash(original),"Unexpected worker exit restarts with clean state");

        var old=new LookPreset("ntscrt-windows",1,true,true,"crt-easymode",320,3,NtscProcessor.Defaults(),[]);
        var oldJson=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(old))!.AsObject();
        oldJson.Remove("Renderer");oldJson.Remove("Digital");
        var loadedOld=LookPreset.Parse(oldJson.ToJsonString(),["crt-easymode"]);
        Check(loadedOld.Renderer=="legacy-v1"&&loadedOld.Digital==null,"Original v1 preset without new fields retains legacy renderer");
        var methods = new[] { "nearest", "nearest+", "bilinear", "bicubic", "lanczos", "area" };
        var pattern = PixelFrame.TestPattern();
        var scaleSettings = baseline with { Crt = true, Shader = Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp"),
            RetroWidth = 96, Output = new(384, 288) };
        var filtered = new HashSet<string>();
        foreach (var method in methods)
        {
            var stored = JsonSerializer.Serialize(old with { DownscaleMethod = method });
            Check(LookPreset.Parse(stored, ["crt-easymode"]).DownscaleMethod == method, $"{method} survives a look round trip");
            var image = await pipeline.RenderAsync(pattern, scaleSettings with { DownscaleMethod = method }, "downscale", cancellation: token);
            filtered.Add(Hash(image.Frame));
        }
        Check(filtered.Count == methods.Length, "All six downscale methods produce distinct images on the test pattern");
        var automaticScale = await pipeline.RenderAsync(pattern, scaleSettings, "downscale", cancellation: token);
        var areaScale = await pipeline.RenderAsync(pattern, scaleSettings with { DownscaleMethod = "area" }, "downscale", cancellation: token);
        Check(Hash(automaticScale.Frame) == Hash(areaScale.Frame), "The shared renderer still defaults to area averaging");
        foreach (var enabled in new bool?[] { null, true, false })
        {
            var stored = JsonSerializer.Serialize(old with { DownscaleEnabled = enabled });
            Check(LookPreset.Parse(stored, ["crt-easymode"]).DownscaleEnabled == enabled, $"Downscale state {enabled?.ToString() ?? "automatic"} survives a preset round trip");
        }
        var useNative = scaleSettings with { DownscaleEnabled = false, Output = null };
        var native = await pipeline.RenderAsync(pattern, useNative, "downscale-off", cancellation: token);
        var otherWidth = await pipeline.RenderAsync(pattern, useNative with { RetroWidth = 160, DownscaleMethod = "nearest" },
            "downscale-off", cancellation: token);
        Check(native.Frame.Width == pattern.Width && native.Frame.Height == pattern.Height
            && native.ScanlineRows == pattern.Height && Hash(native.Frame) == Hash(otherWidth.Frame)
            && Hash(native.Frame) != Hash(pattern), "CRT remains active at source size with independent downscale disabled");
        var standalone = await pipeline.RenderAsync(pattern, baseline with { DownscaleEnabled = true }, "downscale-only", cancellation: token);
        Check(standalone.Frame.Width == pattern.Width && standalone.ScanlineRows < pattern.Height && Hash(standalone.Frame) != Hash(pattern),
            "Internal downscale can operate on its own without changing the output canvas");

        foreach(var id in new[]{"webcam","minidv","cd_rom"})
        {
            var look=DigitalCatalog.Find(id);
            var preset=new LookPreset("tape-and-tube",2,false,true,"crt-easymode",look.Width,3,NtscProcessor.Defaults(),[],new(160,120),DigitalCatalog.Profile,look.Values);
            var saved=JsonSerializer.Serialize(preset);File.WriteAllText(Path.Combine(directory,id+"-look.json"),saved);
            var restored=LookPreset.Parse(saved,["crt-easymode"]);
            Check(restored.Renderer==DigitalCatalog.Profile&&restored.Width==look.Width&&restored.Digital!.OrderBy(p=>p.Key).SequenceEqual(look.Values.OrderBy(p=>p.Key)),id+": v2 preset round-trips all digital values, width and renderer");
            var settings=baseline with{Crt=true,Shader=Path.Combine(Paths.ShaderRoot,"crt","crt-easymode.slangp"),RetroWidth=look.Width,Digital=look.Values,Output=new(160,120),
                DownscaleMethod=id=="webcam"?"lanczos":null, DownscaleEnabled=id=="minidv"?false:null};
            var preview=new List<string>();for(int n=0;n<12;n++)preview.Add(Hash(await Render(n,settings,"look-"+id)));
            var observed=new List<string>();var output=Path.Combine(directory,id+".mp4");
            var result=await VideoExporter.ExportAsync(input,output,settings,cancellation:token,overwrite:true,frameObserver:(n,f)=>observed.Add(Hash(f)));
            Check(observed.SequenceEqual(preview),id+": every exported frame equals sequential preview before encoding");
            await using var exported=await VideoSource.OpenAsync(output,token);
            Check(result.Width==160&&result.Height==120&&exported.Info.FrameCount==12,id+": full 160×120 H.264 export");
            var audio=await MediaTools.CaptureAsync("ffprobe",["-v","error","-select_streams","a:0","-show_entries","stream=codec_name,duration,start_time","-of","json",output],token);
            using var metadata=JsonDocument.Parse(audio);var stream=metadata.RootElement.GetProperty("streams")[0];
            var duration=double.Parse(stream.GetProperty("duration").GetString()!,System.Globalization.CultureInfo.InvariantCulture);
            Check(stream.GetProperty("codec_name").GetString()=="aac"&&Math.Abs(duration-2)<0.05,id+": original audio retained for full clip duration");
            var late=await exported.ReadFrameAsync(9,token);
            Check(late.Pixels.Where((v,i)=>i%4!=3).Average(v=>(double)v)>2,id+": output remains visible at late frame");
        }
        // Independent retained CLI is the full-chain reference, including NTSC frame offset.
        var raw=Path.Combine(directory,"reference-input.rgba");
        await using(var file=File.Create(raw))for(int n=0;n<12;n++)await file.WriteAsync(EffectPipeline.ToCore(await source.ReadFrameAsync(n,token)).Rgba,token);
        var chain=baseline with{Ntsc=true,Crt=true,Shader=Path.Combine(Paths.ShaderRoot,"crt","crt-easymode.slangp"),Digital=DigitalCatalog.Find("cd_rom").Values,RetroWidth=240,Output=new(144,96)};
        var expected=new List<string>();for(int n=0;n<12;n++)expected.Add(Hash(await Render(n,chain,"full-chain")));
        // The reference CLI deliberately refuses existing outputs.
        var uniqueReference=Path.Combine(directory,"reference-"+Guid.NewGuid().ToString("N")+".rgba");
        var job=Path.Combine(directory,"reference-job.json");
        File.WriteAllText(job,JsonSerializer.Serialize(new{apiVersion=1,inputPath=raw,outputPath=uniqueReference,width=96,height=64,outputWidth=144,outputHeight=96,frames=12,fpsNum=6,fpsDen=1,retroWidth=240,shaderRoot=Paths.ShaderRoot,shaderPreset="crt/crt-easymode.slangp",ntscJson=chain.NtscJson,digitalSettings=chain.Digital}));
        var start=new ProcessStartInfo(EffectPipeline.WorkerPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("--job");start.ArgumentList.Add(job);
        using(var cli=Process.Start(start)!){var errors=cli.StandardError.ReadToEndAsync();var output=cli.StandardOutput.ReadToEndAsync();using var cancel=token.Register(()=>cli.Kill(true));await cli.WaitForExitAsync(token);if(cli.ExitCode!=0)throw new Exception(await errors);await output;}
        var referenceBytes=File.ReadAllBytes(uniqueReference);
        Check(referenceBytes.Length==12*144*96*4,"Full-chain reference output complete");
        var actual=Enumerable.Range(0,12).Select(n=>Hash(new PixelFrame(144,96,EffectPipeline.SwapChannels(referenceBytes.AsSpan(n*144*96*4,144*96*4).ToArray()))));
        Check(actual.SequenceEqual(expected),"Digital + NTSC + CRT matches retained independent CLI byte-for-byte");
        await AnalogSmoke.RunAsync(directory, source, Check, token);
        await ScreenLoopSmoke.RunAsync(directory, source, Check, token);
        await ImportedLookSmoke.RunAsync(directory, source, Check, token);
        await SourceCropSmoke.RunAsync(directory, source, Check, token);
        await EffectTimelineSmoke.RunAsync(directory, source, Check, token);
        await HdrVideoSmoke.RunAsync(directory, Check, token);
        checks.AddRange(await DigitalMediaSmoke.RunAsync(directory,token));
        File.WriteAllText(Path.Combine(directory,"report.json"),JsonSerializer.Serialize(new{passed=true,count=checks.Count,adapter=pipeline.WorkerAdapter,checks},new JsonSerializerOptions{WriteIndented=true}));
    }
}
