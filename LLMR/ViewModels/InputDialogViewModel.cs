using System;
using System.Reactive;
using ReactiveUI;

namespace LLMR.ViewModels;

public class InputDialogViewModel : ReactiveObject
{
    private string _title;
    private string _message;
    private string _userInput;

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public string UserInput
    {
        get => _userInput;
        set => this.RaiseAndSetIfChanged(ref _userInput, value);
    }

    public ReactiveCommand<Unit, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public event EventHandler<string> CloseRequested;

    public InputDialogViewModel()
    {
        OkCommand = ReactiveCommand.Create(() =>
        {
            CloseRequested?.Invoke(this, UserInput);
        });

        CancelCommand = ReactiveCommand.Create(() =>
        {
            CloseRequested?.Invoke(this, null);
        });
    }
}
