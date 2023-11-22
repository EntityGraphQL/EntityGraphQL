using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Benchmarks
{
    public class MovieData
    {
        public int Year { get; set; }
        public string Title { get; set; }

        public MovieData(string title, MoveInfo info)
        {
            Title = title;
            Info = info;
        }

        public MoveInfo Info { get; set; }
    }

    public class MoveInfo
    {
        public MoveInfo(List<string> directors, DateTime releaseDate, float rating, List<string> genres, string imageUrl, string plot, int rank, int runningTimeSecs, List<string> actors)
        {
            Directors = directors;
            ReleaseDate = releaseDate;
            Rating = rating;
            Genres = genres;
            ImageUrl = imageUrl;
            Plot = plot;
            Rank = rank;
            RunningTimeSecs = runningTimeSecs;
            Actors = actors;
        }

        public List<string> Directors { get; set; }
        [JsonPropertyName("release_date")]
        public DateTime ReleaseDate { get; set; }
        public float Rating { get; set; }
        public List<string> Genres { get; set; }
        public string ImageUrl { get; set; }
        public string Plot { get; set; }
        public int Rank { get; set; }
        [JsonPropertyName("running_time_secs")]
        public int RunningTimeSecs { get; set; }
        public List<string> Actors { get; set; }
    }
}