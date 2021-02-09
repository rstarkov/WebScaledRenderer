using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RT.Util;

namespace WebScaledRenderer
{
    class Program
    {
        static string ChromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        static string OutputPath;
        static int ErrorCount = 0;

        static void Main(string[] args)
        {
            OutputPath = Directory.GetCurrentDirectory();

            var screens = new List<ScreenDesc>();

            // https://www.kylejlarson.com/blog/iphone-6-screen-size-web-design-tips/
            // https://developer.apple.com/library/archive/documentation/DeviceInformation/Reference/iOSDeviceCompatibility/Displays/Displays.html
            screens.Add(new ScreenDesc("iphone-5", 2.0m, 320, 460, 528));
            screens.Add(new ScreenDesc("iphone-678", 2.0m, 375, 559, 627));
            screens.Add(new ScreenDesc("iphone-678p", 2.608m, 414, 628, 696));
            screens.Add(new ScreenDesc("iphone-X", 3.0m, 375, 633, 701));
            screens.Add(new ScreenDesc("iphone-Xmax", 3.0m, 414, 717, 785));

            screens.Add(new ScreenDesc("android-1", 1.5m, 360, 568));
            screens.Add(new ScreenDesc("android-s8", 1.5m, 360, 617));
            screens.Add(new ScreenDesc("android-xperia", 2.0m, 360, 511));
            screens.Add(new ScreenDesc("android-xperia-kbd", 2.0m, 360, 268));
            screens.Add(new ScreenDesc("android-julia", 2.748m, 393, 658));
            screens.Add(new ScreenDesc("android-julia-kbd", 2.748m, 393, 368));
            screens.Add(new ScreenDesc("android-pixel2", 2.625m, 412, 604));
            screens.Add(new ScreenDesc("android-pixel4", 2.625m, 412, 769));

            screens.Add(new ScreenDesc("tablet-tab-s5", 2.25m, 712, 970));
            screens.Add(new ScreenDesc("tablet-tab-s3", 2m, 768, 904));
            screens.Add(new ScreenDesc("tablet-tab-s3-lscape", 2m, 1024, 648));
            screens.Add(new ScreenDesc("tablet-tab-4", 1m, 800, 1159));
            screens.Add(new ScreenDesc("tablet-ipad-mini2019", 2m, 768, 954));
            screens.Add(new ScreenDesc("tablet-ipad-air2019", 2m, 834, 1042));
            screens.Add(new ScreenDesc("tablet-ipad-pro2018", 2m, 1024, 1292));
            screens.Add(new ScreenDesc("tablet-ipad-pro2018-lscape", 2m, 1366, 950));

            screens.Add(new ScreenDesc("macbook-jo", 2.0m, 1280, 549));

            screens.Add(new ScreenDesc("desktop-roman", 1.5m, 1411, 905));
            screens.Add(new ScreenDesc("desktop-hi", 1.5m, 1707, 889));
            screens.Add(new ScreenDesc("desktop-lo", 1.0m, 1920, 950));
            screens.Add(new ScreenDesc("desktop-verylo", 1.0m, 1366, 668));

            //for (int w = 440; w <= 750; w += 62)
            //    for (int h = 500; h <= 900; h += 80)
            //        screens.Add(new ScreenDesc($"{w:000}x{h:000}", 2, w, h));

            var pages = new List<PageDesc>();
            var baseUrl = "https://en.wikipedia.org/wiki/Main_Page";
            pages.Add(new PageDesc("1main", $"{baseUrl}"));
            //pages.Add(new PageDesc("2customer", $"{baseUrl}#customer"));
            //pages.Add(new PageDesc("3retailer", $"{baseUrl}#retailer"));
            //pages.Add(new PageDesc("4contact", $"{baseUrl}#contact"));

            render(pages.SelectMany(_ => screens, (p, s) => (page: p, screen: s)).ToList());

            if (ErrorCount > 0)
            {
                Console.WriteLine($"ENCOUNTERED {ErrorCount} ERRORS");
                Console.ReadLine();
            }
        }

        private static void render(List<(PageDesc page, ScreenDesc screen)> ts)
        {
            var tasks = new ConcurrentQueue<(PageDesc page, ScreenDesc screen)>(ts);
            var start = DateTime.UtcNow;
            var threads = Enumerable.Range(0, 6).Select(_ => new Thread(() =>
            {
                while (true)
                {
                    if (!tasks.TryDequeue(out var task))
                        break;
                    Console.WriteLine($"{task.page.Name}--{task.screen.Name}: starting");
                    var pngName = Path.Combine(OutputPath, $"{task.page.Name}--{task.screen.CssWidth:0000},{task.screen.CssHeight:0000}--{task.screen.Name}.png");
                    if (File.Exists(pngName))
                        File.Delete(pngName);
                    try
                    {
                        var si = new ProcessStartInfo();
                        si.FileName = ChromePath;
                        si.Arguments = CommandRunner.ArgsToCommandLine(new[] { "--headless", "--disable-gpu",
                            $"--window-size={task.screen.CssWidth},{task.screen.CssHeight}",
                            //$"--force-device-scale-factor={task.screen.DpiZoom}",
                            $"--force-device-scale-factor={2}",
                            $"--screenshot={pngName}",
                            task.page.Url });
                        si.UseShellExecute = false;
                        var proc = new Process();
                        proc.StartInfo = si;
                        proc.Start();
                        proc.PriorityClass = ProcessPriorityClass.Idle;
                        proc.WaitForExit();
                        if (!File.Exists(pngName))
                            throw new Exception();
                        Console.WriteLine($"{task.page.Name}--{task.screen.Name}: success");
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{task.page.Name}--{task.screen.Name}: FAILED");
                        Interlocked.Increment(ref ErrorCount);
                        Console.ResetColor();
                    }
                }
            })).ToList();
            foreach (var t in threads)
                t.Start();
            foreach (var t in threads)
                t.Join();
            Console.WriteLine($"Time: {(DateTime.UtcNow - start).TotalSeconds} sec");
        }
    }

    class PageDesc
    {
        public string Name;
        public string Url;

        public PageDesc(string name, string url)
        {
            Name = name;
            Url = url;
        }
    }

    class ScreenDesc
    {
        public string Name;
        public decimal DpiZoom;
        public int CssWidth;
        public int CssHeight;
        public int? CssHeight2;

        public ScreenDesc(string name, decimal dpiZoom, int cssWidth, int cssHeight, int? cssHeight2 = null)
        {
            Name = name;
            DpiZoom = dpiZoom;
            CssWidth = cssWidth;
            CssHeight = cssHeight;
            CssHeight2 = cssHeight2;
        }
    }
}
