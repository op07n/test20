using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Akavache;

using DynamicData;
using DynamicData.Binding;
using MovieList.DialogModels;
using MovieList.Preferences;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Splat;

using static MovieList.Constants;

namespace MovieList.ViewModels
{
    public sealed class HomePageViewModel : ReactiveObject
    {
        private readonly IBlobCache store;
        private readonly ReadOnlyObservableCollection<RecentFileViewModel> recentFiles;
        private readonly SourceCache<RecentFileViewModel, string> recentFilesSource
            = new SourceCache<RecentFileViewModel, string>(vm => vm.File.Path);

        public HomePageViewModel(IBlobCache? store = null)
        {
            this.store = store ?? Locator.Current.GetService<IBlobCache>(StoreKey);

            this.recentFilesSource.Connect()
                .Sort(SortExpressionComparer<RecentFileViewModel>.Descending(vm => vm.File.Closed))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out this.recentFiles)
                .DisposeMany()
                .Subscribe();

            this.store.GetObject<UserPreferences>(PreferencesKey)
                .SelectMany(preferences => preferences.File.RecentFiles)
                .Select(file => new RecentFileViewModel(file, this))
                .Subscribe(recentFilesSource.AddOrUpdate);

            this.WhenAnyValue(vm => vm.RecentFiles.Count)
                .Select(count => count != 0)
                .ToPropertyEx(this, vm => vm.RecentFilesPresent);

            this.CreateFile = ReactiveCommand.CreateFromTask(this.OnCreateFileAsync);
            this.OpenFile = ReactiveCommand.CreateFromTask<string?, string?>(this.OnOpenFileAsync);
            this.OpenRecentFile = ReactiveCommand.CreateFromTask<string, string?>(this.OnOpenRecentFileAsync);

            var canRemoveSelectedRecentFiles = this.recentFilesSource.Connect()
                .AutoRefresh(file => file.IsSelected)
                .ToCollection()
                .Select(files => files.Any(file => file.IsSelected));

            this.RemoveSelectedRecentFiles = ReactiveCommand.CreateFromTask(
                this.OnRemoveSelectedRecentFilesAsync, canRemoveSelectedRecentFiles);

            this.AddRecentFile = ReactiveCommand.Create<RecentFile>(
                file => this.recentFilesSource.AddOrUpdate(new RecentFileViewModel(file, this)));

            this.RemoveRecentFile = ReactiveCommand.Create<RecentFile>(
                file => this.recentFilesSource.RemoveKey(file.Path));

            this.OpenRecentFile
                .WhereNotNull()
                .InvokeCommand(this.OpenFile);
        }

        public ReadOnlyObservableCollection<RecentFileViewModel> RecentFiles
            => this.recentFiles;

        public bool RecentFilesPresent { [ObservableAsProperty] get; }

        public ReactiveCommand<Unit, CreateFileModel?> CreateFile { get; }
        public ReactiveCommand<string?, string?> OpenFile { get; }
        public ReactiveCommand<string, string?> OpenRecentFile { get; }

        public ReactiveCommand<Unit, Unit> RemoveSelectedRecentFiles { get; }

        public ReactiveCommand<RecentFile, Unit> AddRecentFile { get; }
        public ReactiveCommand<RecentFile, Unit> RemoveRecentFile { get; }

        private async Task<CreateFileModel?> OnCreateFileAsync()
        {
            this.Log().Debug("Creating a new list.");

            string? listName = await Dialog.Input.Handle(new InputModel("CreateListMessage", "CreateListTitle"));

            if (listName is null)
            {
                return null;
            }

            string? fileName = await Dialog.SaveFile.Handle(listName);

            return fileName is null ? null : new CreateFileModel(fileName, listName);
        }

        private async Task<string?> OnOpenFileAsync(string? fileName)
        {
            this.Log().Debug(fileName is null ? "Opening a list." : $"Opening a list: {fileName}.");
            return fileName ?? await Dialog.OpenFile.Handle(Unit.Default);
        }

        private async Task<string?> OnOpenRecentFileAsync(string fileName)
        {
            if (File.Exists(fileName))
            {
                return fileName;
            }

            bool shouldRemoveFile = await Dialog.Confirm.Handle(
                new ConfirmationModel("RemoveRecentFileQuesiton", "RemoveRecentFileTitle"));

            if (shouldRemoveFile)
            {
                var preferences = await this.store.GetObject<UserPreferences>(PreferencesKey);

                this.Log().Debug($"Removing recent file: {fileName}.");

                this.recentFilesSource.Remove(fileName);
                preferences.File.RecentFiles.RemoveAll(file => file.Path == fileName);

                await this.store.InsertObject(PreferencesKey, preferences);
            }

            return null;
        }

        private async Task OnRemoveSelectedRecentFilesAsync()
        {
            var preferences = await this.store.GetObject<UserPreferences>(PreferencesKey);

            var filesToRemove = this.recentFiles
                .Where(file => file.IsSelected)
                .ToList();

            string fileNames = filesToRemove
                .Select(file => file.File.Name)
                .Aggregate((acc, file) => $"{acc}, {file}");

            this.Log().Debug($"Removing recent files: {fileNames}.");

            this.recentFilesSource.Remove(filesToRemove);
            preferences.File.RecentFiles.RemoveMany(filesToRemove.Select(file => file.File));

            await this.store.InsertObject(PreferencesKey, preferences);
        }
    }
}
