using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;

using MovieList.ViewModels;

using ReactiveUI;

namespace MovieList.Views
{
    public abstract class RecentFileControlBase : ReactiveUserControl<RecentFileViewModel> { }

    public partial class RecentFileControl : RecentFileControlBase
    {
        public RecentFileControl()
        {
            this.InitializeComponent();

            this.WhenActivated(disposables =>
            {
                this.WhenAnyValue(v => v.ViewModel)
                    .BindTo(this, v => v.DataContext)
                    .DisposeWith(disposables);

                this.OneWayBind(this.ViewModel, vm => vm.File.Name, v => v.ListNameTextBlock.Text)
                    .DisposeWith(disposables);

                this.OneWayBind(this.ViewModel, vm => vm.File.Path, v => v.ListPathTextBlock.Text)
                    .DisposeWith(disposables);

                this.Bind(this.ViewModel, vm => vm.IsSelected, v => v.IsSelectedCheckBox.IsChecked)
                    .DisposeWith(disposables);

                this.Events().MouseLeftButtonDown
                    .Where(e => e.ClickCount == 2)
                    .Select(e => this.ViewModel.File.Path)
                    .ObserveOnDispatcher()
                    .InvokeCommand(this.ViewModel.HomePage.OpenRecentFile);

                this.WhenAnyValue(v => v.ViewModel.IsSelected)
                    .Subscribe(isSelected => this.Border.Background = isSelected
                        ? (Brush)this.FindResource("MaterialDesignSelection")
                        : Brushes.Transparent)
                    .DisposeWith(disposables);
            });
        }
    }
}
