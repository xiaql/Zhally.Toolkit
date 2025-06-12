namespace Zhally.Toolkit.DragDrop;

public interface IDragDropPayload
{
    public View View { get; }                   // 拖放源/目标控件
    public object? Affix { get; }               // 任意附加数据（如文本、对象）
    public Action<View, object?>? Callback { get; }            // 拖放完成后的回调

    public View? Anchor { get; }
}


public class DragDropPayload<TView> : IDragDropPayload where TView : View
{
    public required TView View { get; init; }
    public object? Affix { get; init; }
    public Action<View, object?>? Callback { get; init; }
    public View? Anchor { get; set; } = null;

    View IDragDropPayload.View => View;
}

public interface IDragDropMessage
{
    public IDragDropPayload SourcePayload { get; }
    public IDragDropPayload TargetPayload { get; }
}

public sealed class DragDropMessage<TSource, TTarget> : IDragDropMessage
    where TSource : View
    where TTarget : View
{
    public required DragDropPayload<TSource> SourcePayload { get; init; }
    public required DragDropPayload<TTarget> TargetPayload { get; init; }

    IDragDropPayload IDragDropMessage.SourcePayload => SourcePayload;
    IDragDropPayload IDragDropMessage.TargetPayload => TargetPayload;
}