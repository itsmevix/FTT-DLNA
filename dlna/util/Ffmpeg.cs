﻿using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NMaier.SimpleDlna.Utilities
{
  using InfoCache = LeastRecentlyUsedDictionary<FileInfo, IDictionary<string, string>>;

  public static class FFmpeg
  {
    private static DirectoryInfo GetFFMpegFolder(
      Environment.SpecialFolder folder)
    {
      return new DirectoryInfo(
        Path.Combine(Environment.GetFolderPath(folder), "ffmpeg"));
    }

    private static readonly DirectoryInfo[] specialLocations = new DirectoryInfo[] {
      GetFFMpegFolder(Environment.SpecialFolder.CommonProgramFiles),
      GetFFMpegFolder(Environment.SpecialFolder.CommonProgramFilesX86),
      GetFFMpegFolder(Environment.SpecialFolder.ProgramFiles),
      GetFFMpegFolder(Environment.SpecialFolder.ProgramFilesX86),
      GetFFMpegFolder(Environment.SpecialFolder.UserProfile),
      new DirectoryInfo(Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile))
    };

    private readonly static InfoCache infoCache = new InfoCache(500);

    private static readonly Regex regAssStrip =
      new Regex(@"^,+", RegexOptions.Compiled);

    private static readonly Regex regDuration = new Regex(
      @"Duration: ([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]+))?",
      RegexOptions.Compiled);

    private static readonly Regex regDimensions = new Regex(
      @"Video: .+ ([0-9]{2,})x([0-9]{2,}) ", RegexOptions.Compiled);

    public static readonly string FFmpegExecutable =
      FindExecutable("ffmpeg");

    private static string FindExecutable(string executable)
    {
      var os = Environment.OSVersion.Platform.ToString().ToUpperInvariant();
      var isWin = os.Contains("WIN");
      if (isWin) {
        executable += ".exe";
      }
      var places = new List<DirectoryInfo>();
      places.Add(new FileInfo(
        Assembly.GetExecutingAssembly().Location).Directory);
      try {
        places.Add(new DirectoryInfo(
          Environment.GetEnvironmentVariable("FFMPEG_HOME")));
      }
      catch (Exception) {
      }
      foreach (var l in specialLocations) {
        try {
          places.Add(l);
        }
        catch (Exception) {
          continue;
        }
      }
      foreach (var p in Environment.GetEnvironmentVariable("PATH").
        Split(isWin ? ';' : ':')) {
        try {
          places.Add(new DirectoryInfo(p.Trim()));
        }
        catch (Exception) {
          continue;
        }
      }

      foreach (var i in places) {
        LogManager.GetLogger(typeof(FFmpeg)).DebugFormat(
          "Searching {0}", i.FullName);
        if (!i.Exists) {
          continue;
        }
        var folders = new[] {
          i,
          new DirectoryInfo(Path.Combine(i.FullName, "bin"))
        };
        foreach (var di in folders) {
          try {
            var r = di.GetFiles(executable, SearchOption.TopDirectoryOnly);
            if (r.Length != 0) {
              var rv = r[0];
              LogManager.GetLogger(typeof(FFmpeg)).InfoFormat(
                "Found {0} at {1}",
                executable,
                rv.FullName
              );
              return rv.FullName;
            }
          }
          catch (Exception) {
            continue;
          }
        }
      }
      LogManager.GetLogger(typeof(FFmpeg)).WarnFormat(
        "Did not find {0}", executable);
      return null;
    }

    private static IDictionary<string, string> IdentifyFileInternal(
      FileInfo file)
    {
      if (FFmpeg.FFmpegExecutable == null) {
        throw new NotSupportedException();
      }
      if (file == null) {
        throw new ArgumentNullException("file");
      }
      IDictionary<string, string> rv;
      if (infoCache.TryGetValue(file, out rv)) {
        return rv;
      }
      try {
        return IdentifyInternalFromProcess(file);
      }
      catch (Exception ex) {
        throw new NotSupportedException(ex.Message, ex);
      }
    }

    private static IDictionary<string, string> IdentifyInternalFromProcess(
      FileInfo file)
    {
      IDictionary<string, string> rv;

      using (var p = new Process()) {
        var sti = p.StartInfo;
#if !DEBUG
        sti.CreateNoWindow = true;
#endif
        sti.UseShellExecute = false;
        sti.FileName = FFmpeg.FFmpegExecutable;
        sti.Arguments = String.Format("-i \"{0}\"", file.FullName);
        sti.LoadUserProfile = false;
        sti.RedirectStandardError = true;
        p.Start();
        rv = new Dictionary<string, string>();

        using (var reader = new StreamReader(new MemoryStream())) {
          using (var pump = new StreamPump(
            p.StandardError.BaseStream, reader.BaseStream, 4096)) {
            pump.Pump(null);
            if (!p.WaitForExit(3000)) {
              throw new NotSupportedException("ffmpeg timed out");
            }
            if (!pump.Wait(1000)) {
              throw new NotSupportedException("ffmpeg pump timed out");
            }
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            var output = reader.ReadToEnd();
            var match = regDuration.Match(output);
            if (match != null && match.Success) {
              int h, m, s, ms;
              if (int.TryParse(match.Groups[1].Value, out h) &&
                int.TryParse(match.Groups[2].Value, out m) &&
                int.TryParse(match.Groups[3].Value, out s)) {
                if (match.Groups.Count < 5 ||
                    !int.TryParse(match.Groups[4].Value, out ms)) {
                  ms = 0;
                }
                var ts = new TimeSpan(0, h, m, s, ms * 10);
                var tss = ts.TotalSeconds.ToString(
                  CultureInfo.InvariantCulture);
                rv.Add("LENGTH", tss);
              }
            }
            match = regDimensions.Match(output);
            if (match != null && match.Success) {
              int w, h;
              if (int.TryParse(match.Groups[1].Value, out w) &&
                int.TryParse(match.Groups[2].Value, out h)) {
                rv.Add("VIDEO_WIDTH", w.ToString());
                rv.Add("VIDEO_HEIGHT", h.ToString());
              }
            }
          }
        }
        if (rv.Count == 0) {
          throw new NotSupportedException("File not supported");
        }
        return rv;
      }
    }

    public static Size GetFileDimensions(FileInfo file)
    {
      string sw, sh;
      int w, h;
      if (IdentifyFile(file).TryGetValue("VIDEO_WIDTH", out sw)
        && IdentifyFile(file).TryGetValue("VIDEO_HEIGHT", out sh)
        && int.TryParse(sw, out w)
        && int.TryParse(sh, out h)
        && w > 0 && h > 0) {
        return new Size(w, h);
      }
      throw new NotSupportedException();
    }

    public static double GetFileDuration(FileInfo file)
    {
      string sl;
      if (FFmpeg.IdentifyFile(file).TryGetValue("LENGTH", out sl)) {
        double dur;
        bool valid = Double.TryParse(
          sl, NumberStyles.AllowDecimalPoint,
          CultureInfo.GetCultureInfo("en-US", "en"), out dur);
        if (valid && dur > 0) {
          return dur;
        }
      }
      throw new NotSupportedException();
    }

    public static string GetSubtitleSubrip(FileInfo file)
    {
      if (FFmpeg.FFmpegExecutable == null) {
        throw new NotSupportedException();
      }
      if (file == null) {
        throw new ArgumentNullException("file");
      }
      try {
        using (var p = new Process()) {
          var sti = p.StartInfo;
#if !DEBUG
          sti.CreateNoWindow = true;
#endif
          sti.UseShellExecute = false;
          sti.FileName = FFmpeg.FFmpegExecutable;
          sti.Arguments = String.Format(
            "-i \"{0}\" -map s:0 -f srt pipe:", file.FullName);
          sti.LoadUserProfile = false;
          sti.RedirectStandardOutput = true;
          p.Start();

          using (var reader = new StreamReader(new MemoryStream())) {
            using (var pump = new StreamPump(
              p.StandardOutput.BaseStream, reader.BaseStream, 40960)) {
              pump.Pump(null);
              if (!p.WaitForExit(10000)) {
                throw new NotSupportedException("ffmpeg timed out");
              }
              if (!pump.Wait(2000)) {
                throw new NotSupportedException("ffmpeg pump timed out");
              }
              reader.BaseStream.Seek(0, SeekOrigin.Begin);

              var rv = string.Empty;
              string line;
              while ((line = reader.ReadLine()) != null) {
                rv += regAssStrip.Replace(line.Trim(), string.Empty) + "\n";
              }
              if (!string.IsNullOrWhiteSpace(rv)) {
                return rv;
              }
            }
          }
        }
      }
      catch (Exception ex) {
        throw new NotSupportedException(ex.Message, ex);
      }
      throw new NotSupportedException(
        "File does not contain a valid subtitle");
    }

    public static IDictionary<string, string> IdentifyFile(FileInfo file)
    {
      if (FFmpeg.FFmpegExecutable != null) {
        return IdentifyFileInternal(file);
      }
      throw new NotSupportedException();
    }
  }
}
