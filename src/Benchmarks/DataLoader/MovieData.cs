using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Benchmarks
{
    public class MovieData
    {
        public int Year { get; set; }
        public string Title { get; set; }
        public MoveInfo Info { get; set; }
    }

    public class MoveInfo
    {
        public List<string> Directors { get; set; }
        [JsonProperty("release_date")]
        public DateTime ReleaseDate { get; set; }
        public float Rating { get; set; }
        public List<string> Genres { get; set; }
        public string ImageUrl { get; set; }
        public string Plot { get; set; }
        public int Rank { get; set; }
        [JsonProperty("running_time_secs")]
        public int RunningTimeSecs { get; set; }
        public List<string> Actors { get; set; }
    }
}