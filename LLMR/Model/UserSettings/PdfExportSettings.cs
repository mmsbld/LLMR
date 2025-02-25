namespace LLMR.Model.UserSettings;

public class PdfExportSettings
{
    public bool ShowFileName { get; set; } = true;
    public bool ShowApiKey { get; set; } = true;
    public bool ShowFullApiKey { get; set; } = false;
    public bool ShowDownloadedOn { get; set; } = true;
    public bool ShowModelName { get; set; } = true;
    public bool ShowModelParameters { get; set; } = true;
}