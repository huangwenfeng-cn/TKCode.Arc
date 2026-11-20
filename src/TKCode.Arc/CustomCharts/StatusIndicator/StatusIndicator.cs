using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

// 状态灯入门阅读顺序：状态枚举 → 属性注册/读写 → Refresh → 主题模板 → 自动化接口。
// 应用提供设备反馈，控件把反馈转换为灯色和文字；点击启动按钮不代表设备已经运行。
// TemplatedControl 表示图形由独立 XAML 主题绘制，本类主要管理状态和通知。
// 普通中文注释用于源码阅读；公共英文 XML 文档供 Gallery 生成三语 API。
/// <summary>A read-only device status lamp with localized text and optional Full-motion blinking; the application owns state and alarms.</summary>
[PseudoClasses(":unknown", ":offline", ":idle", ":running", ":warning", ":fault", ":blinking")]
public class StatusIndicator : TemplatedControl
{
    // StyledProperty 是支持绑定/样式的属性登记对象，实际实例值通过下面 State 访问。
    // 默认 Unknown 表示尚无可靠反馈；validate 拒绝六种状态之外的枚举值。
    /// <summary>Application-provided state (default Unknown); changing it updates text and color immediately.</summary>
    public static readonly StyledProperty<StatusIndicatorState> StateProperty =
        AvaloniaProperty.Register<StatusIndicator, StatusIndicatorState>(nameof(State),
            StatusIndicatorState.Unknown, validate: value => value is >= StatusIndicatorState.Unknown and <= StatusIndicatorState.Fault);
    // Label 是可选的自定义状态文字，不是设备名称。null 跟随当前语言，
    // "" 则是明确的空文字；设备名称可通过 AutomationProperties.Name 提供。
    /// <summary>Custom status text; null uses the localized state name. Set an automation name to identify the device.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatusIndicator, string?>(nameof(Label));
    /// <summary>Whether status text is visible (default true); accessibility always exposes the actual text.</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<StatusIndicator, bool>(nameof(ShowLabel), true);
    /// <summary>Whether to pulse the lamp once per second (default false); Reduced and Off motion remain steady.</summary>
    public static readonly StyledProperty<bool> IsBlinkingProperty =
        AvaloniaProperty.Register<StatusIndicator, bool>(nameof(IsBlinking));
    // DirectProperty 暴露只读结果：应用设置 State/Label，控件计算 DisplayText。
    // SetAndRaise 会在文字真正变化时通知绑定；直接给字段赋值不会完成这套通知。
    /// <summary>Resolved custom or localized status text, available for read-only binding.</summary>
    public static readonly DirectProperty<StatusIndicator, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<StatusIndicator, string>(nameof(DisplayText), owner => owner.DisplayText);

    private string _displayText = string.Empty;
    // 语言管理器属于应用：仅在挂载期间订阅，重挂载时重新读取当前语言。
    private ArcLanguageManager? _languages;
    private StatusIndicatorAutomationPeer? _peer;
    // 记录是否已经进入可视树；不可见或已卸载时，不应继续让旧模板闪烁。
    private bool _attached;

    /// <summary>Creates an indicator in the Unknown state, with visible text and no blinking.</summary>
    public StatusIndicator()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Refresh();
    }

    // CLR 属性是 C# 调用入口，GetValue/SetValue 与 XAML 绑定使用相同的属性存储。
    /// <inheritdoc cref="StateProperty"/>
    public StatusIndicatorState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <inheritdoc cref="ShowLabelProperty"/>
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }
    /// <inheritdoc cref="IsBlinkingProperty"/>
    public bool IsBlinking { get => GetValue(IsBlinkingProperty); set => SetValue(IsBlinkingProperty, value); }
    /// <inheritdoc cref="DisplayTextProperty"/>
    public string DisplayText => _displayText;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBlinkingProperty && _attached)
        {
            // 先解除旧订阅，再按当前请求决定是否订阅，防止重复订阅同一个回调。
            // 不闪烁的灯无需监听布局；监听是为了发现父容器被隐藏，而非每帧采样设备。
            LayoutUpdated -= OnLayoutUpdated;
            if (IsBlinking)
            {
                LayoutUpdated += OnLayoutUpdated;
            }
        }
        if (change.Property == StateProperty || change.Property == LabelProperty ||
            change.Property == IsBlinkingProperty || change.Property == IsVisibleProperty)
        {
            Refresh();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 页面首次显示或重新显示时，恢复需要的订阅并读取最新语言、状态。
        base.OnAttachedToVisualTree(e);
        _attached = true;
        if (IsBlinking)
        {
            LayoutUpdated += OnLayoutUpdated;
        }
        _languages = ArcLanguageManager.Current;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 离开界面时对称解除 += 建立的订阅，避免应用语言事件一直引用旧控件。
        _attached = false;
        LayoutUpdated -= OnLayoutUpdated;
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
        // 移除动画选择器，使旧模板灯恢复稳态；没有私有定时器或后台设备订阅。
        PseudoClasses.Set(":blinking", false);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) => Refresh();

    // 父容器隐藏时也关闭动画选择器；布局通知不是每控件帧定时器。
    private void OnLayoutUpdated(object? sender, EventArgs e) => RefreshBlinking();
    private void RefreshBlinking() => PseudoClasses.Set(":blinking", IsBlinking && _attached && IsVisible && IsEffectivelyVisible);

    private void Refresh()
    {
        // 伪类可理解为给主题看的状态标签。逐一设置后，当前状态对应项为 true，
        // 原状态对应项恢复 false；主题用这些标签挑选颜色，不在 C# 中写死颜色。
        PseudoClasses.Set(":unknown", State == StatusIndicatorState.Unknown);
        PseudoClasses.Set(":offline", State == StatusIndicatorState.Offline);
        PseudoClasses.Set(":idle", State == StatusIndicatorState.Idle);
        PseudoClasses.Set(":running", State == StatusIndicatorState.Running);
        PseudoClasses.Set(":warning", State == StatusIndicatorState.Warning);
        PseudoClasses.Set(":fault", State == StatusIndicatorState.Fault);
        RefreshBlinking();
        // switch 把设备状态映射为语言资源键，随后生成器提供的 Get 读取当前翻译。
        // _ 是兜底分支；?? 表示 Label 为 null 时才使用翻译。
        var kind = State switch
        {
            StatusIndicatorState.Offline => StatusIndicatorLangResourceKind.Offline,
            StatusIndicatorState.Idle => StatusIndicatorLangResourceKind.Idle,
            StatusIndicatorState.Running => StatusIndicatorLangResourceKind.Running,
            StatusIndicatorState.Warning => StatusIndicatorLangResourceKind.Warning,
            StatusIndicatorState.Fault => StatusIndicatorLangResourceKind.Fault,
            _ => StatusIndicatorLangResourceKind.Unknown
        };
        SetAndRaise(DisplayTextProperty, ref _displayText, Label ?? StatusIndicatorLangResources.Get(kind));
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new StatusIndicatorAutomationPeer(this);

    private sealed class StatusIndicatorAutomationPeer(StatusIndicator owner) : ControlAutomationPeer(owner), IValueProvider
    {
        // 给屏幕阅读器/自动化工具提供一个完整的只读状态，而不是分别读灯和模板文字。
        // 即使 ShowLabel=false，真实状态仍通过文本接口提供；这个接口不能发送设备命令。
        private string? _lastText;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(owner);
            return string.IsNullOrEmpty(name) ? StatusIndicatorLangResources.Get(StatusIndicatorLangResourceKind.DeviceStatus) : name;
        }
        protected override string? GetHelpTextCore() => owner.DisplayText;
        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];
        public bool IsReadOnly => true;
        public string Value => owner.DisplayText;
        public void SetValue(string? value) { }

        public void Announce()
        {
            // 记住上次播报内容，只在文字改变时通知；灯的透明度变化不重复播报。
            if (_lastText != owner.DisplayText)
            {
                var old = _lastText;
                _lastText = owner.DisplayText;
                RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, old, _lastText);
            }
        }
    }
}
