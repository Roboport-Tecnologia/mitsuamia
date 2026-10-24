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
    /// <remarks>Pass the node it shows on as <paramref name="owner"/> when
    /// the source outlives it, as a list row's item does: the binding
    /// ends with the node.</remarks>
    public static void OneWay<T>(INotifyPropertyChanged source, string property, Func<T> get, Action<T> show,
        Node? owner = null)
    {
        show(get());
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != property) return;
            if (Application.IsUiThread) show(get());
            else Application.Post(() => show(get()));
        };
        source.PropertyChanged += handler;
        if (owner is not null) owner.Forgotten += () => source.PropertyChanged -= handler;
    }

    /// <summary>A text field's text, both ways.</summary>
    public static void Value(TextField input, INotifyPropertyChanged source, string property, Func<string?> get,
        Action<string> set, Node? owner = null)
    {
        OneWay(source, property, get, v => input.Value = v ?? "", owner ?? input);
        input.Changed += set;
    }

    /// <summary>A search field's text, both ways: the property follows the
    /// field when the platform searches, not at every keystroke.</summary>
    public static void Search(SearchInput input, INotifyPropertyChanged source, string property, Func<string?> get,
        Action<string> set, Node? owner = null)
    {
        OneWay(source, property, get, v => input.Value = v ?? "", owner ?? input);
        input.Search += set;
    }

    /// <summary>A checkbox's state, both ways.</summary>
    public static void Checked(Checkbox box, INotifyPropertyChanged source, string property, Func<bool> get,
        Action<bool> set, Node? owner = null)
    {
        OneWay(source, property, get, v => box.IsChecked = v, owner ?? box);
        box.Changed += set;
    }
}
