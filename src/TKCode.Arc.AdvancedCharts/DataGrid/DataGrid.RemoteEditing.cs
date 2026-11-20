using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    // 阅读顺序：SetRemoteCellValue 是布尔/渲染器入口，文本和模板从 CommitEdit 进入 StartRemoteCommit。
    // 只有一个在途写入。任务自身持有取消源；控件只持有当前任务，迟到任务不能清理新任务的状态。
    private sealed class RemoteCommit
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private RemoteCommit? _remoteCommit;

    private bool RemoteCellAllowed(int row, int accessor) =>
        DataSourceItem(row) is { } item && DataSourceKey(row) is { } key &&
        (CanEditCell is not { } predicate ||
         predicate(new DataGridCellContext(item, key, ColumnOf(accessor), DataSourceValue(row, accessor))));

    private bool SetRemoteCellValue(int row, int accessor, object? value)
    {
        if (DataSource is not IDataGridEditableDataSource source)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.GridReadOnly));
            return false;
        }
        if (!IsCellEditable(row, accessor) || !Fits(ColumnOf(accessor), value) ||
            _edit is not null && !CommitEdit(null, null))
        {
            return false;
        }
        var item = DataSourceItem(row)!;
        var key = DataSourceKey(row)!;
        var old = DataSourceValue(row, accessor);
        if (SameValue(old, value))
        {
            return false;
        }
        if (ValidationMode == DataGridValidationMode.Block && Validate(item, key, accessor, value) is { } invalid)
        {
            Say(invalid);
            return false;
        }
        var query = _queryVersion;
        var args = new DataGridCellEditEndingEventArgs(CellEditEndingEvent, item, key, ColumnOf(accessor), old, value);
        RaiseEvent(args);
        if (args.Cancel)
        {
            if (!string.IsNullOrEmpty(args.ErrorMessage))
            {
                Say(args.ErrorMessage);
            }
            return false;
        }
        // 宿主事件可以切换数据源或列；不能用事件之前的行继续发请求。
        if (!ReferenceEquals(source, DataSource) || query != _queryVersion || !IsCellEditable(row, accessor) ||
            !ReferenceEquals(item, DataSourceItem(row)))
        {
            return false;
        }
        return StartRemoteCommit(row, accessor, item, key, old, value, null, null);
    }

    private bool StartRemoteCommit(int row, int accessor, object item, object key, object? old, object? value,
                                   DataGridEditState? edit, (int Rows, int Columns)? move)
    {
        if (DataSource is not IDataGridEditableDataSource source || _remoteCommit is not null ||
            !IsCellEditable(row, accessor) || !ReferenceEquals(item, DataSourceItem(row)) ||
            edit is not null && !ReferenceEquals(edit, _edit))
        {
            return false;
        }
        var operation = new RemoteCommit();
        var column = ColumnOf(accessor);
        var query = _queryVersion;
        _remoteCommit = operation;
        IsCellCommitPending = true;
        if (ReferenceEquals(_remoteCommit, operation))
        {
            Viewport.SetEditorCommitPending(true);
            Say(DataGridText.Get(DataGridLangResourceKind.CellSaving));
            _ = SaveRemoteCellAsync(operation, source, query,
                new DataGridCellChange(item, key, column.Key, old, value), accessor, edit, move);
        }
        else
        {
            operation.Cancellation.Dispose();
        }
        // 同步完成的数据源仍可以报告 true；这里只读取已完成任务，不阻塞 UI 线程。
        return operation.Completion.Task.IsCompletedSuccessfully && operation.Completion.Task.Result;
    }

    private async Task SaveRemoteCellAsync(RemoteCommit operation, IDataGridEditableDataSource source, int query,
                                          DataGridCellChange change, int accessor, DataGridEditState? edit,
                                          (int Rows, int Columns)? move)
    {
        try
        {
            object authoritative;
            object? stored;
            try
            {
                authoritative = await source.UpdateCellAsync(change, operation.Cancellation.Token);
                if (!IsCurrent())
                {
                    return;
                }
                if (authoritative is null)
                {
                    throw new InvalidOperationException("The replacement row must not be null.");
                }
                var key = RowKeySelector?.Invoke(authoritative) ?? (authoritative is DataGridRow record ? record.Key : authoritative);
                if (!Equals(change.RowKey, key))
                {
                    throw new InvalidOperationException("The replacement row must retain the stable row key.");
                }
                var access = _store.Accessors[accessor];
                try
                {
                    stored = access.Read(authoritative);
                }
                finally
                {
                    access.Release();
                }
            }
            catch (Exception)
            {
                if (IsCurrent())
                {
                    // 不将异常原文当作业务文案。宿主在预提交事件中给出可展示的拒绝理由；
                    // 网络失败显示本地化通用提示，草稿保留供用户决定是否重试。
                    if (edit is not null && ReferenceEquals(edit, _edit))
                    {
                        RefuseEdit(edit, DataGridText.Get(DataGridLangResourceKind.CellSaveFailed));
                    }
                    else
                    {
                        Say(DataGridText.Get(DataGridLangResourceKind.CellSaveFailed));
                    }
                    operation.Completion.TrySetResult(false);
                }
                return;
            }

            if (!IsCurrent())
            {
                return;
            }
            if (edit is not null && ReferenceEquals(edit, _edit))
            {
                edit.Done = true;
                CloseEditor();
                if (move is { } delta)
                {
                    Select(new DataGridCellAddress(edit.Row + delta.Rows, edit.Column + delta.Columns));
                }
                Viewport.FocusGrid();
            }
            // 返回行用于成功事件；重新读取整条查询，避免本地猜测排序位置、筛选成员和服务端总计。
            // 例如金额修改后某行可能不再满足 >100 的筛选，因此不能把返回行塞在原显示下标。
            if (_checked.Contains(change.RowKey))
            {
                _checkedItems[change.RowKey] = authoritative;
            }
            ResetBlocks(preserveCommit: true);
            InvalidateOptions();
            _editVersion++;
            // 保存与查询是两个结果；先报告保存，再读取，同步的读取失败提示也不会被“已保存”覆盖。
            Say(DataGridText.Get(DataGridLangResourceKind.CellSaved));
            RequestRows(0, 0);
            Viewport.InvalidateLayers();
            Viewport.UpdateTotals();
            operation.Completion.TrySetResult(true);
            RaiseEvent(new DataGridCellsChangedEventArgs(CellsChangedEvent,
                [change with { Row = authoritative, NewValue = stored }], DataGridChangeReason.Edit));
        }
        finally
        {
            if (ReferenceEquals(_remoteCommit, operation))
            {
                FinishPending();
            }
            operation.Completion.TrySetResult(false);
            operation.Cancellation.Dispose();
        }

        bool IsCurrent() => ReferenceEquals(_remoteCommit, operation) && !operation.Cancellation.IsCancellationRequested &&
                            ReferenceEquals(DataSource, source) && _queryVersion == query;

        void FinishPending()
        {
            Viewport.SetEditorCommitPending(false);
            _remoteCommit = null;
            IsCellCommitPending = false;
        }
    }

    private void CancelRemoteCommit()
    {
        if (_remoteCommit is not { } operation)
        {
            return;
        }
        Viewport.SetEditorCommitPending(false);
        _remoteCommit = null;
        IsCellCommitPending = false;
        // 先移除所有权，再发取消通知；取消回调同步完成任务时也只能成为迟到结果。
        operation.Completion.TrySetResult(false);
        operation.Cancellation.Cancel();
    }

    private void CancelRemoteEdit()
    {
        if (_edit is { IsRemote: true } || _remoteCommit is not null)
        {
            CancelEdit();
        }
    }
}
