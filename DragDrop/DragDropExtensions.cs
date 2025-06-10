using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Zhally.Toolkit.DragDrop;

public static class DragDropExtensions
{
    // 使用 ConditionalWeakTable 避免内存泄漏
    private static readonly ConditionalWeakTable<View, IDragDropPayload> dragPayloads = [];
    private static readonly ConditionalWeakTable<GestureRecognizer, ConcurrentDictionary<string, IDragDropPayload>> dropPayloads = [];

    public static void AsDraggable<TSource>(this TSource source, object? sourceAffix = null, Action? sourceCallback = null)
        where TSource : View
    {
        // 创建并存储 payload
        var payload = new DragDropPayload<TSource>
        {
            View = source,
            Affix = sourceAffix,
            Callback = sourceCallback
        };

        // 覆盖现有 payload（如果存在）
        dragPayloads.AddOrUpdate(source, payload);

        // 查找或创建 DragGestureRecognizer
        var dragGesture = source.GestureRecognizers.OfType<DragGestureRecognizer>().FirstOrDefault();
        if (dragGesture == null)
        {
            dragGesture = new DragGestureRecognizer { CanDrag = true };
            source.GestureRecognizers.Add(dragGesture);

            // 只在首次添加手势时注册事件
            dragGesture.DragStarting += (sender, args) =>
            {
                // 通过 dragPayloads 提取最新的 payload
                if (dragPayloads.TryGetValue(source, out var dragPayload) && dragPayload is DragDropPayload<TSource> payload)
                {
                    args.Data.Properties.Add("SourcePayload", payload);
                    source.Opacity = 0.5;
                }
            };
        }
    }

    public static void AsDroppable<TTarget>(this TTarget target, object? targetAffix = null, Action? targetCallback = null)
        where TTarget : View
    {
        AsDroppable<View, TTarget>(target, targetAffix, targetCallback);
    }

    public static void AsDroppable<TSource, TTarget>(this TTarget target, object? targetAffix = null, Action? targetCallback = null)
        where TSource : View
        where TTarget : View
    {
        var dropGesture = target.GestureRecognizers.OfType<DropGestureRecognizer>().FirstOrDefault();
        if (dropGesture is null)
        {
            dropGesture = new DropGestureRecognizer() { AllowDrop = true };
            target.GestureRecognizers.Add(dropGesture);

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
                target.BackgroundColor = isSupported ? Colors.LightGreen : Colors.Transparent;
            };

            dropGesture.DragLeave += (sender, args) =>
            {
                target.BackgroundColor = Colors.Transparent;
            };

            dropGesture.Drop += (s, e) => OnDroppablesMessage<TTarget>(target, dropGesture, e);
        }

        DragDropPayload<TTarget> sourceSpecificDropPayload = new()
        {
            View = target,
            Affix = targetAffix,
            Callback = targetCallback
        };

        var payloadDict = dropPayloads.GetOrCreateValue(dropGesture);
        _ = payloadDict.AddOrUpdate(typeof(TSource).Name, (s) => sourceSpecificDropPayload, (s, old) => sourceSpecificDropPayload);
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
        target.BackgroundColor = Colors.Transparent;
    }
}