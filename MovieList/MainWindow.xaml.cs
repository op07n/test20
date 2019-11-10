using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;

using MovieList.Properties;
using MovieList.ViewModels;

using ReactiveUI;

namespace MovieList
{
    public abstract class MainWindowBase : ReactiveWindow<MainViewModel> { }

    public partial class MainWindow : MainWindowBase
    {
        public MainWindow()
        {
            this.InitializeComponent();

            this.WhenActivated(disposables =>
            {
                this.WhenAnyValue(v => v.ViewModel)
                    .BindTo(this, v => v.DataContext)
                    .DisposeWith(disposables);

                this.InitializeMainTabControl(disposables);
                this.InitializeMenu(disposables);

                this.ViewModel.OpenFile
                    .WhereNotNull()
                    .Where(model => model.IsExternal)
                    .Discard()
                    .ObserveOnDispatcher()
                    .Subscribe(this.OnOpenFileExternally)
                    .DisposeWith(disposables);

                this.Events().Drop
                    .Where(e => e.Data.GetDataPresent(DataFormats.FileDrop))
                    .SelectMany(e => (string[])e.Data.GetData(DataFormats.FileDrop))
                    .Select(file => new OpenFileModel(file, true))
                    .InvokeCommand(this.ViewModel.OpenFile);

                this.Events().Closing
                    .Do(e => e.Cancel = true)
                    .Discard()
                    .InvokeCommand(this.ViewModel.Shutdown)
                    .DisposeWith(disposables);

                this.ViewModel.Shutdown
                    .Do(unit => disposables.Dispose())
                    .Subscribe(this.Close);
            });
        }

        private void InitializeMainTabControl(CompositeDisposable disposables)
        {
            this.MainTabControl.Items.Add(new TabItem
            {
                Header = Messages.HomePage,
                Content = new ViewModelViewHost { ViewModel = this.ViewModel.HomePage }
            });

            this.AddFileTabOnChange(changes => changes.Select(change => change.Item.Current), ListChangeReason.Add)
                .DisposeWith(disposables);

            this.AddFileTabOnChange(changes => changes.SelectMany(change => change.Range), ListChangeReason.AddRange)
                .DisposeWith(disposables);

            this.ViewModel.Files
                .ToObservableChangeSet()
                .WhereReasonsAre(ListChangeReason.Remove)
                .SelectMany(changeSet => changeSet)
                .Select(change => change.Item.Current.FileName)
                .Select(fileName => this.MainTabControl.Items
                    .Cast<TabItem>()
                    .First(item => fileName.Equals(item.Tag)))
                .ObserveOnDispatcher()
                .Subscribe(item => this.MainTabControl.Items.Remove(item))
                .DisposeWith(disposables);

            this.Bind(this.ViewModel, vm => vm.SelectedItemIndex, v => v.MainTabControl.SelectedIndex)
                .DisposeWith(disposables);
        }

        private void InitializeMenu(CompositeDisposable disposables)
        {
            this.BindCommand(this.ViewModel, vm => vm.HomePage.CreateFile, v => v.NewMenuItem)
                .DisposeWith(disposables);

            this.Events().KeyUp
                .Where(e => e.Key == Key.N && this.IsCtrlDown() && !this.IsAltDown() && !this.IsShiftDown())
                .Discard()
                .InvokeCommand(this.ViewModel.HomePage.CreateFile)
                .DisposeWith(disposables);

            this.BindCommand(this.ViewModel, vm => vm.HomePage.OpenFile, v => v.OpenMenuItem)
                .DisposeWith(disposables);

            this.Events().KeyUp
                .Where(e => e.Key == Key.O && this.IsCtrlDown() && !this.IsAltDown() && !this.IsShiftDown())
                .Select(e => (string?)null)
                .InvokeCommand(this.ViewModel.HomePage.OpenFile)
                .DisposeWith(disposables);

            this.ViewModel.HomePage.RecentFiles
                .ActOnEveryObject(
                    file => this.OpenRecentMenuItem.Items.Insert(
                        this.ViewModel.HomePage.RecentFiles.IndexOf(file),
                        new MenuItem
                        {
                            Header = file.File.Path,
                            Command = this.ViewModel.OpenFile,
                            CommandParameter = new OpenFileModel(file.File.Path),
                            Tag = file.File.Path
                        }),
                    file => this.OpenRecentMenuItem.Items.Remove(this.OpenRecentMenuItem.Items
                        .Cast<MenuItem>()
                        .First(item => file.File.Path.Equals(item.Tag))));

            this.OneWayBind(
                    this.ViewModel,
                    vm => vm.HomePage.RecentFiles.Count,
                    v => v.OpenRecentMenuItem.IsEnabled,
                    count => count > 0)
                .DisposeWith(disposables);

            this.SaveMenuItem.IsEnabled = false;
            this.SaveAsMenuItem.IsEnabled = false;
            this.SettingsMenuItem.IsEnabled = false;
            this.CloseMenuItem.IsEnabled = false;

            this.BindCommand(this.ViewModel, vm => vm.Shutdown, v => v.ExitMenuItem)
                .DisposeWith(disposables);

            this.BindCommand(this.ViewModel, vm => vm.ShowAbout, v => v.AboutMenuItem)
                .DisposeWith(disposables);

            this.Events().KeyUp
                .Where(e => e.Key == Key.F1)
                .Discard()
                .InvokeCommand(this.ViewModel.ShowAbout)
                .DisposeWith(disposables);
        }

        private IDisposable AddFileTabOnChange(
            Func<IObservable<Change<TabItem>>, IObservable<TabItem>> itemSelector,
            params ListChangeReason[] reasons)
            => itemSelector(this.ViewModel.Files
                .ToObservableChangeSet()
                .WhereReasonsAre(reasons)
                .Transform(vm => new TabItem
                {
                    Header = new ViewModelViewHost { ViewModel = vm.Header },
                    Content = new ViewModelViewHost { ViewModel = vm },
                    Tag = vm.FileName
                })
                .SelectMany(changeSet => changeSet))
                .ObserveOnDispatcher()
                .Subscribe(item => this.MainTabControl.Items.Add(item));

        private void OnOpenFileExternally()
        {
            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();
        }

        private bool IsCtrlDown()
            => this.IsAnyKeyDown(Key.LeftCtrl, Key.RightCtrl);

        private bool IsAltDown()
            => this.IsAnyKeyDown(Key.LeftAlt, Key.RightAlt);

        private bool IsShiftDown()
            => this.IsAnyKeyDown(Key.LeftShift, Key.RightShift);

        private bool IsAnyKeyDown(params Key[] keys)
            => keys.Any(Keyboard.IsKeyDown);
    }
}
