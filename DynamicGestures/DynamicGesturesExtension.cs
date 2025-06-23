using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Zhally.Toolkit.DynamicGestures;


public static class DynamicGesturesExtension
{
    // 使用 ConditionalWeakTable 避免内存泄漏
    private static readonly ConditionalWeakTable<GestureRecognizer, ConcurrentDictionary<string, IDragDropPayload>> dropPayloads = [];

    private static readonly ConditionalWeakTable<GuidToken, IDragDropPayload> dragPayloads = [];
    private static readonly ConcurrentDictionary<string, GuidToken> tempDragStrongReferences = new();
    private static readonly ConcurrentBag<WeakReference<GuidToken>> guidTokens = [];

    public static void Cleanup()
    {
        var deadTokens = guidTokens.Where(t => !t.TryGetTarget(out _)).ToList();
        foreach (var deadToken in deadTokens)
        {
            _ = guidTokens.TryTake(out _);        // 移除已被GC回收的Token引用
        }
    }
    public sealed class GuidToken
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Token => Id.ToString();
    }

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

    public static void AsDraggable<TSource>(this TSource source, Func<TSource, DragDropPayload<TSource>> payloadCreatorr) where TSource : View
    {
        source.AsDraggable<TSource, TSource>(source, (source, _) => payloadCreatorr(source));
    }
    public static void AsDraggable<TSourceAnchor, TSource>(this TSourceAnchor anchor, TSource source, Func<TSourceAnchor, TSource, DragDropPayload<TSource>> payloadCreator)
        where TSourceAnchor : View
        where TSource : View
    {
        AttachDragGestureRecognizer(anchor, source, payloadCreator); // 覆盖现有 payload（如果存在）
    }

    private static void AttachDragGestureRecognizer<TSourceAnchor, TSource>(TSourceAnchor anchor, TSource source, Func<TSourceAnchor, TSource, DragDropPayload<TSource>> payloadCreator)
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
            GuidToken dragPayloadToken = new();
            guidTokens.Add(new WeakReference<GuidToken>(dragPayloadToken));
            dragGesture.DragStarting += (sender, args) =>
            {
                DragDropPayload<TSource> dragPayload = payloadCreator(anchor, source);
                dragPayloads.Add(dragPayloadToken, dragPayload);
                tempDragStrongReferences[dragPayloadToken.Token] = dragPayloadToken;

                args.Data.Text = dragPayloadToken.Token;
                anchor.Opacity = 0.5;
            };

            dragGesture.DropCompleted += (sender, args) =>
            {
                _ = tempDragStrongReferences.TryRemove(dragPayloadToken.Token, out _);
            };
        }
    }

    private static bool RemoveDragPayloadReference(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        if (tempDragStrongReferences.TryGetValue(token, out var guilToken))
        {
            _ = tempDragStrongReferences.TryRemove(token, out _);

            return dragPayloads.Remove(guilToken);
        }

        // 如果强引用已移除，尝试遍历弱引用表查找并移除
        foreach (var wref in guidTokens)
        {
            if (wref.TryGetTarget(out var guidToken) && guidToken.Token == token)
            {
                return dragPayloads.Remove(guidToken);
            }
        }

        return false;
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

            dropGesture.DragOver += (sender, e) =>
            {
                string token = e.Data.Text;
                bool isSupported = tempDragStrongReferences.TryGetValue(token, out _);
                anchor.BackgroundColor = isSupported ? Colors.LightGreen : Colors.Transparent;
            };

            dropGesture.DragLeave += (sender, args) =>
            {
                anchor.BackgroundColor = Colors.Transparent;
            };

            dropGesture.Drop += async (s, e) =>
            {
                await OnDroppablesMessageAsync<TTarget>(target, dropGesture, e);
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

    private static async Task OnDroppablesMessageAsync<TTarget>(TTarget? target, DropGestureRecognizer dropGesture, DropEventArgs e)
     where TTarget : View
    {
        string token = await e.Data.GetTextAsync();
        bool isSourcePayload = tempDragStrongReferences.TryGetValue(token, out var guidToken);
        if (target is null || !isSourcePayload)
        {
            return;
        }

        _ = dragPayloads.TryGetValue(guidToken!, out IDragDropPayload? sourcePayload);
        if (sourcePayload is null)
        {
            return;
        }
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

        _ = RemoveDragPayloadReference(token);
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