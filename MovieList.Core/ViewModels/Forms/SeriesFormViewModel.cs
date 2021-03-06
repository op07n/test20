using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Resources;
using System.Threading.Tasks;

using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;

using MovieList.Data.Models;
using MovieList.Data.Services;
using MovieList.DialogModels;
using MovieList.ViewModels.Forms.Base;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

using Splat;

using static MovieList.Data.Constants;

namespace MovieList.ViewModels.Forms
{
    public sealed class SeriesFormViewModel : MovieSeriesEntryFormBase<Series, SeriesFormViewModel>
    {
        private readonly IEntityService<Series> seriesService;
        private readonly ISettingsService settingsService;

        private readonly SourceList<EntityBase> componentsSource = new SourceList<EntityBase>();

        private readonly ReadOnlyObservableCollection<ISeriesComponentForm> componentForms;
        private readonly ReadOnlyObservableCollection<SeasonFormViewModel> seasons;
        private readonly ReadOnlyObservableCollection<SpecialEpisodeFormViewModel> specialEpisodes;
        private readonly ReadOnlyObservableCollection<SeriesComponentViewModel> components;

        private readonly BehaviorSubject<int> maxSequenceNumberSubject = new BehaviorSubject<int>(0);
        private readonly Subject<Unit> resort = new Subject<Unit>();

        public SeriesFormViewModel(
            Series series,
            ReadOnlyObservableCollection<Kind> kinds,
            string fileName,
            ResourceManager? resourceManager = null,
            IScheduler? scheduler = null,
            IEntityService<Series>? seriesService = null,
            ISettingsService? settingsService = null)
            : base(series.Entry, resourceManager, scheduler)
        {
            this.Series = series;
            this.Kinds = kinds;

            this.seriesService = seriesService ?? Locator.Current.GetService<IEntityService<Series>>(fileName);
            this.settingsService = settingsService ?? Locator.Current.GetService<ISettingsService>(fileName);

            this.SelectComponent = ReactiveCommand.Create<ISeriesComponentForm, ISeriesComponentForm>(
                form => form);

            this.componentsSource.Connect()
                .Transform(this.CreateForm)
                .Sort(
                    SortExpressionComparer<ISeriesComponentForm>.Ascending(form => form.SequenceNumber),
                    resort: this.resort)
                .Bind(out this.componentForms)
                .DisposeMany()
                .Subscribe();

            this.ComponentForms.ToObservableChangeSet()
                .Transform(this.CreateComponent)
                .Bind(out this.components)
                .DisposeMany()
                .Subscribe();

            this.ComponentForms.ToObservableChangeSet()
                .Filter(form => form is SeasonFormViewModel)
                .Transform(form => (SeasonFormViewModel)form)
                .Bind(out this.seasons)
                .DisposeMany()
                .Subscribe();

            this.ComponentForms.ToObservableChangeSet()
                .Filter(form => form is SpecialEpisodeFormViewModel)
                .Transform(form => (SpecialEpisodeFormViewModel)form)
                .Bind(out this.specialEpisodes)
                .DisposeMany()
                .Subscribe();

            this.Components.ToObservableChangeSet()
                .AutoRefresh(component => component.SequenceNumber)
                .ToCollection()
                .Select(components => components.Count != 0 ? components.Max(c => c.SequenceNumber) : 0)
                .Subscribe(this.maxSequenceNumberSubject);

            this.CopyProperties();

            this.ImdbLinkRule = this.ValidationRule(vm => vm.ImdbLink, link => link.IsUrl(), "ImdbLinkInvalid");
            this.PosterUrlRule = this.ValidationRule(vm => vm.PosterUrl, url => url.IsUrl(), "PosterUrlInvalid");

            this.AddSeason = ReactiveCommand.CreateFromTask(this.AddSeasonAsync);
            this.AddSpecialEpisode = ReactiveCommand.Create(this.OnAddSpecialEpisode);
            this.ConvertToMiniseries = ReactiveCommand.Create(() => { }, this.CanConvertToMiniseries);

            this.AddSeason
                .Merge(this.AddSpecialEpisode)
                .SelectMany(_ => this.ComponentForms.MaxBy(season => season.SequenceNumber))
                .Cast<ISeriesComponentForm>()
                .InvokeCommand(this.SelectComponent);

            this.CanDeleteWhenNotChanged();
            this.CanCreateMovieSeries();
            this.EnableChangeTracking();
        }

        public Series Series { get; }

        public ReadOnlyObservableCollection<Kind> Kinds { get; }

        [Reactive]
        public Kind Kind { get; set; } = null!;

        [Reactive]
        public bool IsAnthology { get; set; }

        [Reactive]
        public SeriesWatchStatus WatchStatus { get; set; }

        [Reactive]
        public SeriesReleaseStatus ReleaseStatus { get; set; }

        public ReadOnlyObservableCollection<ISeriesComponentForm> ComponentForms
            => this.componentForms;

        public ReadOnlyObservableCollection<SeasonFormViewModel> Seasons
            => this.seasons;

        public ReadOnlyObservableCollection<SpecialEpisodeFormViewModel> SpecialEpisodes
            => this.specialEpisodes;

        public ReadOnlyObservableCollection<SeriesComponentViewModel> Components
            => this.components;

        [Reactive]
        public string ImdbLink { get; set; } = String.Empty;

        [Reactive]
        public string PosterUrl { get; set; } = String.Empty;

        public ValidationHelper ImdbLinkRule { get; }
        public ValidationHelper PosterUrlRule { get; }

        public ReactiveCommand<Unit, Unit> AddSeason { get; }
        public ReactiveCommand<Unit, Unit> AddSpecialEpisode { get; }
        public ReactiveCommand<ISeriesComponentForm, ISeriesComponentForm> SelectComponent { get; }
        public ReactiveCommand<Unit, Unit> ConvertToMiniseries { get; }

        public override bool IsNew
            => this.Series.Id == default;

        protected override SeriesFormViewModel Self
            => this;

        protected override ICollection<Title> ItemTitles
            => this.Series.Titles;

        protected override string NewItemKey
            => "NewSeries";

        private IObservable<bool> CanConvertToMiniseries
            => Observable.CombineLatest(
                    this.Seasons.ToObservableChangeSet()
                        .Count()
                        .StartWith(0)
                        .Select(count => count <= 1),
                    this.Seasons.ToObservableChangeSet()
                        .TransformMany(season => season.Periods)
                        .Count()
                        .StartWith(0)
                        .Select(count => count <= 1),
                    this.SpecialEpisodes.ToObservableChangeSet()
                        .Count()
                        .StartWith(0)
                        .Select(count => count == 0))
                .AllTrue();

        public async Task AddSeasonAsync()
        {
            int seasonNumber = this.Seasons.Count + 1;

            var previousComponent = this.ComponentForms.Count != 0 ? this.ComponentForms[^1] : null;
            int year = previousComponent?.GetNextYear() ?? SeriesDefaultYear;

            var period = new Period
            {
                StartMonth = 1,
                StartYear = year,
                EndMonth = 1,
                EndYear = year,
                NumberOfEpisodes = 1
            };

            var settings = await this.settingsService.GetSettingsAsync();

            var season = new Season
            {
                Titles = new List<Title>
                {
                    new Title { Name = settings.GetSeasonTitle(seasonNumber), Priority = 1, IsOriginal = false },
                    new Title { Name = settings.GetSeasonOriginalTitle(seasonNumber), Priority = 1, IsOriginal = true }
                },
                Series = this.Series,
                Periods = new List<Period> { period },
                SequenceNumber = this.Components.Count + 1,
                Channel = previousComponent?.Channel ?? String.Empty
            };

            period.Season = season;

            this.componentsSource.Add(season);
        }

        protected override void EnableChangeTracking()
        {
            this.TrackChanges(vm => vm.WatchStatus, vm => vm.Series.WatchStatus);
            this.TrackChanges(vm => vm.ReleaseStatus, vm => vm.Series.ReleaseStatus);
            this.TrackChanges(vm => vm.Kind, vm => vm.Series.Kind);
            this.TrackChanges(vm => vm.IsAnthology, vm => vm.Series.IsAnthology);
            this.TrackChanges(vm => vm.ImdbLink, vm => vm.Series.ImdbLink.EmptyIfNull());
            this.TrackChanges(vm => vm.PosterUrl, vm => vm.Series.PosterUrl.EmptyIfNull());
            this.TrackChanges(this.IsCollectionChanged(vm => vm.Seasons, vm => vm.Series.Seasons));
            this.TrackChanges(this.IsCollectionChanged(vm => vm.SpecialEpisodes, vm => vm.Series.SpecialEpisodes));

            if (this.Series.IsMiniseries && !this.IsNew)
            {
                this.TrackChanges(Observable.Return(true).Merge(this.Save.Select(_ => false)));
            }

            this.TrackValidation(this.IsCollectionValid<SeasonFormViewModel, Season>(this.Seasons));
            this.TrackValidation(this.IsCollectionValid<SpecialEpisodeFormViewModel, SpecialEpisode>(
                this.SpecialEpisodes));

            this.TrackValidationStrict(this.componentsSource.Connect().Count().Select(count => count > 0));

            base.EnableChangeTracking();
        }

        protected override async Task<Series> OnSaveAsync()
        {
            await this.SaveTitlesAsync();
            await this.SaveSeasonsAsync();
            await this.SaveSpecialEpisodesAsync();

            this.Series.IsMiniseries = false;
            this.Series.IsAnthology = this.IsAnthology;
            this.Series.WatchStatus = this.WatchStatus;
            this.Series.ReleaseStatus = this.ReleaseStatus;
            this.Series.Kind = this.Kind;
            this.Series.ImdbLink = this.ImdbLink.NullIfEmpty();
            this.Series.PosterUrl = this.PosterUrl.NullIfEmpty();

            await this.seriesService.SaveAsync(this.Series);

            return this.Series;
        }

        protected override async Task<Series?> OnDeleteAsync()
        {
            bool shouldDelete = await Dialog.Confirm.Handle(new ConfirmationModel("DeleteSeries"));

            if (shouldDelete)
            {
                await this.seriesService.DeleteAsync(this.Series);
                return this.Series;
            }

            return null;
        }

        protected override void CopyProperties()
        {
            base.CopyProperties();

            this.componentsSource.Edit(components =>
            {
                components.Clear();
                components.AddRange(this.Series.Seasons);
                components.AddRange(this.Series.SpecialEpisodes);
            });

            this.resort.OnNext(Unit.Default);

            this.IsAnthology = this.Series.IsAnthology;
            this.Kind = this.Series.Kind;
            this.WatchStatus = this.Series.WatchStatus;
            this.ReleaseStatus = this.Series.ReleaseStatus;
            this.ImdbLink = this.Series.ImdbLink.EmptyIfNull();
            this.PosterUrl = this.Series.PosterUrl.EmptyIfNull();
        }

        protected override void AttachTitle(Title title)
            => title.Series = this.Series;

        private void OnAddSpecialEpisode()
        {
            var previousComponent = this.ComponentForms.Count != 0 ? this.ComponentForms[^1] : null;

            this.componentsSource.Add(new SpecialEpisode
            {
                Titles = new List<Title>
                {
                    new Title { Priority = 1, IsOriginal = false },
                    new Title { Priority = 1, IsOriginal = true }
                },
                Series = this.Series,
                SequenceNumber = this.Components.Count + 1,
                Channel = previousComponent?.Channel ?? String.Empty,
                Month = 1,
                Year = previousComponent?.GetNextYear() ?? SeriesDefaultYear
            });
        }

        private ISeriesComponentForm CreateForm(EntityBase entity)
            => entity switch
            {
                Season season => this.CreateSeasonForm(season),
                SpecialEpisode episode => this.CreateSpecialEpisodeForm(episode),
                _ => throw new NotSupportedException($"Cannot create a form for entity {entity.GetType()}")
            };

        private SeasonFormViewModel CreateSeasonForm(Season season)
        {
            var seasonForm = new SeasonFormViewModel(
                season, this, this.maxSequenceNumberSubject, this.ResourceManager, this.Scheduler);

            this.SubscribeToFormEvents(seasonForm);

            return seasonForm;
        }

        private SpecialEpisodeFormViewModel CreateSpecialEpisodeForm(SpecialEpisode episode)
        {
            var episodeForm = new SpecialEpisodeFormViewModel(
                episode, this, this.maxSequenceNumberSubject, this.ResourceManager, this.Scheduler);

            this.SubscribeToFormEvents(episodeForm);

            return episodeForm;
        }

        private void SubscribeToFormEvents<TM, TVm>(SeriesComponentFormBase<TM, TVm> componentForm)
            where TM : EntityBase
            where TVm : SeriesComponentFormBase<TM, TVm>
        {
            componentForm.Delete
                .WhereNotNull()
                .Subscribe(s => this.componentsSource.Remove(s));

            componentForm.GoToNext
                .Select(_ => this.Components.First(component =>
                    component.SequenceNumber == componentForm.SequenceNumber + 1))
                .Select(component => component.Form)
                .SubscribeAsync(async form => await this.SelectComponent.Execute(form));

            componentForm.GoToPrevious
                .Select(_ => this.Components.First(component =>
                    component.SequenceNumber == componentForm.SequenceNumber - 1))
                .Select(component => component.Form)
                .SubscribeAsync(async form => await this.SelectComponent.Execute(form));

            componentForm.WhenAnyValue(form => form.SequenceNumber)
                .StartWith(componentForm.SequenceNumber)
                .Buffer(2, 1)
                .Select(values => (Old: values[0], New: values[1]))
                .Where(values => values.Old != values.New)
                .Subscribe(values => this.ComponentForms
                    .Where(form => form != componentForm && form.SequenceNumber == componentForm.SequenceNumber)
                    .ForEach(form => form.SequenceNumber = values.New < values.Old
                        ? form.SequenceNumber + 1
                        : form.SequenceNumber - 1));

            componentForm.WhenAnyValue(form => form.SequenceNumber)
                .Discard()
                .Subscribe(this.resort);
        }

        private SeriesComponentViewModel CreateComponent(ISeriesComponentForm form)
        {
            var component = SeriesComponentViewModel.FromForm(form);

            component.Select.InvokeCommand(this.SelectComponent);

            return component;
        }

        private async Task SaveSeasonsAsync()
        {
            foreach (var season in this.Seasons)
            {
                await season.Save.Execute();
            }

            foreach (var season in this.componentsSource.Items
                .OfType<Season>()
                .Except(this.Series.Seasons)
                .ToList())
            {
                this.Series.Seasons.Add(season);
            }

            foreach (var season in this.Series.Seasons
                .Except(this.componentsSource.Items.OfType<Season>())
                .ToList())
            {
                this.Series.Seasons.Remove(season);
            }
        }

        private async Task SaveSpecialEpisodesAsync()
        {
            foreach (var episode in this.SpecialEpisodes)
            {
                await episode.Save.Execute();
            }

            foreach (var episode in this.componentsSource.Items
                .OfType<SpecialEpisode>()
                .Except(this.Series.SpecialEpisodes)
                .ToList())
            {
                this.Series.SpecialEpisodes.Add(episode);
            }

            foreach (var episode in this.Series.SpecialEpisodes
                .Except(this.componentsSource.Items.OfType<SpecialEpisode>())
                .ToList())
            {
                this.Series.SpecialEpisodes.Remove(episode);
            }
        }
    }
}
