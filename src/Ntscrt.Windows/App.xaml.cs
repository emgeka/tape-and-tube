// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Windows;

namespace Ntscrt.Windows;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Localization.Initialize();
        Components.ConfigureNativeLibraries();
        if (e.Args.Contains("--update-smoke"))
        {
            try { UpdateSmoke.Run(); Shutdown(0); }
            catch (Exception ex)
            {
                Directory.CreateDirectory(Paths.Root + "/artifacts");
                File.WriteAllText(Paths.Root + "/artifacts/update-smoke-error.txt", ex.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Contains("--crt-temporal-diagnostic"))
        {
            try
            {
                if (e.Args.Length == 3) await CrtTemporalDiagnostic.RunClipAsync(e.Args[1], e.Args[2]);
                else await CrtTemporalDiagnostic.RunAsync();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(Paths.Root + "/artifacts");
                File.WriteAllText(Paths.Root + "/artifacts/crt-temporal-diagnostic-error.txt", ex.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Contains("--smoke") || e.Args.Contains("--video-smoke") || e.Args.Contains("--export-smoke") || e.Args.Contains("--output-smoke") || e.Args.Contains("--language-smoke"))
        {
            try
            {
                if (e.Args.Contains("--language-smoke")) LanguageSmoke.Run();
                else if (e.Args.Contains("--output-smoke")) await OutputSmoke.RunAsync();
                else if (e.Args.Contains("--export-smoke")) await ExportSmoke.RunAsync();
                else if (e.Args.Contains("--video-smoke")) await VideoSmoke.RunAsync();
                else Smoke.Run();
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(Paths.Root + "/artifacts");
                File.WriteAllText(Paths.Root + (e.Args.Contains("--language-smoke") ? "/artifacts/language-smoke-error.txt" : e.Args.Contains("--output-smoke") ? "/artifacts/output-smoke-error.txt" : e.Args.Contains("--export-smoke") ? "/artifacts/export-smoke-error.txt" : e.Args.Contains("--video-smoke") ? "/artifacts/video-smoke-error.txt" : "/artifacts/smoke-error.txt"), ex.ToString());
                Shutdown(1);
            }
            return;
        }
        var window = new MainWindow();
        window.Show();
        if (e.Args.Contains("--demo")) window.LoadDemo();
        var videoArgument = Array.IndexOf(e.Args, "--video");
        if (videoArgument >= 0 && videoArgument + 1 < e.Args.Length) await window.LoadVideo(e.Args[videoArgument + 1]);
    }
}
