// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text;
using System.Text.Json;
using Ntscrt.Windows;

namespace TapeTube.Cli;

internal static class McpServer
{
    private const string Version = "2025-11-25";
    private const int MaxMessageLength = 1_048_576;
    private static readonly JsonElement Tools = JsonSerializer.Deserialize<JsonElement>("""
        [
          {
            "name": "describe",
            "description": "List output formats and effect stages. Select a stage for parameter ranges, default values and presets.",
            "inputSchema": {"type":"object","properties":{
              "stage":{"type":"string","enum":["processing","ntsc","digital","vcr","reception","crt","screen-loop","all"],"description":"Effect stage to describe. Omit for an overview."},
              "shader":{"type":"string","description":"Optional installed CRT shader name. Defaults to listing all shaders for the crt stage."}
            },"additionalProperties":false},
            "annotations":{"readOnlyHint":true,"openWorldHint":false}
          },
          {
            "name": "render",
            "description": "Render a local image or video with an optional Tape & Tube, NTSCRT or Screen Loop preset. The output extension selects PNG, MP4 or GIF.",
            "inputSchema":{
              "type":"object",
              "properties":{
                "input":{"type":"string","description":"Local source file path."},
                "output":{"type":"string","description":"Local target path ending in .png, .mp4 or .gif."},
                "preset":{"type":"string","description":"Optional local preset JSON file path."},
                "effects":{
                  "type":"object","description":"Effect overrides for Tape & Tube and Screen Loop presets, or no preset. Parameter IDs come from describe.",
                  "additionalProperties":false,
                  "properties":{
                    "processing":{"type":"object","additionalProperties":false,"properties":{
                      "mode":{"enum":["auto","on","off"]},"width":{"type":"integer","minimum":16,"maximum":1280},"method":{"enum":["auto","nearest","nearest+","bilinear","bicubic","lanczos","area"]}
                    }},
                    "ntsc":{"$ref":"#/$defs/stage"},
                    "digital":{"type":"object","additionalProperties":false,"properties":{
                      "enabled":{"type":"boolean"},"format":{"type":"string"},"values":{"$ref":"#/$defs/numbers"}
                    }},
                    "vcr":{"$ref":"#/$defs/numericStage"},
                    "reception":{"$ref":"#/$defs/numericStage"},
                    "crt":{"type":"object","additionalProperties":false,"properties":{
                      "enabled":{"type":"boolean"},"shader":{"type":"string"},"values":{"$ref":"#/$defs/numbers"}
                    }},
                    "screenLoop":{"$ref":"#/$defs/numericStage"}
                  }
                },
                "width":{"type":"integer","minimum":1,"maximum":8192,"description":"Output width. Supply height as well."},
                "height":{"type":"integer","minimum":1,"maximum":8192,"description":"Output height. Supply width as well."},
                "frame":{"type":"integer","minimum":0,"description":"Zero-based frame for PNG export."},
                "times":{"type":"array","minItems":1,"maxItems":256,"items":{"type":"number","minimum":0},"description":"PNG positions in seconds. Supply times or frame."},
                "zoom":{"type":"number","minimum":1,"maximum":12,"description":"Magnify the finished PNG at the same output size."},
                "zoomAt":{"type":"array","minItems":2,"maxItems":2,"items":{"type":"number","minimum":0,"maximum":1},"description":"Close-up center [x,y], from the top-left [0,0]. Requires zoom."},
                "seconds":{"type":"number","minimum":0.5,"maximum":120,"description":"Duration for animation from a still."},
                "fps":{"type":"integer","minimum":1,"maximum":60,"description":"Frame rate for animation from a still."},
                "codec":{"type":"string","enum":["h264","hevc"],"description":"MP4 video codec. Defaults to h264. HEVC requires FFmpeg with libx265."},
                "quality":{"type":"string","enum":["standard","high","very-high","maximum"],"default":"high","description":"MP4 video quality. Higher quality generally produces larger files."},
                "loops":{"type":"integer","minimum":1,"maximum":50,"default":1,"description":"Total plays of a video input in the MP4."},
                "gifWidth":{"type":"integer","minimum":64,"maximum":1920,"description":"GIF width in pixels. Height follows the cropped source aspect ratio."},
                "gifFps":{"type":"integer","minimum":1,"maximum":50,"description":"GIF output frame rate. Available for both video and still input."},
                "snap":{"type":"boolean","default":false,"description":"Adjust the output size to the scanline grid."},
                "dryRun":{"type":"boolean","default":false,"description":"Report resolved settings, dimensions and timing without rendering or writing output."},
                "overwrite":{"type":"boolean","default":false,"description":"Allow replacing an existing output file."}
              },
              "required":["input","output"],
              "$defs":{
                "numbers":{"type":"object","additionalProperties":{"type":"number"}},
                "numericStage":{"type":"object","additionalProperties":false,"properties":{
                  "enabled":{"type":"boolean"},"values":{"$ref":"#/$defs/numbers"}
                }},
                "stage":{"type":"object","additionalProperties":false,"properties":{
                  "enabled":{"type":"boolean"},"values":{"type":"object","additionalProperties":{"type":["number","boolean"]}}
                }}
              },
              "additionalProperties":false
            },
            "annotations":{"destructiveHint":true,"openWorldHint":false}
          }
        ]
        """);

    private sealed record RenderJob(JsonElement Id, CancellationTokenSource Cancel, Task Render, Task Response);

    public static async Task RunAsync(CancellationToken stopping)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        var outputLock = new object();
        RenderJob? job = null;
        var initialized = false;
        var ready = false;

        void Write(object message)
        {
            lock (outputLock)
                Console.WriteLine(JsonSerializer.Serialize(message, CoreWorkerClient.Json));
        }
        void Reply(JsonElement id, object result) => Write(new { jsonrpc = "2.0", id, result });
        void Error(JsonElement? id, int code, string message) => Write(new { jsonrpc = "2.0", id, error = new { code, message } });
        void ToolResult(JsonElement id, object value) =>
            Reply(id, new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value, CoreWorkerClient.Json) } } });
        void ToolError(JsonElement id, string message) =>
            Reply(id, new { isError = true, content = new[] { new { type = "text", text = message } } });

        try
        {
            while (await Console.In.ReadLineAsync(stopping).ConfigureAwait(false) is { } line)
            {
                if (job is { Render.IsCompleted: true })
                {
                    await job.Response.ConfigureAwait(false);
                    job.Cancel.Dispose();
                    job = null;
                }
                if (line.Length > MaxMessageLength) { Error(null, -32600, "Message is too large."); continue; }
                JsonDocument document;
                try { document = JsonDocument.Parse(line); }
                catch (JsonException) { Error(null, -32700, "Invalid JSON."); continue; }
                using (document)
                {
                    var request = document.RootElement;
                    var id = Property(request, "id");
                    var method = Text(request, "method");
                    if (Text(request, "jsonrpc") != "2.0" || method == null ||
                        id.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.String or JsonValueKind.Number))
                    {
                        Error(null, -32600, "Invalid request.");
                        continue;
                    }
                    var args = Property(request, "params");
                    if (id.ValueKind == JsonValueKind.Undefined)
                    {
                        if (method == "notifications/initialized" && initialized) ready = true;
                        var cancelledId = Property(args, "requestId");
                        if (method == "notifications/cancelled" && job != null &&
                            cancelledId.ValueKind is JsonValueKind.String or JsonValueKind.Number &&
                            JsonElement.DeepEquals(job.Id, cancelledId)) job.Cancel.Cancel();
                        continue;
                    }

                    switch (method)
                    {
                        case "ping":
                            Reply(id, new { });
                            break;
                        case "initialize" when !initialized:
                            var requested = Text(args, "protocolVersion");
                            if (requested == null) { Error(id, -32602, "protocolVersion is required."); break; }
                            initialized = true;
                            Reply(id, new
                            {
                                protocolVersion = requested is Version or "2025-06-18" or "2025-03-26" ? requested : Version,
                                capabilities = new { tools = new { listChanged = false } },
                                serverInfo = new { name = "tape-and-tube", title = "Tape & Tube",
                                    version = typeof(McpServer).Assembly.GetName().Version!.ToString(3) }
                            });
                            break;
                        case "tools/list" when ready:
                            Reply(id, new { tools = Tools });
                            break;
                        case "tools/call" when ready:
                            var name = Text(args, "name");
                            var arguments = Property(args, "arguments");
                            if (name == "describe")
                            {
                                if (arguments.ValueKind != JsonValueKind.Undefined &&
                                    (arguments.ValueKind != JsonValueKind.Object || arguments.EnumerateObject().Any(p =>
                                        p.Name is not ("stage" or "shader") || p.Value.ValueKind != JsonValueKind.String)))
                                {
                                    Error(id, -32602, "describe accepts optional stage and shader strings.");
                                    break;
                                }
                                try { ToolResult(id, Program.Describe(Text(arguments, "stage"), Text(arguments, "shader"))); }
                                catch (ArgumentException ex) { Error(id, -32602, ex.Message); }
                                catch (Exception ex) { ToolError(id, ex.Message); }
                            }
                            else if (name == "render")
                            {
                                if (job != null) { ToolError(id, "A render is already running."); break; }
                                Dictionary<string, string> options;
                                try { options = RenderOptions(arguments); }
                                catch (ArgumentException ex) { Error(id, -32602, ex.Message); break; }
                                var renderId = id.Clone();
                                var cancel = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                                var render = Task.Run(() => Program.RenderAsync(options, cancel.Token));
                                async Task RespondAsync()
                                {
                                    try
                                    {
                                        var result = await render.ConfigureAwait(false);
                                        if (!cancel.IsCancellationRequested) ToolResult(renderId, result);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (!cancel.IsCancellationRequested) ToolError(renderId, ex.Message);
                                    }
                                }
                                job = new(renderId, cancel, render, RespondAsync());
                            }
                            else Error(id, -32602, "Unknown tool.");
                            break;
                        default:
                            if (method is "tools/list" or "tools/call") Error(id, -32002, "The server is not initialized.");
                            else Error(id, -32601, "Method not found.");
                            break;
                    }
                }
            }
        }
        finally
        {
            if (job != null)
            {
                job.Cancel.Cancel();
                await job.Response.ConfigureAwait(false);
                job.Cancel.Dispose();
            }
        }
    }

    private static Dictionary<string, string> RenderOptions(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) throw new ArgumentException("render requires an arguments object.");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in args.EnumerateObject())
        {
            var name = prop.Name;
            var value = prop.Value;
            var option = name switch { "gifWidth" => "--gif-width", "gifFps" => "--gif-fps", "dryRun" => "--dry-run",
                "times" => "--time", "zoomAt" => "--zoom-at", _ => "--" + name };
            if (!seen.Add(name)) throw new ArgumentException("Repeated argument: " + name);
            switch (name)
            {
                case "input" or "output" or "preset" when value.ValueKind == JsonValueKind.String:
                    var path = value.GetString()!;
                    if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                        throw new ArgumentException(name + " requires an absolute file path.");
                    options.Add(option, path);
                    break;
                case "width" or "height" or "frame" or "fps" or "loops" or "gifWidth" or "gifFps"
                    when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n):
                    options.Add(option, FormattableString.Invariant($"{n}"));
                    break;
                case "seconds" or "zoom" when value.ValueKind == JsonValueKind.Number:
                    options.Add(option, value.GetRawText());
                    break;
                case "times" when value.ValueKind == JsonValueKind.Array && value.GetArrayLength() is > 0 and <= PngSequence.MaxPositions
                    && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.Number):
                case "zoomAt" when value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 2
                    && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.Number):
                    options.Add(option, string.Join(",", value.EnumerateArray().Select(item => item.GetRawText())));
                    break;
                case "effects" when value.ValueKind == JsonValueKind.Object:
                    options.Add(option, value.GetRawText());
                    break;
                case "codec" or "quality" when value.ValueKind == JsonValueKind.String:
                    options.Add(option, value.GetString()!);
                    break;
                case "overwrite" or "snap" or "dryRun" when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    if (value.GetBoolean()) options.Add(option, "");
                    break;
                default:
                    throw new ArgumentException("Invalid render argument: " + name);
            }
        }
        if (!options.ContainsKey("--input") || !options.ContainsKey("--output"))
            throw new ArgumentException("Both input and output are required.");
        return options;
    }

    private static JsonElement Property(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) ? value : default;

    private static string? Text(JsonElement parent, string name) =>
        Property(parent, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
}
