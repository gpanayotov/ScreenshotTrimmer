using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ScreenshotTrimmer.App;

/// <summary>
/// A small modal window asking one question with a row of answers. The answer <c>default(T)</c> leaves everything as it
/// was: Escape chooses it, and closing the window without choosing gives it.
/// </summary>
public sealed class QuestionWindow<T> : Window where T : struct
{
    /// <param name="title">The window's title.</param>
    /// <param name="question">The question, which may wrap over several lines.</param>
    /// <param name="answers">The answers, left to right; the first is the default for Enter.</param>
    public QuestionWindow(string title, string question, IReadOnlyList<(string Label, T Answer)> answers)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
        };
        for (var i = 0; i < answers.Count; i++)
        {
            var (label, answer) = answers[i];
            var button = new Button
            {
                Content = label,
                IsDefault = i == 0,
                IsCancel = EqualityComparer<T>.Default.Equals(answer, default),
                MinWidth = 90,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            button.Click += (_, _) => Close(answer);
            buttons.Children.Add(button);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = question, TextWrapping = TextWrapping.Wrap },
                buttons,
            },
        };

        Opened += (_, _) => (buttons.Children.FirstOrDefault() as Button)?.Focus();
    }

    /// <summary>Shows the question over <paramref name="owner"/> and waits for the answer.</summary>
    public async Task<T> AskAsync(Window owner) => await ShowDialog<T?>(owner) ?? default;
}
