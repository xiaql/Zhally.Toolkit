using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Concurrent;

namespace Zhally.Toolkit.NoticeView;

public enum NoticePriority { Low, Medium, High, Critical }

public class NoticeDisplayOptions
{
    public TimeSpan DisplayDuration { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan MinDisplayDuration { get; set; } = TimeSpan.FromMilliseconds(500);
    public int MaxQueueLength { get; set; } = 50;
    public Color FontColor { get; set; } = Colors.DarkOrchid;
    public float FontSize { get; set; } = 16F;
    public float RotationAngle { get; set; } = -20;
    public bool HasShadow { get; set; } = true;
}

public class NoticeMessage(string value, NoticePriority priority = NoticePriority.Medium)
{
    public string Value { get; } = value;
    public NoticePriority Priority { get; } = priority;
}

public partial class NoticeView : GraphicsView, IDrawable, IDisposable
{
    private readonly NoticeDisplayOptions _options;
    private readonly ConcurrentDictionary<int, QueuedNotice> _messageQueue = new();
    private readonly Lock _queueSync = new();
    private QueuedNotice? _currentMessage;
    private DateTimeOffset _currentMessageStartTime;
    private int _messageOrder = 0;
    private bool _isTimerRunning;
    private IDispatcherTimer? _timer;

    public NoticeView(NoticeDisplayOptions? options = null)
    {
        _options = options ?? new NoticeDisplayOptions();
        Drawable = this;
        VerticalOptions = LayoutOptions.Fill;
        HorizontalOptions = LayoutOptions.Fill;
        InputTransparent = true;

        WeakReferenceMessenger.Default.Register<NoticeMessage>(this, (_, m) => OnMessageArrive(m));
    }

    private void OnMessageArrive(NoticeMessage message)
    {
        int enqueueOrder = Interlocked.Increment(ref _messageOrder);
        var enqueueNotice = new QueuedNotice(message.Priority, enqueueOrder, message.Value);

        bool immediateSwitch = false;

        lock (_queueSync)
        {
            if (message.Priority == NoticePriority.Critical)
            {
                _messageQueue.Clear();
                _currentMessage = null;
                immediateSwitch = true; 
            }
            else
            {
                // 非Critical级：移除队列中所有低于新消息优先级的消息
                var lowerPriorityItems = _messageQueue.Values
                    .Where(m => m.Priority < message.Priority)
                    .ToList();
                foreach (var item in lowerPriorityItems)
                {
                    _ = _messageQueue.TryRemove(item.Order, out _);
                }

                if (_currentMessage != null && _currentMessage.Priority < message.Priority)
                {
                    // 移除当前显示的低优先级消息（从队列中清除，因为它可能还在队列中）
                    _ = _messageQueue.TryRemove(_currentMessage.Order, out _);
                    _currentMessage = null; 
                    immediateSwitch = true; 
                }
            }

            // 队列长度控制
            while (_messageQueue.Count >= _options.MaxQueueLength)
            {
                var lowestPriority = _messageQueue.Values.Min(m => m.Priority);
                var oldestLowPriority = _messageQueue.Values
                                        .Where(m => m.Priority == lowestPriority)
                                        .OrderBy(m => m.Order)
                                        .FirstOrDefault();

                if (oldestLowPriority is not null)
                {
                    _ = _messageQueue.TryRemove(oldestLowPriority.Order, out _);
                }
                else break;
            }

            _ = _messageQueue.TryAdd(enqueueNotice.Order, enqueueNotice);
        }

        if (message.Priority == NoticePriority.Critical || immediateSwitch)
        {
            MainThread.BeginInvokeOnMainThread(SwitchToNextMessage);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(UpdateDisplay);
        }
    }

    private void UpdateDisplay()
    {
        lock (_queueSync)
        {
            // 无当前消息且队列有消息时，立即显示第一条
            if (_currentMessage is null && !_messageQueue.IsEmpty)
            {
                StartDisplayingNextMessage();
                return;
            }

            // 有当前消息且队列有消息，检查是否可以切换（已满足最小显示时长）
            if (_currentMessage is not null && !_messageQueue.IsEmpty)
            {
                var elapsed = DateTimeOffset.Now - _currentMessageStartTime;
                if (elapsed >= _options.MinDisplayDuration)
                {
                    SwitchToNextMessage();
                }
            }
        }
    }

    private class QueuedNotice(NoticePriority priority, int order, string message) : IComparable<QueuedNotice>
    {
        public NoticePriority Priority { get; } = priority;
        public int Order { get; } = order;
        public string Message { get; } = message;

        public int CompareTo(QueuedNotice? other)
        {
            if (other is null) return 1;
            // 优先级顺序：Critical > High > Medium > Low
            int priorityCompare = other.Priority.CompareTo(Priority);
            return priorityCompare != 0 ? priorityCompare : Order.CompareTo(other.Order);
        }
    }

    private void StartDisplayingNextMessage()
    {
        QueuedNotice? nextNotice = null;

        lock (_queueSync)
        {
            if (!_messageQueue.IsEmpty)
            {
                nextNotice = _messageQueue.Values.OrderBy(m => m).FirstOrDefault();
                if (nextNotice is not null)
                {
                    _currentMessage = nextNotice;
                }
            }
        }

        if (nextNotice is not null)
        {
            _currentMessageStartTime = DateTimeOffset.Now;
            Notice = nextNotice.Message;
            Invalidate();
            StartTimer();
        }
    }

    private void SwitchToNextMessage()
    {
        lock (_queueSync)
        {
            // 移除当前已显示的消息
            if (_currentMessage is not null)
            {
                _ = _messageQueue.TryRemove(_currentMessage.Order, out _);
            }

            // 准备下一条消息
            var nextNotice = !_messageQueue.IsEmpty
                ? _messageQueue.Values.OrderBy(m => m).FirstOrDefault()
                : null;

            if (nextNotice is not null)
            {
                _currentMessage = nextNotice;
                _currentMessageStartTime = DateTimeOffset.Now;
                Notice = nextNotice.Message;
                Invalidate();
            }
            else
            {
                ClearCurrentMessage();
            }
        }
    }

    private void ClearCurrentMessage()
    {
        _currentMessage = null;
        Notice = string.Empty;
        Invalidate();
        StopTimer();
    }

    private void StartTimer()
    {
        if (_isTimerRunning) return;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += Timer_Tick;
        _timer.Start();
        _isTimerRunning = true;
    }

    private void StopTimer()
    {
        if (!_isTimerRunning) return;

        _timer?.Stop();
        _timer = null;
        _isTimerRunning = false;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_currentMessage == null)
        {
            StopTimer();
            return;
        }

        var elapsed = DateTimeOffset.Now - _currentMessageStartTime;
        bool hasNextMessage;

        lock (_queueSync)
        {
            hasNextMessage = _messageQueue.Count > (_currentMessage != null ? 1 : 0);
        }

        var maxDuration = hasNextMessage
            ? _options.MinDisplayDuration
            : TimeSpan.FromMilliseconds(Math.Max(
                _options.MinDisplayDuration.TotalMilliseconds,
                _options.DisplayDuration.TotalMilliseconds));

        if (elapsed >= maxDuration)
        {
            MainThread.BeginInvokeOnMainThread(SwitchToNextMessage);
        }
    }

    public void ClearQueue()
    {
        lock (_queueSync)
        {
            _messageQueue.Clear();
            ClearCurrentMessage();
        }
    }

    public string Notice { get; private set; } = string.Empty;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (string.IsNullOrEmpty(Notice)) return;

        canvas.SaveState();

        canvas.FontColor = _options.FontColor;
        canvas.FontSize = _options.FontSize;
        canvas.Rotate(_options.RotationAngle, dirtyRect.Center.X, dirtyRect.Center.Y);

        if (_options.HasShadow)
        {
            canvas.SetShadow(new SizeF(10, 10), 10, Colors.Grey);
        }

        canvas.DrawString(Notice, dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center, TextFlow.ClipBounds);

        canvas.RestoreState();
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        StopTimer();
        ClearQueue();
        GC.SuppressFinalize(this);
    }

    public static void DisplayNotice(string message, NoticePriority priority = NoticePriority.Medium)
    {
        _ = WeakReferenceMessenger.Default.Send(new NoticeMessage(message, priority));
    }

    public static void ClearNotice() => DisplayNotice(string.Empty, NoticePriority.Critical); // 用Critical级确保清空所有
}
