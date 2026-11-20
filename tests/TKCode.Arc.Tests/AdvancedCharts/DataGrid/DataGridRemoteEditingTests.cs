using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

public class DataGridRemoteEditingTests
{
    // 测试服务保存独立快照，故意允许取消后的请求返回，验证控件自身的迟到结果隔离。
    private sealed class Server : IDataGridEditableDataSource
    {
        public DataGridRow Row { get; private set; } = new("r1") { { "name", "Old" }, { "amount", 10m }, { "active", false } };
        public List<(DataGridCellChange Change, CancellationToken Token, TaskCompletionSource<object> Reply)> Writes { get; } = [];
        public List<DataGridRowsRequest> Reads { get; } = [];
        public bool Synchronous { get; set; }
        public bool FailReads { get; set; }

        public Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation)
        {
            Reads.Add(request);
            if (FailReads)
            {
                return Task.FromException<DataGridRowsResult>(new InvalidOperationException("Read unavailable"));
            }
            return Task.FromResult(new DataGridRowsResult([Row], 1,
                new Dictionary<string, decimal?> { ["amount"] = (decimal?)Row["amount"] }));
        }

        public Task<object> UpdateCellAsync(DataGridCellChange change, CancellationToken cancellation)
        {
            var reply = new TaskCompletionSource<object>();
            Writes.Add((change, cancellation, reply));
            if (Synchronous)
            {
                Answer(change.NewValue);
            }
            return reply.Task;
        }

        public void Answer(object? value, int? index = null, string key = "r1")
        {
            var write = Writes[index ?? (Writes.Count - 1)];
            Row = new DataGridRow(key) { { "name", Row["name"] }, { "amount", Row["amount"] }, { "active", Row["active"] } };
            Row[write.Change.ColumnKey] = value;
            write.Reply.SetResult(Row);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (TKCode.Arc.DataGrid Grid, Server Source, Window Window) Show(bool synchronous = false)
    {
        var source = new Server { Synchronous = synchronous };
        var grid = new TKCode.Arc.DataGrid { DataSource = source, Width = 700, MaxViewportHeight = 300 };
        grid.Columns.Add(new DataGridColumn("name", "Name"));
        grid.Columns.Add(new DataGridColumn("amount", "Amount", DataGridColumnType.Number));
        grid.Columns.Add(new DataGridColumn("active", "Active", DataGridColumnType.Boolean));
        var window = DataGridBehaviorTests.Show(grid);
        Settle(grid);
        return (grid, source, window);
    }

    private static void Settle(TKCode.Arc.DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        grid.EnsureView();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Commit_Waits_Once_Uses_Authoritative_Value_And_Reloads_Query_And_Totals()
    {
        var (grid, source, _) = Show();
        var changes = new List<DataGridCellChange>();
        grid.CellsChanged += (_, e) => changes.AddRange(e.Changes);
        grid.BeginEdit(new DataGridCellAddress(0, 1), "23", false);
        var task = grid.CommitEditAsync();
        grid.CommitEditAsync().ShouldBeSameAs(task);
        grid.CommitEdit().ShouldBeFalse();
        grid.IsCellCommitPending.ShouldBeTrue();
        grid.ValueAt(0, 1).ShouldBe(10m);
        source.Row["amount"].ShouldBe(10m);
        grid.SetCellValue(0, 2, true).ShouldBeFalse();
        source.Writes.Count.ShouldBe(1);
        changes.ShouldBeEmpty();
        var reads = source.Reads.Count;
        source.Answer(24m);
        Settle(grid);
        task.IsCompletedSuccessfully.ShouldBeTrue();
        task.Result.ShouldBeTrue();
        grid.IsCellCommitPending.ShouldBeFalse();
        grid.IsEditing.ShouldBeFalse();
        grid.ValueAt(0, 1).ShouldBe(24m);
        grid.Totals[1].ShouldBe(24m);
        source.Reads.Count.ShouldBeGreaterThan(reads);
        changes.Single().NewValue.ShouldBe(24m);
        changes[0].Row.ShouldBeSameAs(source.Row);
        grid.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Failure_Keeps_Original_And_Draft_And_Requires_Explicit_Retry()
    {
        var (grid, source, _) = Show();
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Draft", false);
        var task = grid.CommitEditAsync();
        source.Writes[0].Reply.SetException(new InvalidOperationException("server rejected"));
        Settle(grid);
        task.Result.ShouldBeFalse();
        grid.IsEditing.ShouldBeTrue();
        grid.IsCellCommitPending.ShouldBeFalse();
        grid.Viewport.EditorText.ShouldBe("Draft");
        grid.CellText(0, 0).ShouldBe("Old");
        source.Writes.Count.ShouldBe(1);
        grid.Edit!.Invalid.ShouldBeTrue();
        var retry = grid.CommitEditAsync();
        source.Answer("Accepted");
        retry.Result.ShouldBeTrue();
        grid.CellText(0, 0).ShouldBe("Accepted");
    }

    [AvaloniaFact]
    public void Cancelled_Write_Cannot_Close_Or_Change_A_New_Edit()
    {
        var (grid, source, _) = Show();
        grid.BeginEdit(new DataGridCellAddress(0, 0), "First", false);
        var first = grid.CommitEditAsync();
        grid.CancelEdit();
        first.Result.ShouldBeFalse();
        source.Writes[0].Token.IsCancellationRequested.ShouldBeTrue();
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Second", false);
        var second = grid.CommitEditAsync();
        source.Answer("Late", 0);
        grid.IsCellCommitPending.ShouldBeTrue();
        grid.Viewport.EditorText.ShouldBe("Second");
        grid.CellText(0, 0).ShouldBe("Old");
        second.IsCompleted.ShouldBeFalse();
        source.Answer("Second");
        second.Result.ShouldBeTrue();
    }

    [AvaloniaTheory]
    [InlineData("source")]
    [InlineData("local")]
    [InlineData("query")]
    [InlineData("columns")]
    [InlineData("detach")]
    [InlineData("key")]
    public void Changing_Edit_Identity_Cancels_And_Ignores_Late_Result(string change)
    {
        var (grid, source, window) = Show();
        var changed = 0;
        grid.CellsChanged += (_, _) => changed++;
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Late", false);
        var task = grid.CommitEditAsync();
        switch (change)
        {
            case "source":
                grid.DataSource = new Server();
                break;
            case "local":
                grid.DataSource = null;
                break;
            case "query":
                grid.SortDescriptions = [DataGridSort.Descending("name")];
                break;
            case "columns":
                grid.Columns[0].Header = "Changed";
                break;
            case "detach":
                window.Content = null;
                break;
            case "key":
                grid.RowKeySelector = row => "changed-key";
                break;
        }
        task.IsCompleted.ShouldBeTrue();
        task.Result.ShouldBeFalse();
        source.Writes[0].Token.IsCancellationRequested.ShouldBeTrue();
        source.Answer("Late");
        grid.IsCellCommitPending.ShouldBeFalse();
        grid.IsEditing.ShouldBeFalse();
        changed.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Boolean_And_Synchronous_Server_Use_The_Same_Commit_Path()
    {
        var (grid, source, _) = Show(synchronous: true);
        grid.BeginEdit(new DataGridCellAddress(0, 2), null, false);
        source.Writes.Count.ShouldBe(1);
        grid.ValueAt(0, 2).ShouldBe(true);
        grid.IsEditing.ShouldBeFalse();
        grid.IsCellCommitPending.ShouldBeFalse();
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Same task", false);
        grid.CommitEdit().ShouldBeTrue();
        grid.CommitEditAsync().Result.ShouldBeTrue();
        source.Writes.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Custom_Editor_And_Validation_Reuse_Existing_Contracts()
    {
        var (grid, source, _) = Show();
        grid.Columns[0].EditTemplate = new FuncDataTemplate<DataGridEditContext>((context, _) =>
            new TextBox { Text = context!.Text });
        Settle(grid);
        grid.BeginEdit();
        grid.Edit!.Context!.Value = "Custom";
        var task = grid.CommitEditAsync();
        source.Writes[0].Change.NewValue.ShouldBe("Custom");
        source.Answer("CUSTOM");
        task.Result.ShouldBeTrue();
        grid.CellText(0, 0).ShouldBe("CUSTOM");
        grid.CanEditCell = c => c.Column.Key != "active";
        grid.SetCellValue(0, 2, true).ShouldBeFalse();
        grid.CellEditEnding += (_, e) => e.Cancel = true;
        grid.BeginEdit(new DataGridCellAddress(0, 1), "42", false);
        grid.CommitEditAsync().Result.ShouldBeFalse();
        source.Writes.Count.ShouldBe(1);
        grid.IsEditing.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Replacement_With_A_Different_Key_Is_A_Failed_Commit()
    {
        var (grid, source, _) = Show();
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Draft", false);
        var task = grid.CommitEditAsync();
        source.Answer("Wrong", key: "other");
        task.Result.ShouldBeFalse();
        grid.CellText(0, 0).ShouldBe("Old");
        grid.IsEditing.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Blocking_Rules_Reject_Before_Any_Server_Write()
    {
        var (grid, source, _) = Show();
        grid.Columns[1].Rules.Add(new RangeRule { Minimum = 0m });
        grid.BeginEdit(new DataGridCellAddress(0, 1), "-1", false);
        grid.CommitEditAsync().Result.ShouldBeFalse();
        grid.IsEditing.ShouldBeTrue();
        grid.Edit!.Invalid.ShouldBeTrue();
        grid.SetCellValue(0, 1, -1m).ShouldBeFalse();
        source.Writes.ShouldBeEmpty();
        grid.ValueAt(0, 1).ShouldBe(10m);
    }

    [AvaloniaFact]
    public void Keyboard_Movement_Waits_For_Save_And_Escape_Cancels()
    {
        var (grid, source, _) = Show();
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Draft", false);
        grid.CommitEdit((0, 1), null).ShouldBeFalse();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 0));
        source.Answer("Saved");
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 1));
        grid.BeginEdit(new DataGridCellAddress(0, 1), "30", false);
        var task = grid.CommitEditAsync();
        grid.HandleGridKey(new Avalonia.Input.KeyEventArgs { Key = Avalonia.Input.Key.Escape });
        task.Result.ShouldBeFalse();
        grid.IsEditing.ShouldBeFalse();
        source.Writes[1].Token.IsCancellationRequested.ShouldBeTrue();
        source.Answer(30m);
        grid.ValueAt(0, 1).ShouldBe(10m);
    }

    [AvaloniaFact]
    public void Local_CommitAsync_Retains_Synchronous_Write_And_Undo()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Local", false);
        var task = grid.CommitEditAsync();
        task.IsCompletedSuccessfully.ShouldBeTrue();
        task.Result.ShouldBeTrue();
        grid.CellText(0, 0).ShouldBe("Local");
        grid.CanUndo.ShouldBeTrue();
        grid.Undo();
        grid.CellText(0, 0).ShouldBe("Lumen Labs");
    }

    [AvaloniaFact]
    public void Save_Confirmation_Is_Separate_From_A_Failed_Query_Reload()
    {
        var (grid, source, _) = Show();
        var changes = 0;
        grid.CellsChanged += (_, _) => changes++;
        grid.BeginEdit(new DataGridCellAddress(0, 0), "Saved", false);
        var task = grid.CommitEditAsync();
        source.FailReads = true;
        source.Answer("Saved");
        task.Result.ShouldBeTrue();
        changes.ShouldBe(1);
        grid.IsEditing.ShouldBeFalse();
        grid.StatusBar!.MessageText.ShouldNotBeNull().ShouldContain("Rows could not load");
    }

    [AvaloniaFact]
    public void Automation_Value_Writes_Use_Server_Confirmation_And_Pending_ReadOnly_State()
    {
        var (grid, source, _) = Show();
        var provider = new DataGridBodyAutomationPeer(grid, grid.Viewport);
        provider.IsReadOnly.ShouldBeFalse();
        provider.SetValue("Automation draft");
        provider.IsReadOnly.ShouldBeTrue();
        provider.Value.ShouldBe("Old");
        source.Writes.Count.ShouldBe(1);
        source.Answer("Authoritative");
        provider.IsReadOnly.ShouldBeFalse();
        provider.Value.ShouldBe("Authoritative");
        grid.Columns[0].IsEditable = false;
        provider.IsReadOnly.ShouldBeTrue();
        provider.SetValue("Refused");
        source.Writes.Count.ShouldBe(1);
    }
}
