using System;
using System.Collections.Generic;
using System.Linq;

using Dapper.Contrib.Extensions;

namespace MovieList.Data.Models
{
    [Table("Series")]
    public sealed class Series : EntityBase
    {
        public bool IsMiniseries { get; set; }
        public bool IsAnthology { get; set; }

        public SeriesWatchStatus WatchStatus { get; set; } = SeriesWatchStatus.NotWatched;
        public SeriesReleaseStatus ReleaseStatus { get; set; } = SeriesReleaseStatus.NotStarted;

        public string? ImdbLink { get; set; }
        public string? PosterUrl { get; set; }

        public int KindId { get; set; }

        [Write(false)]
        public Kind Kind { get; set; } = null!;

        [Write(false)]
        public MovieSeriesEntry? Entry { get; set; }

        [Write(false)]
        public IList<Title> Titles { get; set; } = new List<Title>();

        [Write(false)]
        public IList<Season> Seasons { get; set; } = new List<Season>();

        [Write(false)]
        public IList<SpecialEpisode> SpecialEpisodes { get; set; } = new List<SpecialEpisode>();

        [Computed]
        public Title Title
            => this.Titles
                .Where(title => !title.IsOriginal)
                .OrderBy(title => title.Priority)
                .First();

        [Computed]
        public Title OriginalTitle
            => this.Titles
                .Where(title => title.IsOriginal)
                .OrderBy(title => title.Priority)
                .First();

        [Computed]
        public int StartYear
            => Math.Min(
                this.Seasons
                    .OrderBy(season => season.StartYear)
                    .FirstOrDefault()
                    ?.StartYear
                    ?? Int32.MaxValue,
                this.SpecialEpisodes
                    .OrderBy(episode => episode.Year)
                    .ThenBy(episode => episode.Month)
                    .FirstOrDefault()
                    ?.Year
                    ?? Int32.MaxValue);

        [Computed]
        public int EndYear
            => Math.Max(
                this.Seasons
                    .OrderByDescending(season => season.EndYear)
                    .FirstOrDefault()
                    ?.EndYear
                    ?? Int32.MinValue,
                this.SpecialEpisodes
                    .OrderByDescending(episode => episode.Year)
                    .ThenByDescending(episode => episode.Month)
                    .FirstOrDefault()
                    ?.Year
                    ?? Int32.MinValue);

        public override string ToString()
            => $"Series #{this.Id}: {Title.ToString(this.Titles)}";
    }
}
