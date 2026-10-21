// A clone form on a CommunityToolkit.Mvvm view model, as SourceGit's are:
// the view binds to it, the command clones in the background, and the
// status comes back to the UI thread.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mitsuamia;

Application.Run("br.com.roboport.mitsuamia.Clone", "Clone", () =>
{
    var vm = new CloneViewModel();

    var url = new TextInput { Placeholder = "https://", AccessibleName = "Repository URL" };
    Bind.Value(url, vm, nameof(vm.Url), () => vm.Url, v => vm.Url = v);

    var bare = new Checkbox("Bare repository");
    Bind.Checked(bare, vm, nameof(vm.Bare), () => vm.Bare, v => vm.Bare = v);

    var status = new Text { Style = TextStyle.Caption };
    Bind.OneWay(vm, nameof(vm.Status), () => vm.Status, v => status.Value = v);

    var clone = new Button("Clone") { Role = ButtonRole.Default, Command = vm.CloneCommand };

    var buttons = new Row { Justify = Justify.End };
    buttons.Add(clone);

    var form = new Column { Padding = 20, Gap = 10, AlignItems = Align.Stretch };
    form.Add(new Text("Clone a repository") { Style = TextStyle.Headline });
    form.Add(url);
    form.Add(bare);
    form.Add(status);
    form.Add(buttons);

    _ = new Window("Clone", 420, 220) { Content = form };
});

internal sealed partial class CloneViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloneCommand))]
    public partial string Url { get; set; } = "";

    [ObservableProperty]
    public partial bool Bare { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Enter a repository's URL.";

    private bool CanClone() => Url.Length > 0;

    [RelayCommand(CanExecute = nameof(CanClone))]
    private async Task Clone()
    {
        Status = $"Cloning {Url}{(Bare ? " (bare)" : "")}…";
        // Work off the UI thread, as `git clone` would be.
        await Task.Run(() => Thread.Sleep(1500));
        Status = "Done.";
    }
}
