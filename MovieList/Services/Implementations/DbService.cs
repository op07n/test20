using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MovieList.Config;
using MovieList.Data;
using MovieList.Data.Models;
using MovieList.ViewModels;
using MovieList.ViewModels.ListItems;

namespace MovieList.Services.Implementations
{
    public class DbService : IDbService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IOptions<Configuration> config;

        public DbService(IServiceProvider serviceProvider, IOptions<Configuration> config)
        {
            this.serviceProvider = serviceProvider;
            this.config = config;
        }

        public async Task<List<ListItem>> LoadListAsync()
        {
            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            var movies = await context.Movies
                .Include(context.GetIncludePaths(typeof(Movie)))
                .ToListAsync();

            var series = await context.Series
                .Include(context.GetIncludePaths(typeof(Series)))
                .ToListAsync();

            var movieSeries = await context.MovieSeries
                .Include(context.GetIncludePaths(typeof(MovieSeries)))
                .ToListAsync();

            return movies
                .Select(movie => new MovieListItem(movie, this.config.Value))
                .Cast<ListItem>()
                .Union(series.Select(series => new SeriesListItem(series, this.config.Value)))
                .Union(movieSeries
                    .Where(series => series.Title != null)
                    .Select(series => new MovieSeriesListItem(series, this.config.Value)))
                .OrderBy(item => item)
                .ToList();
        }

        public async Task<ObservableCollection<KindViewModel>> LoadAllKindsAsync()
        {
            using var context = serviceProvider.GetRequiredService<MovieContext>();

            return new ObservableCollection<KindViewModel>(
                await context.Kinds
                    .Include(context.GetIncludePaths(typeof(Kind)))
                    .OrderBy(k => k.Id)
                    .Select(k => new KindViewModel(k))
                    .ToListAsync());
        }

        public async Task SaveMovieAsync(Movie movie)
        {
            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            if (movie.Id == default)
            {
                context.Attach(movie.Kind);
                context.Add(movie);
            } else
            {
                context.Entry(movie).State = EntityState.Modified;

                foreach (var title in movie.Titles)
                {
                    if (title.Id == default)
                    {
                        context.Add(title);
                    } else
                    {
                        context.Entry(title).State = EntityState.Modified;
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        public Task SaveSeriesAsync(Series series)
        {
            throw new NotImplementedException();
        }

        public async Task SaveKindsAsync(IEnumerable<KindViewModel> kinds)
        {
            if (kinds.Any(k => k.HasErrors))
            {
                throw new ArgumentException("Cannot save invalid kinds.", nameof(kinds));
            }

            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            var dbKinds = await context.Kinds
                .Include(context.GetIncludePaths(typeof(Kind)))
                .AsNoTracking()
                .ToListAsync();

            var kindsToSave = kinds.Select(k => k.Kind).ToList();

            foreach (var kind in kindsToSave)
            {
                if (await context.Kinds.ContainsAsync(kind))
                {
                    context.Attach(kind).State = EntityState.Modified;
                } else
                {
                    context.Kinds.Add(kind);
                }
            }

            foreach (var kind in dbKinds.Except(kindsToSave, IdEqualityComparer<Kind>.Instance))
            {
                context.Attach(kind).State = EntityState.Deleted;
            }

            await context.SaveChangesAsync();
        }

        public async Task ToggleWatchedAsync(ListItem item)
        {
            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            switch (item)
            {
                case MovieListItem movieItem:
                    movieItem.Movie.IsWatched = !movieItem.Movie.IsWatched;
                    context.Attach(movieItem.Movie).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    break;
                case SeriesListItem seriesItem:
                    seriesItem.Series.IsWatched = !seriesItem.Series.IsWatched;
                    context.Attach(seriesItem.Series).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    break;
            }
        }

        public async Task ToggleReleasedAsync(MovieListItem item)
        {
            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            item.Movie.IsReleased = !item.Movie.IsReleased;
            context.Attach(item.Movie).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync<TEntity>(TEntity entity)
            where TEntity : EntityBase
        {
            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            context.Attach(entity).State = EntityState.Deleted;
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync<TEntity>(IEnumerable<TEntity> entities)
            where TEntity : EntityBase
        {
            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            foreach (var entity in entities)
            {
                context.Attach(entity).State = EntityState.Deleted;
            }

            await context.SaveChangesAsync();
        }

        public async Task<bool> CanDeleteKindAsync(KindViewModel kind)
        {
            using var context = this.serviceProvider.GetRequiredService<MovieContext>();

            return (await context.Movies.Where(m => m.KindId == kind.Kind.Id).CountAsync()) == 0 &&
                (await context.Series.Where(s => s.KindId == kind.Kind.Id).CountAsync()) == 0;
        }
    }
}