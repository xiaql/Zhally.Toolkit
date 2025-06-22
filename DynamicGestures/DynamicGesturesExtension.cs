using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Zhally.Toolkit.DynamicGestures;

public static class DynamicGesturesExtension
{
    // 使用 ConditionalWeakTable 避免内存泄漏
    private static readonly ConditionalWeakTable<View, IDragDropPayload> dragPayloads = [];
    private static readonly ConditionalWeakTable<GestureRecognizer, ConcurrentDictionary<string, IDragDropPayload>> dropPayloads = [];

    public static void AsPointerPerceptible<TSource>(this TSource source, Action<TSource>? entered = null, Action<TSource>? exited = null) where TSource : View
    {
        // 查找或创建 PointerGestureRecognizer
        var pointerGesture = source.GestureRecognizers.OfType<PointerGestureRecognizer>().FirstOrDefault();
        if (pointerGesture == null)
        {
            pointerGesture = new PointerGestureRecognizer();
            source.GestureRecognizers.Add(pointerGesture);

            // 只在首次注册时添加手势事件
            pointerGesture.PointerEntered += (sender, args) =>
            {
                if (entered != null)
                {
                    entered(source);
                }
                else
                {
                    source.BackgroundColor = Color.FromArgb("#864605");
                }
            };

            pointerGesture.PointerExited += (sender, args) =>
            {
                if (exited != null)
                {
                    exited(source);
                }
                else
                {
                    source.BackgroundColor = Colors.Transparent;
                }
            };

        }
    }

    public static void AsDraggable<TSource>(this TSource source, DragDropPayload<TSource> payload) where TSource : View
    {
        source.AsDraggable<TSource, TSource>(payload);
    }
    public static void AsDraggable<TSourceAnchor, TSource>(this TSourceAnchor anchor, DragDropPayload<TSource> payload)
        where TSourceAnchor : View
        where TSource : View
    {
        payload.Anchor = anchor;
        var source = payload.View;
        AttachDragGestureRecognizer(anchor, source);
        dragPayloads.AddOrUpdate(source, payload);  // 覆盖现有 payload（如果存在）
    }

    private static void AttachDragGestureRecognizer<TSourceAnchor, TSource>(TSourceAnchor anchor, TSource source)
        where TSourceAnchor : View
        where TSource : View
    {
        // 查找或创建 DragGestureRecognizer
        var dragGesture = anchor.GestureRecognizers.OfType<DragGestureRecognizer>().FirstOrDefault();
        if (dragGesture == null)
        {
            dragGesture = new DragGestureRecognizer { CanDrag = true };
            anchor.GestureRecognizers.Add(dragGesture);

            // 只在首次添加手势时注册事件
            dragGesture.DragStarting += (sender, args) =>
            {
                // 通过 dragPayloads 提取最新的 payload
                if (dragPayloads.TryGetValue(source, out var dragPayload) && dragPayload is DragDropPayload<TSource> payload)
                {
                    args.Data.Properties.Add("SourcePayload", payload);
                    anchor.Opacity = 0.5;
                }
            };
        }
    }

    public static void AsDroppable<TTarget>(this TTarget target, DragDropPayload<TTarget> payload) where TTarget : View
    {
        target.AsDroppable<View, TTarget>(payload);
    }

    public static void AsDroppable<TSource, TTarget>(this TTarget target, DragDropPayload<TTarget> payload)
        where TSource : View
        where TTarget : View
    {
        target.AsDroppable<TTarget, TSource, TTarget>(payload);
    }

    public static void AsDroppable<TTargetAnchor, TSource, TTarget>(this TTargetAnchor anchor, DragDropPayload<TTarget> payload)
        where TTargetAnchor : View
        where TSource : View
        where TTarget : View
    {
        var target = payload.View;
        var dropGesture = GetOrAttachDropGestureRecognizer(anchor, target);
        RegisterDropPayload<TSource, TTarget>(payload, dropGesture);
    }

    private static DropGestureRecognizer GetOrAttachDropGestureRecognizer<TTargetAnchor, TTarget>(TTargetAnchor anchor, TTarget target)
        where TTargetAnchor : View
        where TTarget : View
    {
        // 查找或创建 DropGestureRecognizer
        var dropGesture = anchor.GestureRecognizers.OfType<DropGestureRecognizer>().FirstOrDefault();
        if (dropGesture == null)
        {
            dropGesture = new DropGestureRecognizer { AllowDrop = true };
            anchor.GestureRecognizers.Add(dropGesture);

            DragDropPayload<TTarget> defaultPayload = new()
            {
                View = target,
                Affix = null,
                Callback = null
            };

            _ = dropPayloads
                .GetOrCreateValue(dropGesture)
                .GetOrAdd(typeof(View).Name, _ => defaultPayload);

            dropGesture.DragOver += (sender, args) =>
            {
                bool isSupported = args.Data.Properties.TryGetValue("SourcePayload", out _);
                anchor.BackgroundColor = isSupported ? Colors.LightGreen : Colors.Transparent;
            };

            dropGesture.DragLeave += (sender, args) =>
            {
                anchor.BackgroundColor = Colors.Transparent;
            };

            dropGesture.Drop += (s, e) =>
            {
                OnDroppablesMessage<TTarget>(target, dropGesture, e);
                anchor.Opacity = 1;
                anchor.BackgroundColor = Colors.Transparent;
            };

        }
        return dropGesture;
    }


    private static void RegisterDropPayload<TSource, TTarget>(DragDropPayload<TTarget> payload, DropGestureRecognizer dropGesture)
        where TSource : View
        where TTarget : View
    {
        TTarget target = payload.View;
        var payloadDict = dropPayloads.GetOrCreateValue(dropGesture);
        _ = payloadDict.AddOrUpdate(typeof(TSource).Name, (s) => payload, (s, old) => payload);
    }

    private static void OnDroppablesMessage<TTarget>(TTarget? target, DropGestureRecognizer dropGesture, DropEventArgs e)
     where TTarget : View
    {
        if (target is null || !e.Data.Properties.TryGetValue("SourcePayload", out var payloadObj))
        {
            return;
        }

        IDragDropPayload sourcePayload = (IDragDropPayload)payloadObj!;
        Type sourceType = sourcePayload.View.GetType();

        if (!dropPayloads.TryGetValue(dropGesture, out var payloadDict))
        {
            return;
        }

        // 尝试获取特定源类型的 payload
        if (!payloadDict.TryGetValue(sourceType.Name, out IDragDropPayload? targetPayload))
        {
            // 尝试获取默认 payload
            _ = payloadDict.TryGetValue(typeof(View).Name, out targetPayload);
        }

        if (targetPayload is null)
        {
            return;
        }

        // 构建泛型类型
        Type genericMessageType = typeof(DragDropMessage<,>);
        Type constructedMessageType = genericMessageType.MakeGenericType(sourceType, typeof(TTarget));

        // 创建实例
        object? message = Activator.CreateInstance(constructedMessageType);
        if (message is null)
        {
            return;
        }

        // 设置属性
        PropertyInfo sourceProp = constructedMessageType.GetProperty("SourcePayload")!;
        PropertyInfo targetProp = constructedMessageType.GetProperty("TargetPayload")!;
        sourceProp.SetValue(message, sourcePayload);
        targetProp.SetValue(message, targetPayload);

        // 核心动作
        _ = WeakReferenceMessenger.Default.Send<IDragDropMessage>((IDragDropMessage)message);

        // 视觉反馈
        sourcePayload.View.Opacity = 1;
        if (sourcePayload.Anchor is not null)
        {
            sourcePayload.Anchor.Opacity = 1;
        }
        target.BackgroundColor = Colors.Transparent;
    }

    public static void Undraggable<TSource>(this TSource source) where TSource : View
    {
        var dragGestureRecognizers = source.GestureRecognizers.OfType<DragGestureRecognizer>().ToList();

        foreach (var recognizer in dragGestureRecognizers)
        {
            _ = source.GestureRecognizers.Remove(recognizer);
        }
    }

    public static void Undroppable<TTarget>(this TTarget source) where TTarget : View
    {
        var dragGestureRecognizers = source.GestureRecognizers.OfType<DropGestureRecognizer>().ToList();

        foreach (var recognizer in dragGestureRecognizers)
        {
            _ = source.GestureRecognizers.Remove(recognizer);
        }
    }
}