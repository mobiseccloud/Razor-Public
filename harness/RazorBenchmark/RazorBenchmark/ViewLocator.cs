using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RazorBenchmark.ViewModels;
using System;
using System.Collections.Generic;

namespace RazorBenchmark;

public sealed class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Func<Control>> _factories = new();

    public Control? Build(object? data)
    {
        if (data is null)
            return null;
        var type = data.GetType();
        if (_factories.TryGetValue(type, out var factory))
            return factory();
        return new TextBlock { Text = "No view for " + type.Name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
