using System;
using System.IO;

namespace LLMR.Helpers;

public static class AppDataPath
{
    public static string GetBaseDataDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string baseDataDir = Path.Combine(appData, "LLMR", "Scripts");
        if (!Directory.Exists(baseDataDir))
        {
            Directory.CreateDirectory(baseDataDir);
        }
        return baseDataDir;
    }
}