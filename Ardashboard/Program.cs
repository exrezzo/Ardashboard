using System.ComponentModel;
using System.Data;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NStack;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Terminal.Gui;

namespace Ardashboard;

class Program
{
    public static void Main(string[] args)
    {
        Application.Init();
        RxApp.MainThreadScheduler = TerminalScheduler.Default;
        RxApp.TaskpoolScheduler = TaskPoolScheduler.Default;
        Application.Run(new MainView(new MainViewModel()));
    }
}

class MainViewModel : ReactiveObject
{
    [Reactive] public ustring SomeText { get; set; } = ustring.Empty;

    public List<F.Core.EmailService.BankTransaction.BankTransaction> msgs;

    public MainViewModel()
    {
        // var emailService = new EmailService.EmailService(new BankMessageStore());
        // var msgs = emailService.GetHtmlBankMessages().Result;
        msgs = F.Core.EmailService.EmailServiceModule.getBankMessages.ToList();
    }
}

class MainView : Toplevel, IViewFor<MainViewModel>
{
    readonly CompositeDisposable _disposable = new();

    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (MainViewModel)value!;
    }

    public MainViewModel? ViewModel { get; set; }

    public MainView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        var menuBar = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("Menu1", new MenuItem[] { new MenuItem("Item1", "Help", null) })
        });
        Add(menuBar);
        var bankTransactionsWindow = new Window("Last bank transactions")
        {
            X = 0,
            Y = Pos.Bottom(menuBar),
            // Width = Dim.Percent(50, true),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        var bankTransactionsDataTable = ViewModel.msgs.ToDataTable();
        var columnStyles = new Dictionary<DataColumn, TableView.ColumnStyle>
        {
            [bankTransactionsDataTable.Columns["Amount"] ?? new()] = new TableView.ColumnStyle() { Alignment = TextAlignment.Centered, MinAcceptableWidth = 30}
        };
        var transactionsListView =
            new TableView(bankTransactionsDataTable)
            {
                X = Pos.X(bankTransactionsWindow),
                Y = Pos.Y(bankTransactionsWindow),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Style = new TableView.TableStyle()
                {
                    ColumnStyles = columnStyles
                }
            };

        bankTransactionsWindow.Add(transactionsListView);
        Add(bankTransactionsWindow);
    }


    protected override void Dispose(bool disposing)
    {
        _disposable.Dispose();
        base.Dispose(disposing);
    }
}

internal static class DataTableExtensions
{
    public static DataTable ToDataTable<T>(this IList<T> data)
    {
        PropertyDescriptorCollection properties =
            TypeDescriptor.GetProperties(typeof(T));
        DataTable table = new DataTable();
        foreach (PropertyDescriptor prop in properties)
            table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
        foreach (T item in data)
        {
            DataRow row = table.NewRow();
            foreach (PropertyDescriptor prop in properties)
                row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
            table.Rows.Add(row);
        }

        return table;
    }
}