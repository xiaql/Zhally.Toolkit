using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Zhally.Toolkit.DynamicGestures;


public static class DynamicGesturesExtension
{
    private static readonly ConditionalWeakTable<GestureRecognizer, GuidToken> guidTokens = [];
    private static readonly ConditionalWeakTable<GuidToken, DragDropPayload> dragDropPayloads = [];
    private static readonly ConcurrentDictionary<string, WeakReference<GuidToken>> tokenCache = new();

    private static string RegisterPayload(this GestureRecognizer recognizer, DragDropPayload payload)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        ArgumentNullException.ThrowIfNull(payload);

        var guidToken = guidTokens.GetOrCreateValue(recognizer);

        dragDropPayloads.AddOrUpdate(guidToken, payload);

        tokenCache[guidToken.Token] = new WeakReference<GuidToken>(guidToken);

        return guidToken.Token;
    }

    public static bool TryAssociatedPayload(this string token, [NotNullWhen(true)] out DragDropPayload? payload)
    {
        payload = null;
        if (!token.IsValidGuid())
        {
            return false;
        }

        if (tokenCache.TryGetValue(token, out var weakGuidToken) &&
            weakGuidToken.TryGetTarget(out var guidToken) &&
            dragDropPayloads.TryGetValue(guidToken, out payload))
        {
            return true;
        }

        _ = tokenCache.TryRemove(token, out _);        // 尝试清理缓存
        return false;
    }

    public static void RemovePayload(this string token)
    {
        if (!token.IsValidGuid() || !tokenCache.TryGetValue(token, out var weakToken))
        {
            return;
        }

        if (weakToken.TryGetTarget(out var guidToken))
        {
            _ = dragDropPayloads.Remove(guidToken);
        }

        _ = tokenCache.TryRemove(token, out _);
    }



    public sealed class GuidToken
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Token => Id.ToString();
    }
    private static bool IsValidGuid(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        return Guid.TryParse(input, out _);
    }
    public static void AsPointerPerceptible<TSource>(this TSource source, Action<TSource>? entered = null, Action<TSource>? exited = null) where TSource : View
    {

        var pointerGesture = source.GestureRecognizers.OfType<PointerGestureRecognizer>().FirstOrDefault();
        if (pointerGesture == null)
        {
            pointerGesture = new PointerGestureRecognizer();
            source.GestureRecognizers.Add(pointerGesture);

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

    public static void AsDraggable<TSource>(this TSource source, Func<TSource, DragDropPayload> payloadCreatorr) where TSource : View
    {
        source.AsDraggable<TSource, TSource>(source, (source, _) => payloadCreatorr(source));
    }
    public static void AsDraggable<TSourceAnchor, TSource>(this TSourceAnchor anchor, TSource source, Func<TSourceAnchor, TSource, DragDropPayload> payloadCreator)
        where TSourceAnchor : View
        where TSource : View
    {
        AttachDragGestureRecognizer(anchor, source, payloadCreator); // 覆盖现有 payload（如果存在）
    }

    private static void AttachDragGestureRecognizer<TSourceAnchor, TSource>(TSourceAnchor anchor, TSource source, Func<TSourceAnchor, TSource, DragDropPayload> payloadCreator)
        where TSourceAnchor : View
        where TSource : View
    {
        anchor.Undraggable();
        DragGestureRecognizer dragGesture = new() { CanDrag = true };
        anchor.GestureRecognizers.Add(dragGesture);

        dragGesture.DragStarting += (sender, args) =>
        {
            DragDropPayload dragPayload = payloadCreator(anchor, source);
            _ = dragGesture.RegisterPayload(dragPayload);

            args.Data.Text = guidTokens.GetOrCreateValue(dragGesture).Token;
            anchor.Opacity = 0.5;
        };

        dragGesture.DropCompleted += (sender, args) =>
        {
            guidTokens.GetOrCreateValue(dragGesture).Token.RemovePayload();
        };
    }

    public static void AsDroppable<TTarget>(this TTarget target, DragDropPayload payload) where TTarget : View
    {
        target.AsDroppable<TTarget, TTarget>(payload);
    }


    public static void AsDroppable<TTargetAnchor, TTarget>(this TTargetAnchor anchor, DragDropPayload payload)
        where TTargetAnchor : View
        where TTarget : View
    {
        anchor.Undroppable();
        DropGestureRecognizer dropGesture = new() { AllowDrop = true };
        anchor.GestureRecognizers.Add(dropGesture);
        _ = dropGesture.RegisterPayload(payload);

        dropGesture.DragOver += (sender, e) =>
        {
            string token = e.Data.Text;

            if (token.TryAssociatedPayload(out DragDropPayload? dragPayload) &&
                guidTokens.TryGetValue(dropGesture, out GuidToken? dropToken) && dropToken is not null &&
                dropToken.Token.TryAssociatedPayload(out DragDropPayload? dropPayload) &&
                (dragPayload.SourceType & dropPayload.SourceType) != 0)
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                DragOverEffect(dropGesture);
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        };

        dropGesture.DragLeave += (sender, args) =>
        {
            DragLeaveEffect(dropGesture);
        };

        dropGesture.Drop += async (s, e) =>
        {
            await OnDroppablesMessageAsync<TTargetAnchor>(anchor, dropGesture, e);
            DragLeaveEffect(dropGesture);
        };
    }

    public static void DragOverEffect(DropGestureRecognizer dropped)
    {
        View? parent = dropped.Parent as View;
        if (parent is not null)
        {
            parent.Scale = 1.05;
            parent.BackgroundColor = Colors.Goldenrod;
        }
    }

    public static void DragLeaveEffect(DropGestureRecognizer dropped)
    {
        View? parent = dropped.Parent as View;
        if (parent is not null)
        {
            parent.Scale = 1.0;
            parent.Opacity = 1;
            parent.BackgroundColor = Colors.Transparent;
        }
    }

    private static async Task OnDroppablesMessageAsync<TTargetAnchor>(TTargetAnchor anchor, DropGestureRecognizer dropGesture, DropEventArgs e)
     where TTargetAnchor : View
    {
        string token = await e.Data.GetTextAsync();
        if (!token.TryAssociatedPayload(out DragDropPayload? sourcePayload))
        {
            return;
        }

        if (!guidTokens.TryGetValue(dropGesture, out GuidToken? dropToken))
        {
            return;
        }
        if (!dropToken.Token.TryAssociatedPayload(out DragDropPayload? targetPayload))
        {
            return;
        }


        //// 构建泛型类型
        //Type genericMessageType = typeof(DragDropMessage);
        //Type constructedMessageType = genericMessageType.MakeGenericType(sourcePayload.View!.GetType(), targetPayload.View!.GetType());

        //// 创建实例
        //object? message = Activator.CreateInstance(constructedMessageType);
        //if (message is null)
        //{
        //    return;
        //}

        //// 设置属性
        //PropertyInfo sourceProp = constructedMessageType.GetProperty("SourcePayload")!;
        //PropertyInfo targetProp = constructedMessageType.GetProperty("TargetPayload")!;
        //sourceProp.SetValue(message, sourcePayload);
        //targetProp.SetValue(message, targetPayload);

        _ = WeakReferenceMessenger.Default.Send<DragDropMessage>(new DragDropMessage()
        {
            SourcePayload = sourcePayload,
            TargetPayload = targetPayload
        });

        // 视觉反馈
        sourcePayload.View.Opacity = 1;
        if (sourcePayload.Anchor is not null)
        {
            sourcePayload.Anchor.Opacity = 1;
        }
        anchor.BackgroundColor = Colors.Transparent;
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