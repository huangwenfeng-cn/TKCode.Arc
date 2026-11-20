---
name: tkcode-resource-lifecycle
description: 核对 TKCode.Arc 的资源绑定、订阅、缓存和挂载生命周期。新增绑定、模板部件替换、容器回收或排查对象滞留时使用。
---

# 资源与生命周期

先读 [实施指南](../../implementation-guide.md) 第 5、6、7、9、10 节，并读取目标的所有权链。

1. 为新增事件订阅、绑定、计时器、动效、惰性内容、缓存和 owner/container 引用写清获取者、所有者、释放时机及重新获取时机。
2. `OnApplyTemplate` 先清理旧部件关系，再绑定新部件。长寿命外部订阅与可视树生命周期不一致时，在卸载解除、重新挂载重新读取。
3. C# 子控件的主题绑定采用 `ArcTokens.Bind` 等本地原语。它返回 `IDisposable`；按真实所有权决定保存和释放方式，不机械要求同生命周期的每个子控件绑定都额外套一层释放设施。
4. Token 和字体持续跟随主题，不能为避免泄漏改为固定快照。组件 Token 计算不能读取 UI 树或业务 I/O。
5. 非 Visual 的 AvaloniaObject 具有动态资源关系时，核对资源宿主、XAML anchor、owner 和订阅释放。不能从 AtomUI 的特定泄漏链直接断言本项目对象已泄漏，也不能未核对就要求所有对象实现额外接口。
6. 对模板重建、容器回收、外部集合替换和异步完成检查旧引用、旧任务及旧状态是否仍能影响当前控件。
7. MotionValue 等需重新挂载的对象不能销毁后不重建。缓存应有失效语义和数量边界。

用定向搜索寻找待核对关系：

```powershell
rg -n 'DynamicResource|GetResourceObservable|ArcTokens.Bind|ResourcesChanged|ActualThemeVariantChanged|IDisposable' src
rg -n 'OnApplyTemplate|OnAttachedToVisualTree|OnDetachedFromVisualTree|ClearContainerForItemOverride' src
```

搜索命中只说明需要检查，不证明泄漏。泄漏修复用 WeakReference 或对象数量验证，保留外部长寿命对象以复现真实引用链；RSS 单独不足以证明泄漏。同步验证资源更新和重新挂载，避免释放修复破坏主题行为。

不导入 AtomUI 的 TokenResourceBinder、容器 attach API 或“Gallery 只允许一个 ShowCase 存活”的专有契约。项目有明确缓存时验证其所有者与淘汰规则。
