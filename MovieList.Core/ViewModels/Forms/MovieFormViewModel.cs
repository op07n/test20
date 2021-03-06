using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Resources;
using System.Threading.Tasks;

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
    public sealed class MovieFormViewModel : MovieSeriesEntryFormBase<Movie, MovieFormViewModel>
    {
        private readonly IEntityService<Movie> movieService;

        public MovieFormViewModel(
            Movie movie,
            ReadOnlyObservableCollection<Kind> kinds,
            string fileName,
            ResourceManager? resourceManager = null,
            IScheduler? scheduler = null,
            IEntityService<Movie>? movieService = null)
            : base(movie.Entry, resourceManager, scheduler)
        {
            this.Movie = movie;
            this.Kinds = kinds;

            this.movieService = movieService ?? Locator.Current.GetService<IEntityService<Movie>>(fileName);

            this.CopyProperties();

            this.YearRule = this.ValidationRule(vm => vm.Year, MovieMinYear, MovieMaxYear, nameof(this.Year));
            this.ImdbLinkRule = this.ValidationRule(vm => vm.ImdbLink, link => link.IsUrl(), "ImdbLinkInvalid");
            this.PosterUrlRule = this.ValidationRule(vm => vm.PosterUrl, url => url.IsUrl(), "PosterUrlInvalid");

            this.InitializeValueDependencies();
            this.CanDeleteWhenNotChanged();
            this.CanCreateMovieSeries();
            this.EnableChangeTracking();
        }

        public Movie Movie { get; }

        public ReadOnlyObservableCollection<Kind> Kinds { get; }

        [Reactive]
        public string Year { get; set; } = String.Empty;

        [Reactive]
        public Kind Kind { get; set; } = null!;

        [Reactive]
        public bool IsWatched { get; set; }

        [Reactive]
        public bool IsReleased { get; set; }

        [Reactive]
        public string ImdbLink { get; set; } = null!;

        [Reactive]
        public string PosterUrl { get; set; } = null!;

        public ValidationHelper YearRule { get; }
        public ValidationHelper ImdbLinkRule { get; }
        public ValidationHelper PosterUrlRule { get; }

        public override bool IsNew
            => this.Movie.Id == default;

        protected override MovieFormViewModel Self
            => this;

        protected override ICollection<Title> ItemTitles
            => this.Movie.Titles;

        protected override string NewItemKey
            => "NewMovie";

        protected override void EnableChangeTracking()
        {
            this.TrackChanges(vm => vm.Year, vm => vm.Movie.Year.ToString());
            this.TrackChanges(vm => vm.Kind, vm=> vm.Movie.Kind);
            this.TrackChanges(vm => vm.IsWatched, vm => vm.Movie.IsWatched);
            this.TrackChanges(vm => vm.IsReleased, vm => vm.Movie.IsReleased);
            this.TrackChanges(vm => vm.ImdbLink, vm => vm.Movie.ImdbLink.EmptyIfNull());
            this.TrackChanges(vm => vm.PosterUrl, vm => vm.Movie.PosterUrl.EmptyIfNull());

            base.EnableChangeTracking();
        }

        protected override async Task<Movie> OnSaveAsync()
        {
            await this.SaveTitlesAsync();

            this.Movie.IsWatched = this.IsWatched;
            this.Movie.IsReleased = this.IsReleased;
            this.Movie.Year = Int32.Parse(this.Year);
            this.Movie.Kind = this.Kind;
            this.Movie.ImdbLink = this.ImdbLink.NullIfEmpty();
            this.Movie.PosterUrl = this.PosterUrl.NullIfEmpty();

            await this.movieService.SaveAsync(this.Movie);

            return this.Movie;
        }

        protected override async Task<Movie?> OnDeleteAsync()
        {
            bool shouldDelete = await Dialog.Confirm.Handle(new ConfirmationModel("DeleteMovie"));

            if (shouldDelete)
            {
                await this.movieService.DeleteAsync(this.Movie);
                return this.Movie;
            }

            return null;
        }

        protected override void CopyProperties()
        {
            base.CopyProperties();

            this.Year = this.Movie.Year.ToString();
            this.Kind = this.Movie.Kind;
            this.IsWatched = this.Movie.IsWatched;
            this.IsReleased = this.Movie.IsReleased;
            this.ImdbLink = this.Movie.ImdbLink.EmptyIfNull();
            this.PosterUrl = this.Movie.PosterUrl.EmptyIfNull();
        }

        protected override void AttachTitle(Title title)
            => title.Movie = this.Movie;

        private void InitializeValueDependencies()
        {
            this.WhenAnyValue(vm => vm.IsReleased)
                .Where(isReleased => !isReleased)
                .Subscribe(_ => this.IsWatched = false);

            this.WhenAnyValue(vm => vm.IsWatched)
                .Where(isWatched => isWatched)
                .Subscribe(_ => this.IsReleased = true);

            this.WhenAnyValue(vm => vm.Year)
                .Where(_ => this.YearRule.IsValid)
                .Select(Int32.Parse)
                .Where(year => year != this.Scheduler.Now.Year)
                .Subscribe(year => this.IsReleased = year < this.Scheduler.Now.Year);
        }
    }
}
