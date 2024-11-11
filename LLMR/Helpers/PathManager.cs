using System;
using System.IO;

namespace LLMR.Helpers;

public static class PathManager
{
    public static string Combine(params string[] paths)
    {
        var combinedPath = Path.Combine(paths);
        ConsoleMessageManager.LogPathUsage(combinedPath);
        return combinedPath;
    }

    public static string GetBaseDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        ConsoleMessageManager.LogPathUsage(baseDir);
        return baseDir;
    }

    // add centralized path methods (& logging path issues in published versions for deployment debugging)
}