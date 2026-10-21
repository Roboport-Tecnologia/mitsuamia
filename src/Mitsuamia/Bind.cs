using System.ComponentModel;

namespace Mitsuamia;

/// <summary>
/// Binds view model properties (<see cref="INotifyPropertyChanged"/>) to
/// nodes. Getters and setters are lambdas, as compiled bindings are, so
/// nothing is looked up by reflection: <c>Bind.OneWay(vm, nameof(vm.Title),
/// () => vm.Title, v => text.Value = v)</c>.
/// </summary>
public static class Bind
{
    /// <summary>Shows the property now and whenever it changes. A change
    /// raised on another thread is shown on the UI thread.</summary>
    public static void OneWay<T>(INotifyPropertyChanged source, string property, Func<T> get, Action<T> show)
    {
        show(get());
        source.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != property) return;
            if (Application.IsUiThread) show(get());
            else Application.Post(() => show(get()));
        };
    }

    /// <summary>A text field's text, both ways.</summary>
    public static void Value(TextInput input, INotifyPropertyChanged source, string property, Func<string?> get,
        Action<string> set)
    {
        OneWay(source, property, get, v => input.Value = v ?? "");
        input.Changed += set;
    }

    /// <summary>A checkbox's state, both ways.</summary>
    public static void Checked(Checkbox box, INotifyPropertyChanged source, string property, Func<bool> get,
        Action<bool> set)
    {
        OneWay(source, property, get, v => box.IsChecked = v);
        box.Changed += set;
    }
}
