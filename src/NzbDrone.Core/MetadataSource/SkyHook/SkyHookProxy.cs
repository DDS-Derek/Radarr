﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.SkyHook.Resource;
using NzbDrone.Core.Tv;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.SkyHook
{
    public class SkyHookProxy : IProvideSeriesInfo, ISearchForNewSeries
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        private readonly IHttpRequestBuilderFactory _requestBuilder;

        public SkyHookProxy(IHttpClient httpClient, ISonarrCloudRequestBuilder requestBuilder, Logger logger)
        {
            _httpClient = httpClient;
             _requestBuilder = requestBuilder.SkyHookTvdb;
            _logger = logger;
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId)
        {
            var httpRequest = _requestBuilder.Create()
                                             .SetSegment("route", "shows")
                                             .Resource(tvdbSeriesId.ToString())
                                             .Build();

            httpRequest.AllowAutoRedirect = true;
            httpRequest.SuppressHttpError = true;

            string imdbId = string.Format("tt{0:D7}", tvdbSeriesId);

            var imdbRequest = new HttpRequest("http://www.omdbapi.com/?i="+ imdbId + "&plot=full&r=json");

            var httpResponse = _httpClient.Get(imdbRequest);

            if (httpResponse.HasHttpError)
            {
                if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SeriesNotFoundException(tvdbSeriesId);
                }
                else
                {
                    throw new HttpException(httpRequest, httpResponse);
                }
            }

            var response = httpResponse.Content;

            dynamic json = JsonConvert.DeserializeObject(response);

            var series = new Series();

            series.Title = json.Title;
            series.TitleSlug = series.Title.ToLower().Replace(" ", "-");
            series.Overview = json.Plot;
            series.CleanTitle = series.Title;
            series.TvdbId = tvdbSeriesId;
            string airDateStr = json.Released;
            DateTime airDate = DateTime.Parse(airDateStr);
            series.FirstAired = airDate;
            series.Year = airDate.Year;
            series.ImdbId = imdbId;
            series.Images = new List<MediaCover.MediaCover>();
            string url = json.Poster;
            var imdbPoster = new MediaCover.MediaCover(MediaCoverTypes.Poster, url);
            series.Images.Add(imdbPoster);

            var season = new Season();
            season.SeasonNumber = 1;
            season.Monitored = true;
            series.Seasons.Add(season);
            

            var episode = new Episode();

            episode.AirDate = airDate.ToShortTimeString();
            episode.Title = json.Title;
            episode.SeasonNumber = 1;
            episode.EpisodeNumber = 1;
            episode.Overview = series.Overview;
            episode.AirDate = airDate.ToShortDateString();

            var episodes = new List<Episode> { episode };

            return new Tuple<Series, List<Episode>>(series, episodes.ToList());
        }

        public List<Series> SearchForNewSeries(string title)
        {
            try
            {
                var lowerTitle = title.ToLowerInvariant();

                if (lowerTitle.StartsWith("tvdb:") || lowerTitle.StartsWith("tvdbid:"))
                {
                    var slug = lowerTitle.Split(':')[1].Trim();

                    int tvdbId;

                    if (slug.IsNullOrWhiteSpace() || slug.Any(char.IsWhiteSpace) || !int.TryParse(slug, out tvdbId) || tvdbId <= 0)
                    {
                        return new List<Series>();
                    }

                    try
                    {
                        return new List<Series> { GetSeriesInfo(tvdbId).Item1 };
                    }
                    catch (SeriesNotFoundException)
                    {
                        return new List<Series>();
                    }
                }

                var httpRequest = _requestBuilder.Create()
                                                 .SetSegment("route", "search")
                                                 .AddQueryParam("term", title.ToLower().Trim())
                                                 .Build();

                var searchTerm = lowerTitle.Replace("+", "_").Replace(" ", "_");

                var firstChar = searchTerm.First();

                var imdbRequest = new HttpRequest("https://v2.sg.media-imdb.com/suggests/"+firstChar+"/" + searchTerm + ".json");

                var response = _httpClient.Get(imdbRequest);

                var imdbCallback = "imdb$" + searchTerm + "(";

                var responseCleaned = response.Content.Replace(imdbCallback, "").TrimEnd(")");

                dynamic json = JsonConvert.DeserializeObject(responseCleaned);

                var imdbMovies = new List<Series>();

                foreach (dynamic entry in json.d)
                {
                    var imdbMovie = new Series();
                    imdbMovie.ImdbId = entry.id;
                    string noTT = imdbMovie.ImdbId.Replace("tt", "");
                    try
                    {
                        imdbMovie.TvdbId = (int)Double.Parse(noTT);
                    }
                    catch
                    {
                        imdbMovie.TvdbId = 0;
                    }
                    try
                    {
                        imdbMovie.SortTitle = entry.l;
                        imdbMovie.Title = entry.l;
                        string titleSlug = entry.l;
                        imdbMovie.TitleSlug = titleSlug.ToLower().Replace(" ", "-");
                        imdbMovie.Year = entry.y;
                        imdbMovie.Images = new List<MediaCover.MediaCover>();
                        try
                        {
                            string url = entry.i[0];
                            var imdbPoster = new MediaCover.MediaCover(MediaCoverTypes.Poster, url);
                            imdbMovie.Images.Add(imdbPoster);
                        }
                        catch (Exception e)
                        {
                            _logger.Debug(entry);
                            continue;
                        }

                        imdbMovies.Add(imdbMovie);
                    }
                    catch
                    {

                    }
                    
                }

                return imdbMovies;

                var httpResponse = _httpClient.Get<List<ShowResource>>(httpRequest);

                return httpResponse.Resource.SelectList(MapSeries);
            }
            catch (HttpException)
            {
                throw new SkyHookException("Search for '{0}' failed. Unable to communicate with SkyHook.", title);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, ex.Message);
                throw new SkyHookException("Search for '{0}' failed. Invalid response received from SkyHook.", title);
            }
        }

        private static Series MapSeries(ShowResource show)
        {
            var series = new Series();
            series.TvdbId = show.TvdbId;

            if (show.TvRageId.HasValue)
            {
                series.TvRageId = show.TvRageId.Value;
            }

            if (show.TvMazeId.HasValue)
            {
                series.TvMazeId = show.TvMazeId.Value;
            }

            series.ImdbId = show.ImdbId;
            series.Title = show.Title;
            series.CleanTitle = Parser.Parser.CleanSeriesTitle(show.Title);
            series.SortTitle = SeriesTitleNormalizer.Normalize(show.Title, show.TvdbId);

            if (show.FirstAired != null)
            {
                series.FirstAired = DateTime.Parse(show.FirstAired).ToUniversalTime();
                series.Year = series.FirstAired.Value.Year;
            }

            series.Overview = show.Overview;

            if (show.Runtime != null)
            {
                series.Runtime = show.Runtime.Value;
            }

            series.Network = show.Network;

            if (show.TimeOfDay != null)
            {
                series.AirTime = string.Format("{0:00}:{1:00}", show.TimeOfDay.Hours, show.TimeOfDay.Minutes);
            }

            series.TitleSlug = show.Slug;
            series.Status = MapSeriesStatus(show.Status);
            series.Ratings = MapRatings(show.Rating);
            series.Genres = show.Genres;

            if (show.ContentRating.IsNotNullOrWhiteSpace())
            {
                series.Certification = show.ContentRating.ToUpper();
            }
            
            series.Actors = show.Actors.Select(MapActors).ToList();
            series.Seasons = show.Seasons.Select(MapSeason).ToList();
            series.Images = show.Images.Select(MapImage).ToList();

            return series;
        }

        private static Actor MapActors(ActorResource arg)
        {
            var newActor = new Actor
            {
                Name = arg.Name,
                Character = arg.Character
            };

            if (arg.Image != null)
            {
                newActor.Images = new List<MediaCover.MediaCover>
                {
                    new MediaCover.MediaCover(MediaCoverTypes.Headshot, arg.Image)
                };
            }

            return newActor;
        }

        private static Episode MapEpisode(EpisodeResource oracleEpisode)
        {
            var episode = new Episode();
            episode.Overview = oracleEpisode.Overview;
            episode.SeasonNumber = oracleEpisode.SeasonNumber;
            episode.EpisodeNumber = oracleEpisode.EpisodeNumber;
            episode.AbsoluteEpisodeNumber = oracleEpisode.AbsoluteEpisodeNumber;
            episode.Title = oracleEpisode.Title;

            episode.AirDate = oracleEpisode.AirDate;
            episode.AirDateUtc = oracleEpisode.AirDateUtc;

            episode.Ratings = MapRatings(oracleEpisode.Rating);

            //Don't include series fanart images as episode screenshot
            if (oracleEpisode.Image != null)
            {
                episode.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Screenshot, oracleEpisode.Image));
            }

            return episode;
        }

        private static Season MapSeason(SeasonResource seasonResource)
        {
            return new Season
            {
                SeasonNumber = seasonResource.SeasonNumber,
                Images = seasonResource.Images.Select(MapImage).ToList()
            };
        }

        private static SeriesStatusType MapSeriesStatus(string status)
        {
            if (status.Equals("ended", StringComparison.InvariantCultureIgnoreCase))
            {
                return SeriesStatusType.Ended;
            }

            return SeriesStatusType.Continuing;
        }

        private static Ratings MapRatings(RatingResource rating)
        {
            if (rating == null)
            {
                return new Ratings();
            }

            return new Ratings
            {
                Votes = rating.Count,
                Value = rating.Value
            };
        }

        private static MediaCover.MediaCover MapImage(ImageResource arg)
        {
            return new MediaCover.MediaCover
            {
                Url = arg.Url,
                CoverType = MapCoverType(arg.CoverType)
            };
        }

        private static MediaCoverTypes MapCoverType(string coverType)
        {
            switch (coverType.ToLower())
            {
                case "poster":
                    return MediaCoverTypes.Poster;
                case "banner":
                    return MediaCoverTypes.Banner;
                case "fanart":
                    return MediaCoverTypes.Fanart;
                default:
                    return MediaCoverTypes.Unknown;
            }
        }
    }
}
