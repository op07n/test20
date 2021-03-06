using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

using Dapper.Contrib.Extensions;

using Microsoft.Data.Sqlite;

using MovieList.Data.Models;

using Splat;

namespace MovieList.Data.Services.Implementations
{
    internal class ListService : ServiceBase, IListService
    {
        public ListService(string file)
            : base(file)
        { }

        public Task<MovieList> GetListAsync(IList<Kind> kinds)
            => this.WithTransactionAsync((connection, transaction) =>
                this.GetListAsync(kinds, connection, transaction));

        private async Task<MovieList> GetListAsync(
            IList<Kind> kinds,
            SqliteConnection connection,
            IDbTransaction transaction)
        {
            this.Log().Debug("Getting the full list of movies, series and movie series.");

            var titles = await connection.GetAllAsync<Title>(transaction).ToListAsync();
            var entries = await connection.GetAllAsync<MovieSeriesEntry>(transaction).ToListAsync();

            var seasons = await connection.GetAllAsync<Season>(transaction).ToListAsync();
            var periods = await connection.GetAllAsync<Period>(transaction).ToListAsync();
            var specialEpisodes = await connection.GetAllAsync<SpecialEpisode>(transaction).ToListAsync();

            var movies = await connection.GetAllAsync<Movie>(transaction).ToListAsync();
            var series = await connection.GetAllAsync<Series>(transaction).ToListAsync();
            var movieSeries = await connection.GetAllAsync<MovieSeries>(transaction).ToListAsync();

            return new MovieList(
                movies
                    .Join(kinds)
                    .Join(titles)
                    .Join(entries)
                    .ToList(),
                series
                    .Join(kinds)
                    .Join(titles)
                    .Join(seasons.Join(periods).Join(titles))
                    .Join(specialEpisodes.Join(titles))
                    .Join(entries)
                    .ToList(),
                movieSeries
                    .Join(titles)
                    .Join(entries)
                    .ToList());
        }
    }
}
