using System;
using System.IO;
using LLMR.Model;

namespace LLMR.Helpers;

public static class PathManager
{
    //private static bool _combinedPathSet = false;
    private static bool _baseDirectorySet = false;
    //private static string _combinedPath = "";
    private static string _baseDir = "";
    
    public static string Combine(params string[] paths)
    {
        // if (_combinedPathSet)
        //     return _combinedPath;
        var combinedPath = Path.Combine(paths);
        // _combinedPath = combinedPath;
        // _combinedPathSet = true;
        ConsoleMessageManager.LogPathUsage(combinedPath);
        return combinedPath;
    }

    public static string GetBaseDirectory()
    {
        if (_baseDirectorySet)
            return _baseDir;
        var baseDir = AppContext.BaseDirectory;
        _baseDir = baseDir;
        _baseDirectorySet = true;
        ConsoleMessageManager.LogPathUsage(baseDir);
        return baseDir;
    }

    // add centralized path methods (& logging path issues in published versions for deployment debugging)
}