// using System.Collections.ObjectModel;
// using ReactiveUI;
//
// namespace LLMRH_v2.Models;
//
// public class ModelSettings:ReactiveObject
// {
//     private string? _generatedLocalLink;
//     private string? _generatedPublicLink;
//
//     public ObservableCollection<string> AvailableModels { get; set; } = new ObservableCollection<string>();
//
//     public string? SelectedModel { get; set; }
//
//     public double Temperature { get; set; } = 0.7;
//
//     public int MaxTokens { get; set; } = 100;
//
//     public double TopP { get; set; } = 1.0;
//
//     public double FrequencyPenalty { get; set; } = 0.0;
//
//     public double PresencePenalty { get; set; } = 0.0;
//
//     public string? GeneratedLocalLink
//     {
//         get => _generatedLocalLink;
//         set => this.RaiseAndSetIfChanged(ref _generatedLocalLink, value);
//     }
//
//     public string? GeneratedPublicLink
//     {
//         get => _generatedPublicLink;
//         set => this.RaiseAndSetIfChanged(ref _generatedPublicLink, value);
//     }
// }