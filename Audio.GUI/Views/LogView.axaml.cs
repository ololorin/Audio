using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using System;

namespace Audio.GUI.Views;

public partial class LogView : UserControl
{
    public static readonly DirectProperty<LogView, bool> IsScrollingProperty = AvaloniaProperty.RegisterDirect<LogView, bool>(nameof(IsScrolling), o => o.IsScrolling, (o, v) => o.IsScrolling = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private bool _isScrolling;
    private IDisposable? _dragDeltaEvent;
    private IDisposable? _dragCompletedEvent;

    public bool IsScrolling
    {
        get => _isScrolling;
        set => SetAndRaise(IsScrollingProperty, ref _isScrolling, value);
    }
    public LogView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _dragDeltaEvent = Thumb.DragDeltaEvent.AddClassHandler<LogView>((x, e) => x.OnThumbDragStarted(e), RoutingStrategies.Bubble);
        _dragCompletedEvent = Thumb.DragCompletedEvent.AddClassHandler<LogView>((x, e) => x.OnThumbDragCompleted(e), RoutingStrategies.Bubble);

        base.OnAttachedToLogicalTree(e);
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _dragDeltaEvent?.Dispose();
        _dragCompletedEvent?.Dispose();

        base.OnDetachedFromLogicalTree(e);
    }

    protected void OnThumbDragStarted(VectorEventArgs e)
    {
        IsScrolling = true;
    }
    protected void OnThumbDragCompleted(VectorEventArgs e)
    {
        IsScrolling = false;
    }
}
