using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Ardashboard.EmailService;
using Ardashboard.Stores;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
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

    public MainViewModel()
    {
        var emailService = new EmailService.EmailService(new BankMessageStore());
        var msgs = emailService.GetHtmlBankMessages().Result;
        // var doc = new HtmlAgilityPack.HtmlDocument();
        // doc.LoadHtml();

        // List labels. 
        // IList<Google.Apis.Gmail.v1.Data.Label> labels = request.Execute().Labels;
    }
}

class MainView : Window, IViewFor<MainViewModel>
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
        var someText = _someText(this);
        _dependingText(someText);
    }

    private TextField _someText(View previous)
    {
        var someText = new TextField(ViewModel.SomeText)
        {
            X = Pos.Left(previous),
            Y = Pos.Top(previous) + 1,
            Width = 40
        };

        ViewModel
            .WhenAnyValue(x => x.SomeText)
            .BindTo(someText, field => field.Text)
            .DisposeWith(_disposable);
        someText
            .Events()
            .TextChanged
            .Select(old => someText.Text)
            .DistinctUntilChanged()
            .BindTo(ViewModel, x => x.SomeText)
            .DisposeWith(_disposable);
        Add(someText);
        return someText;
    }

    void _dependingText(View otherView)
    {
        var dependingLabel = new Label("ETIIII")
        {
            X = Pos.X(otherView),
            Y = Pos.Bottom(otherView) + 5,
            Width = 40
        };
        ViewModel
            .WhenAnyValue(x => x.SomeText)
            .Subscribe(s => dependingLabel.Text = s.IsEmpty ? "Kitemmurt" : s)
            .DisposeWith(_disposable);
        Add(dependingLabel);
    }

    protected override void Dispose(bool disposing)
    {
        _disposable.Dispose();
        base.Dispose(disposing);
    }
}