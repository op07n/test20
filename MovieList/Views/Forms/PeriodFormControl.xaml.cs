using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using MaterialDesignThemes.Wpf;

using MovieList.Properties;
using MovieList.ViewModels.Forms;

using ReactiveUI;

namespace MovieList.Views.Forms
{
    public abstract class PeriodFormControlBase : ReactiveUserControl<PeriodFormViewModel> { }

    public partial class PeriodFormControl : PeriodFormControlBase
    {
        public PeriodFormControl()
        {
            this.InitializeComponent();

            this.WhenActivated(disposables =>
            {
                this.WhenAnyValue(v => v.ViewModel)
                    .BindTo(this, v => v.DataContext)
                    .DisposeWith(disposables);

                foreach (string month in Properties.MonthNames)
                {
                    this.StartMonthComboBox.Items.Add(month);
                    this.EndMonthComboBox.Items.Add(month);
                }

                this.BindFields(disposables);
                this.AddValidation(disposables);

                this.BindCommand(this.ViewModel, vm => vm.Delete, v => v.DeleteButton)
                    .DisposeWith(disposables);

                this.WhenAnyObservable(v => v.ViewModel.Delete.CanExecute)
                    .BindTo(this, v => v.DeleteButton.Visibility, null, new BooleanToVisibilityTypeConverter())
                    .DisposeWith(disposables);
            });
        }

        public void BindFields(CompositeDisposable disposables)
        {
            this.Bind(
                    this.ViewModel,
                    vm => vm.StartMonth,
                    v => v.StartMonthComboBox.SelectedIndex,
                    month => month - 1,
                    index => index + 1)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.IsSingleDayRelease)
                .Subscribe(isSingleDayRelease => HintAssist.SetHint(
                    this.StartMonthComboBox, isSingleDayRelease ? Messages.Month : Messages.StartMonth));

            this.Bind(this.ViewModel, vm => vm.StartYear, v => v.StartYearTextBox.Text)
                .DisposeWith(disposables);

            this.StartYearTextBox.ValidateWith(this.ViewModel.StartYearRule)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.IsSingleDayRelease)
                .Subscribe(isSingleDayRelease => HintAssist.SetHint(
                    this.StartYearTextBox, isSingleDayRelease ? Messages.Year : Messages.StartYear));

            var boolToVisibility = new BooleanToVisibilityTypeConverter();

            this.Bind(
                    this.ViewModel,
                    vm => vm.EndMonth,
                    v => v.EndMonthComboBox.SelectedIndex,
                    month => month - 1,
                    index => index + 1)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.IsSingleDayRelease)
                .Invert()
                .BindTo(this, v => v.EndMonthComboBox.Visibility, null, boolToVisibility);

            this.Bind(this.ViewModel, vm => vm.EndYear, v => v.EndYearTextBox.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.IsSingleDayRelease)
                .Invert()
                .BindTo(this, v => v.EndYearTextBox.Visibility, null, boolToVisibility);

            this.Bind(this.ViewModel, vm => vm.NumberOfEpisodes, v => v.NumberOfEpisodesTextBox.Text)
                .DisposeWith(disposables);

            this.Bind(this.ViewModel, vm => vm.IsSingleDayRelease, v => v.IsSingleDayReleaseCheckBox.IsChecked)
                .DisposeWith(disposables);

            this.WhenAnyValue(
                    v => v.ViewModel.StartMonth,
                    v => v.ViewModel.StartYear,
                    v => v.ViewModel.EndMonth,
                    v => v.ViewModel.EndYear)
                .Select(((int StartMonth, string StartYear, int EndMonth, string EndYear) values) =>
                    values.StartMonth == values.EndMonth && values.StartYear == values.EndYear)
                .BindTo(this, v => v.IsSingleDayReleaseCheckBox.IsEnabled)
                .DisposeWith(disposables);

            this.Bind(this.ViewModel, vm => vm.PosterUrl, v => v.PosterUrlTextBox.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.ShowPosterUrl)
                .BindTo(this, v => v.PosterUrlTextBox.Visibility, null, boolToVisibility);
        }

        public void AddValidation(CompositeDisposable disposables)
        {
            this.EndYearTextBox.ValidateWith(this.ViewModel.EndYearRule)
                .DisposeWith(disposables);

            this.NumberOfEpisodesTextBox.ValidateWith(this.ViewModel.NumberOfEpisodesRule)
                .DisposeWith(disposables);

            this.PosterUrlTextBox.ValidateWith(this.ViewModel.PosterUrlRule)
                .DisposeWith(disposables);

            this.ShowValidationMessage(this.ViewModel.PeriodRule, v => v.InvalidFormTextBlock.Text)
                .DisposeWith(disposables);

            this.WhenAnyObservable(v => v.ViewModel.PeriodRule.ValidationChanged)
                .Select(state => !state.IsValid)
                .BindTo(this, v => v.InvalidFormTextBlock.Visibility, null, new BooleanToVisibilityTypeConverter())
                .DisposeWith(disposables);
        }
    }
}
