using System;
using System.IO;
using System.Linq;
using System.Windows;
using QuestPDF.Infrastructure;
using TakingMedications.Common;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications;

public partial class App : Application
{
    public App()
    {
        // QuestPDF Community Edition бесплатна для индивидуальных разработчиков,
        // open-source-проектов и компаний с годовым оборотом < $1M USD.
        // См. https://www.questpdf.com/license/
        QuestPDF.Settings.License = LicenseType.Community;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        // Режим генерации PDF-руководства: TM_GUIDE_PDF=<path> [TM_GUIDE_SHOTS=<dir>]
        var guidePath = Environment.GetEnvironmentVariable("TM_GUIDE_PDF");
        if (!string.IsNullOrEmpty(guidePath))
        {
            var shotsDir = Environment.GetEnvironmentVariable("TM_GUIDE_SHOTS")
                           ?? System.IO.Path.GetDirectoryName(guidePath)!;
            try { UserGuideGenerator.Generate(guidePath, shotsDir); }
            finally { Shutdown(0); }
            return;
        }

        // Smoke-режим: при TM_SMOKE_PDF=<path> генерируем тестовый PDF и
        // сразу выходим. Используется для CI и ручных проверок генератора.
        var smokePath = Environment.GetEnvironmentVariable("TM_SMOKE_PDF");
        if (!string.IsNullOrEmpty(smokePath))
        {
            try { RunPdfSmoke(smokePath!); }
            finally { Shutdown(0); }
            return;
        }

        // Проверяем нужно ли запускаться скрыто:
        // --start-hidden: передаётся из реестра автозапуска Windows
        // BackgroundMode == "start_hidden": пользователь выбрал в настройках
        bool startHidden = e.Args.Contains("--start-hidden");
        if (!startHidden)
        {
            try
            {
                var state = new StateStore(AppPaths.ResolveDataDir()).Load();
                startHidden = state.Settings.BackgroundMode == "start_hidden";
            }
            catch { }
        }

        var main = new MainWindow();
        MainWindow = main;
        if (!startHidden)
            main.Show();
    }


    private static void RunPdfSmoke(string outputPath)
    {
        var ctx = new MedAppContext(AppPaths.ResolveDataDir());

        // Засеем минимум: пара секций+препаратов (если default не дал),
        // запись АД, отметка приёма, покупка.
        if (ctx.Sections.TrueForAll(s => s.Items.Count == 0))
        {
            ctx.Sections[0].Items.Add(new Medication
            {
                Id = "smoketest1", Time = "09:00", Name = "Smoke Test 1",
                Subtitle = "demo", Note = "with food",
                Doctor = "Dr. House", Course = "30 days",
                Description = "Demo entry for PDF smoke test."
            });
            ctx.Sections[2].Items.Add(new Medication
            {
                Id = "smoketest2", Time = "19:00", Name = "Smoke Test 2",
                Doctor = "Dr. Smith", Course = "ongoing",
            });
        }

        var today = DateTime.Today;
        for (int i = 0; i < 14; i++)
        {
            var iso = today.AddDays(-i).ToString("yyyy-MM-dd");
            ctx.State.SetTaken(iso, "smoketest1", i % 3 != 0);
            ctx.State.SetTaken(iso, "smoketest2", i % 4 != 0);
        }

        ctx.State.PressureLog.Add(new PressureEntry
        {
            Timestamp = today.AddDays(-1).ToString("yyyy-MM-dd 09:30"),
            Systolic = 128, Diastolic = 84, Pulse = 72
        });
        ctx.State.PressureLog.Add(new PressureEntry
        {
            Timestamp = today.ToString("yyyy-MM-dd 08:15"),
            Systolic = 145, Diastolic = 92, Pulse = 78
        });

        ctx.State.Purchases.Add(new PurchaseEntry
        {
            Date = today.AddDays(-3).ToString("yyyy-MM-dd"),
            MedId = "smoketest1", Amount = 150.50m, Note = "demo"
        });

        var opts = new ReportOptions
        {
            From = today.AddDays(-13),
            To = today,
            PatientName = "Smoke Test Patient",
        };
        PdfReport.Generate(ctx, opts, outputPath);
        Console.WriteLine($"[smoke] PDF created at {outputPath}");
    }
}
