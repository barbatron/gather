using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace WinResize
{
  [StructLayout(LayoutKind.Sequential)]
  public struct RECT
  {
    public int Left;        // x position of upper-left corner
    public int Top;         // y position of upper-left corner
    public int Right;       // x position of lower-right corner
    public int Bottom;      // y position of lower-right corner
    public int Width { get { return Right - Left; } }
    public int Height { get { return Bottom - Top; } }
    public bool IsMinimized { get { return Left < 0 && Top < 0; } }
    public Rectangle toRectangle() { return new Rectangle(Left, Top, Right - Left, Bottom - Top); }
  }

  public static class RectHelper
  {
    public static String format(this System.Drawing.Rectangle r)
    {
      return $"X={r.X}/{r.Left} Y={r.Y}/{r.Top} W={r.Width} H={r.Height}";
    }
    public static bool isMinimized(this RECT rectangle)
    {
      return rectangle.Left < -1000 && rectangle.Top < -1000;
    }
  }

  class Program
  {
    static int SW_SHOWNORMAL = 1;
    static int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int X);

    [DllImport("user32.dll")]
    public static extern bool SetFocus(IntPtr hWnd);



    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);



    static int Main(string[] args)
    {
      Console.WriteLine(DateTime.Now);
      Console.Title = "Gather Windows";

      //Read parameters from command line
      if (args.Count() == 0)
      {
        var exeName = Process.GetCurrentProcess().MainModule.ModuleName;
        Console.WriteLine($"Usage:");
        Console.WriteLine();
        Console.WriteLine($"  {exeName} ls           - List screens");
        Console.WriteLine($"  {exeName} from 1,2     - Gather windows from screens 1+2 to primary screen");
        Console.WriteLine($"  {exeName} from 2 to 0  - Gather windows from screen 2 to screen 0");
        Console.WriteLine($"  {exeName} to 1         - Gather windows from all screens to screen 1");
        Console.WriteLine($"  {exeName} to p[rimary] - Gather windows from all screens to primary screen");
        Console.WriteLine();
        return 0;
      }

      var argi = 0;
      string command = args[argi];

      if (command == "ls")
      {
        var screens = Screen.AllScreens.ToList();
        Console.WriteLine("#   Device name");
        Console.WriteLine("--- -----------");
        screens.ForEach(s => Console.WriteLine($"{screens.IndexOf(s) + 1,-4}${s.DeviceName}{(Screen.PrimaryScreen == s ? " (primary)" : String.Empty)}"));
        return 0;
      }

      List<Screen> fromScreens = new List<Screen>();
      Screen toScreen = Screen.PrimaryScreen;
      List<String> nameFilter = new List<string>();

      Predicate<String> matchesFilter = (String name) => nameFilter.Count == 0 || nameFilter.Any(f => name.Contains(f, StringComparison.CurrentCultureIgnoreCase));

      Func<String, List<Screen>> parseParam = (String s) => s.Split(",")
        .Select(str => str.StartsWith("p") ? Screen.PrimaryScreen : Screen.AllScreens[int.Parse(str) - 1])
        .ToList();

      while (argi < args.Length)
      {
        var option = args[argi++];
        var param = args[argi++];
        switch (option)
        {
          case "from":
            fromScreens = fromScreens.Concat(parseParam(param)).Distinct().ToList<Screen>();
            break;
          case "to":
            var thing = parseParam(param);
            if (thing.Count > 1)
            {
              Console.Error.WriteLine("Cannot specify multiple screens to move to");
              return 1;
            }
            toScreen = parseParam(param).First();
            break;
          case "proc":
            nameFilter = nameFilter.Concat(param.Split(",")).Distinct().ToList<String>();
            break;
        }
      }

      if (fromScreens.Count == 0)
      {
        fromScreens = new List<Screen>(Screen.AllScreens);
      }

      var procs = Process.GetProcesses()
        // Filter to names/pids
        .Where(p => matchesFilter(p.ProcessName) || matchesFilter(p.Id.ToString()))
        // Filter to windows on "from" screens
        .Where(p =>
        {
          IntPtr handle = p.MainWindowHandle;
          Screen procScreen = Screen.FromHandle(handle);
          RECT rt = new RECT();
          return fromScreens.Contains(procScreen) && GetWindowRect(handle, out rt);
        })
        .ToList<Process>();

      if (procs.Count == 0)
      {
        Console.WriteLine("No windows found to move");
        return 0;
      }

      Console.WriteLine($"Gathering windows from screens {String.Join(",", fromScreens.Select(s => s.DeviceName))} -> {toScreen.DeviceName}");
      Console.WriteLine($"Target screen bounds: " + toScreen.Bounds.format());
      Console.WriteLine($"Matching processes: ", String.Join(", ", procs.Select(p => p.ProcessName).ToArray()));

      int targX = toScreen.WorkingArea.Left,
          targY = toScreen.WorkingArea.Top;

      foreach (Process proc in procs)
      {
        IntPtr handle = proc.MainWindowHandle;
        Screen procScreen = Screen.FromHandle(handle);

        try
        {
          Console.WriteLine($"{proc.ProcessName} ({proc.Id})".PadRight(40));
          if (procScreen.DeviceName == toScreen.DeviceName)
          {
            Console.WriteLine($"  Skipping - already in target screen");
            continue;
          }

          // Console.WriteLine("  procScreen Bounds: " + procScreen.Bounds.format());
          // Console.WriteLine("  procScreen WA    : " + procScreen.WorkingArea.format());

          RECT rt = new RECT();
          if (!GetWindowRect(handle, out rt))
          {
            Console.WriteLine("  Unable to get window rect - skipping");
            continue;
          }
          else
          {
            Console.WriteLine("  Original rect: " + rt.toRectangle().format());
          }

          var minimized = (rt.Left < 0 && rt.Top < 0);
          if (minimized)
          {
            Console.WriteLine("  Window is minimized - restoring to measure...");
            ShowWindow(handle, SW_RESTORE);
            if (!GetWindowRect(handle, out rt))
            {
              Console.WriteLine("  Unable to read rect!");
            }
            if (rt.IsMinimized)
            {
              Console.WriteLine("  Still minimized - falling back to fullscreen on target");
              rt.Left = procScreen.Bounds.Left;
              rt.Top = procScreen.Bounds.Top;
              rt.Right = toScreen.WorkingArea.Width;
              rt.Bottom = toScreen.WorkingArea.Height;
            }
          }

          var winRect = System.Drawing.Rectangle.FromLTRB(rt.Left, rt.Top, rt.Right, rt.Bottom);
          Console.WriteLine("  Window rect: " + winRect.format());

          var toRect = new Rectangle(
            winRect.X - procScreen.Bounds.X + toScreen.Bounds.X,
            winRect.Y - procScreen.Bounds.Y + toScreen.Bounds.Y,
            winRect.Width,
            winRect.Height);

          Console.WriteLine("  Target rect: " + toRect.format());

          MoveWindow(handle, toRect.X, toRect.Y, toRect.Width, toRect.Height, true);
          SetFocus(handle);
        }
        catch (Exception ex)
        {
          Console.WriteLine();
          Console.Error.WriteLine(ex);
          return 2;
        }
      }

      return 0;
    }

  }
}